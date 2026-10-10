using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A box of loadable items a hand takes out one at a time (loading.md, the fuel pack). What it holds, is its
    /// type: each kind of pack is a subclass (FuelRodItemPack), with its Contains slot naming the item.
    ///
    /// The items in it are never spawned. The pack keeps which of its places are filled, and shows a local
    /// picture of the item in each. A short tug up on the lid starts it rising, lifting the pictures clear of the
    /// box; a short push down on the open lid starts it sinking shut. A hand taking a picture is given a real,
    /// spawned item instead (TakeOut, set by the networking) and the place empties; an item of its type let go
    /// near an empty place in the open pack is removed and the place fills. The count lamps show how many are in.
    ///
    /// Which places are filled, and whether the lid is open, are the pack's own state: the client whose hand
    /// changes them raises ChangedByHand, and the networking (ItemPackNetwork) shares them; every client runs the
    /// lid's rise and fall, and its sounds, from that, so everyone sees and hears it move.
    public abstract class ItemPack : Item
    {
        [Tooltip("The item it holds: a LoadableItem prefab. A taken one is spawned from it; one of its type goes back in.")]
        [SerializeField] private LoadableItem _contains;
        [Tooltip("Where each item hangs under the lid: an item's centre and turn. Under the lid's track, so they rise with it.")]
        [SerializeField] private Transform[] _places = new Transform[0];
        [Tooltip("How many places are filled when it appears, from the first.")]
        [SerializeField] private int _startsWith = 4;

        [Header("Lid")]
        [Tooltip("Moved up to open: the lid's grip and the places hang under it.")]
        [SerializeField] private Transform _lidTrack;
        [Tooltip("The lid a hand tugs: an XRGrabInteractable with a SlideGrabTransformer that slides both ways.")]
        [SerializeField] private XRGrabInteractable _lid;
        [Tooltip("How far the lid rises when open, in metres.")]
        [SerializeField] private float _rise = 1.08f;
        [Tooltip("Seconds the lid takes to rise or sink.")]
        [SerializeField] private float _riseSeconds = 2f;
        [Tooltip("Share of the lid's slide a tug or push must reach to start it, 0 to 1.")]
        [SerializeField, Range(0.1f, 1f)] private float _tugShare = 0.8f;
        [Tooltip("Seconds the lid takes to slide home in its track once let go.")]
        [SerializeField] private float _returnSeconds = 0.15f;

        [Header("Sound")]
        [Tooltip("Played as the lid starts to rise.")]
        [SerializeField] private AudioClip _openClip;
        [Tooltip("Played as the lid starts to sink.")]
        [SerializeField] private AudioClip _closeClip;
        [Tooltip("Plays the lid sounds. A child object on the Environment layer, so the player's volume controls apply.")]
        [SerializeField] private AudioSource _sound;

        [Header("Taking back")]
        [Tooltip("How near an empty place an item's centre must be let go to go back in, in metres.")]
        [SerializeField] private float _returnDistance = 0.25f;

        [Header("Count lamps")]
        [Tooltip("One per place, lit from the first while that many are in.")]
        [SerializeField] private Renderer[] _lamps = new Renderer[0];
        [Tooltip("A lamp's material while unlit.")]
        [SerializeField] private Material _lampOff;

        private static readonly List<Collider> Colliders = new List<Collider>();
        private LoadableItem[] _pictures = new LoadableItem[0];
        private Material[] _lampOn = new Material[0];
        private SlideGrabTransformer _slide;
        private Vector3 _closedAt;
        private int _filled = -1;
        private bool _open;
        private float _raised;   // 0 shut to 1 open
        private bool _tugged;    // the held lid has already started a move this grab
        private bool _soundedOpen;
        private int _shownFilled = -1;
        private float _shownRaised = -1f;

        /// Raised when the last hand on this client lets go of a pack, for a loader's intake.
        public static event Action<ItemPack> LetGo;

        /// Raised on the client whose hand opened or shut the lid, or took or put back an item.
        public event Action ChangedByHand;

        /// Raised for each picture as it is made, before any hand can reach it, for the networking to strip.
        public event Action<LoadableItem> PictureMade;

        /// Gives the hand holding a picture a real item in its place; true when it did, false with no network.
        /// Set by the networking.
        public Func<LoadableItem, LoadableItem, bool> TakeOut;

        /// The item prefab it holds.
        public LoadableItem Contains => _contains;

        /// Which places are filled, one bit each from the first.
        public int Filled => _filled < 0 ? StartingFilled : _filled;

        /// How many places it has.
        public int Capacity => _places.Length;

        /// The count lamps, lit from the first, for a picture of the pack to copy (ItemPackPicture).
        public Renderer[] Lamps => _lamps;

        /// A lamp's material while unlit.
        public Material LampOff => _lampOff;

        /// True while the lid is open or opening.
        public bool Open => _open;

        /// How many are in.
        public int Count
        {
            get
            {
                int count = 0;
                for (int bits = Filled; bits != 0; bits &= bits - 1) count++;
                return count;
            }
        }

        private int StartingFilled => (1 << Mathf.Clamp(_startsWith, 0, _places.Length)) - 1;

        /// What is in each place beyond whether it is filled, packed into one int, for a pack whose places differ (the
        /// addon pack: which addon and its state). The loader carries it through and the networking shares it. A pack
        /// whose places all hold the same item has none.
        public virtual int Contents
        {
            get => 0;
            set { }
        }

        /// How far the lid has risen: 0 shut, 1 open.
        protected float Raised => _raised;

        /// Where each item hangs: an item's centre and turn.
        protected Transform PlaceAt(int place) => place >= 0 && place < _places.Length ? _places[place] : null;

        /// A hand on this client filled or emptied places: changes them here and tells the networking.
        protected void SetFilledByHand(int filled)
        {
            _filled = filled & ((1 << _places.Length) - 1);
            ChangedByHand?.Invoke();
        }

        /// True for an item of the type it holds.
        public bool Accepts(LoadableItem item) =>
            _contains != null && item != null && _contains.GetType().IsInstanceOfType(item);

        /// Takes the state the networking was sent.
        public void Apply(int filled, bool open)
        {
            _filled = filled & ((1 << _places.Length) - 1);
            _open = open;
        }

        private void Awake()
        {
            if (_lid != null) _slide = _lid.GetComponent<SlideGrabTransformer>();
            if (_lidTrack != null) _closedAt = _lidTrack.localPosition;
            _lampOn = new Material[_lamps.Length];
            for (int i = 0; i < _lamps.Length; i++) _lampOn[i] = _lamps[i] != null ? _lamps[i].sharedMaterial : null;
            _pictures = new LoadableItem[_places.Length];
        }

        /// The pictures are made here, not in Awake, so the networking (subscribed in its Awake) sees each one.
        protected virtual void Start()
        {
            if (_contains != null)
                for (int i = 0; i < _places.Length; i++) _pictures[i] = MakePicture(i);
            IgnoreOwnColliders();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            LoadableItem.LetGo += OnItemLetGo;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            LoadableItem.LetGo -= OnItemLetGo;
        }

        protected override void OnLetGo(SelectExitEventArgs args)
        {
            base.OnLetGo(args);
            if (!Grab.isSelected) LetGo?.Invoke(this);
        }

        private void Update()
        {
            UpdateLid();

            // Each client plays the lid's sounds from its own lid, so nothing is sent for them.
            if (_open != _soundedOpen)
            {
                _soundedOpen = _open;
                var clip = _open ? _openClip : _closeClip;
                if (clip != null && _sound != null) _sound.PlayOneShot(clip);
            }

            float was = _raised;
            _raised = Mathf.MoveTowards(_raised, _open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, _riseSeconds));
            if (_lidTrack != null && _raised != was)
                _lidTrack.localPosition = _closedAt + Vector3.up * (_rise * Mathf.SmoothStep(0f, 1f, _raised));

            if (Filled != _shownFilled || _raised != _shownRaised) Show();
        }

        private void OnDestroy()
        {
            foreach (var picture in _pictures)
                if (picture != null) picture.Grab.selectEntered.RemoveListener(OnPictureTaken);
        }

        /// A tug up on the shut lid opens it; a push down on the open lid shuts it. Either lets go of the lid, as
        /// it moves off by itself. Let go, it slides home in its track.
        private void UpdateLid()
        {
            if (_lid == null || _slide == null) return;
            bool settled = _raised <= 0f || _raised >= 1f;
            if (_lid.enabled != (settled || _lid.isSelected)) _lid.enabled = settled || _lid.isSelected;

            if (_lid.isSelected)
            {
                if (_tugged || !settled) return;
                bool tugged = !_open && _slide.Pulled >= _slide.Travel * _tugShare;
                bool pushed = _open && _slide.TravelDown > 0f && _slide.Pulled <= -_slide.TravelDown * _tugShare;
                if (!tugged && !pushed) return;
                _tugged = true;
                _filled = Filled;
                _open = tugged;
                ChangedByHand?.Invoke();
                LetGoOf(_lid);
                return;
            }

            _tugged = false;
            var t = _lid.transform;
            if (t.localPosition != _slide.RestPosition)
            {
                float most = Mathf.Max(_slide.Travel, _slide.TravelDown);
                t.localPosition = Vector3.MoveTowards(t.localPosition, _slide.RestPosition,
                    most / Mathf.Max(0.01f, _returnSeconds) * Time.deltaTime);
            }
        }

        /// A hand took the picture in a place: the place empties, and the hand is given a real item. With no
        /// network the picture itself becomes the item, and a new picture is made for the place.
        private void OnPictureTaken(SelectEnterEventArgs args)
        {
            var picture = args.interactableObject.transform.GetComponent<LoadableItem>();
            int place = Array.IndexOf(_pictures, picture);
            if (place < 0 || (Filled & (1 << place)) == 0) return;

            _filled = Filled & ~(1 << place);
            ChangedByHand?.Invoke();
            if (TakeOut != null && TakeOut(picture, _contains)) return;

            // No network: the picture is the item now, and the place gets a new one.
            picture.Grab.selectEntered.RemoveListener(OnPictureTaken);
            picture.Grab.retainTransformParent = false;
            picture.transform.SetParent(null, true);
            picture.Picture = false;
            _pictures[place] = MakePicture(place);
            IgnoreOwnColliders();
        }

        /// Puts a picture taken by a hand back in its place, out of sight until the place is filled again.
        public void ReturnPicture(LoadableItem picture)
        {
            int place = Array.IndexOf(_pictures, picture);
            if (place < 0) return;
            if (picture.Grab.isSelected) LetGoOf(picture.Grab);
            Seat(picture, place);
            _shownFilled = -1;
        }

        /// An item of its type let go near an empty place in the open pack goes back in.
        private void OnItemLetGo(LoadableItem item)
        {
            if (item == null || item.Picture || _raised < 1f || !Accepts(item) || !item.Grab.enabled) return;
            int best = -1;
            float nearest = _returnDistance;
            for (int i = 0; i < _places.Length; i++)
            {
                if ((Filled & (1 << i)) != 0 || _places[i] == null) continue;
                float distance = Vector3.Distance(item.transform.position, _places[i].position);
                if (distance <= nearest) { nearest = distance; best = i; }
            }
            if (best < 0) return;

            // Out of sight at once, its picture in its place, while the despawn goes through.
            item.Grab.enabled = false;
            item.Body.isKinematic = true;
            foreach (var c in item.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var r in item.GetComponentsInChildren<Renderer>()) r.enabled = false;
            _filled = Filled | (1 << best);
            ChangedByHand?.Invoke();
            item.RemoveForEveryone();
        }

        /// Shows a picture in each filled place while the lid is off the box, takeable once it is all the way up,
        /// and lights a lamp for each.
        private void Show()
        {
            _shownFilled = Filled;
            _shownRaised = _raised;
            bool visible = _raised > 0f, takeable = _raised >= 1f, appeared = false;
            for (int i = 0; i < _pictures.Length; i++)
            {
                var picture = _pictures[i];
                if (picture == null || picture.Grab.isSelected) continue;
                bool shown = visible && (Filled & (1 << i)) != 0;
                if (picture.gameObject.activeSelf != shown) { picture.gameObject.SetActive(shown); appeared |= shown; }
                if (picture.Grab.enabled != takeable) picture.Grab.enabled = takeable;
            }
            if (appeared) IgnoreOwnColliders();   // deactivating a collider forgets what it ignored
            int count = Count;
            for (int i = 0; i < _lamps.Length; i++)
                if (_lamps[i] != null) _lamps[i].sharedMaterial = i < count ? _lampOn[i] : _lampOff;
        }

        /// A local, never-spawned copy of the item, hung in a place.
        private LoadableItem MakePicture(int place)
        {
            if (_places[place] == null) return null;
            var picture = Instantiate(_contains, _places[place]);
            picture.name = $"{_contains.name} (in pack)";
            picture.Picture = true;
            PictureMade?.Invoke(picture);
            picture.Grab.selectEntered.AddListener(OnPictureTaken);
            Seat(picture, place);
            _shownFilled = -1;
            return picture;
        }

        private void Seat(LoadableItem picture, int place)
        {
            picture.transform.SetParent(_places[place], false);
            picture.transform.localPosition = Vector3.zero;
            picture.transform.localRotation = Quaternion.identity;
            picture.Body.isKinematic = true;
            picture.gameObject.SetActive(false);
        }

        /// Its own parts never push each other: the lid on the box, the pictures inside it.
        protected void IgnoreOwnColliders()
        {
            GetComponentsInChildren(true, Colliders);
            for (int i = 0; i < Colliders.Count; i++)
                for (int j = i + 1; j < Colliders.Count; j++)
                    if (!Colliders[i].isTrigger && !Colliders[j].isTrigger) Physics.IgnoreCollision(Colliders[i], Colliders[j]);
            Colliders.Clear();
        }

        protected static void LetGoOf(XRGrabInteractable grab)
        {
            var manager = grab.interactionManager;
            if (manager == null) return;
            for (int i = grab.interactorsSelecting.Count - 1; i >= 0; i--)
                manager.SelectExit(grab.interactorsSelecting[i], grab);
        }
    }
}
