using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The S1 RTG's cartridge (reactor.md → S1: RTG): a drawer on rails that a tug slides fully out and a push slides
    /// home, as a pack's lid (its TugSlide). Home it is on at full output at once; the moment it starts out it is off.
    /// That is the reactor's only way off.
    ///
    /// Out, it takes one fuel rod by hand: a rod let go near its place between the cartridge's points, while the
    /// reactor has no rod and none waiting, goes in (its LoadingModule loads it, which the networking passes on) and
    /// is removed. The rod is shown in the cartridge while there is one; a spent one just vanishes.
    ///
    /// Open or shut is the reactor's state, decided by the master: a hand here moves it at once and Tugged tells the
    /// networking, which asks the master; every client slides it from that state.
    [RequireComponent(typeof(ReactorBehaviourModule))]
    public sealed class ReactorCartridge : MonoBehaviour, INetworkedState
    {
        [Tooltip("The cartridge's slide: a tug out opens it, a push in shuts it.")]
        [SerializeField] private TugSlide _slide;
        [Tooltip("Where the rod stands in the cartridge, between its points: a child of the cartridge, so it moves with it.")]
        [SerializeField] private Transform _rodPlace;
        [Tooltip("The fuel rod item, whose model is shown in the cartridge.")]
        [SerializeField] private LoadableItem _rod;
        [Tooltip("How near its place a rod's centre must be let go to go in, in metres.")]
        [SerializeField] private float _takeDistance = 0.3f;
        [Tooltip("Seconds a hand's move here is kept against the master's older state, while the master hears of it.")]
        [SerializeField] private float _claimSeconds = 1f;

        private ReactorBehaviourModule _reactor;
        private LoadingModule _fuel;
        private Transform _shown;
        private float _claimUntil;

        /// Raised on the client whose hand opened or shut it: true opened.
        public event Action<bool> Tugged;

        /// True while it is out or sliding out.
        public bool Open => _slide != null && _slide.Open;

        /// Opens or shuts it: the master, told by a hand elsewhere.
        public void SetOpen(bool open)
        {
            if (_slide != null) _slide.Apply(open);
        }

        private void Awake()
        {
            _reactor = GetComponent<ReactorBehaviourModule>();
            if (_slide != null) _slide.ChangedByHand += OnSlid;
        }

        private void Start()
        {
            var fuel = _reactor.Fuel;
            _fuel = fuel.Count > 0 ? fuel[0] : null;
            if (_rodPlace != null && _rod != null) _shown = Picture(_rod, _rodPlace);
        }

        private void OnEnable() => LoadableItem.LetGo += OnItemLetGo;

        private void OnDisable() => LoadableItem.LetGo -= OnItemLetGo;

        private void OnDestroy()
        {
            if (_slide != null) _slide.ChangedByHand -= OnSlid;
        }

        private void OnSlid()
        {
            _claimUntil = Time.time + _claimSeconds;
            Tugged?.Invoke(_slide.Open);
        }

        private void Update()
        {
            if (_reactor == null || _slide == null) return;
            // Home and shut, full output; anything else, off. Every client works it out from the shared state.
            bool home = !_slide.Open && _slide.Raised <= 0f;
            float want = home ? 1f : 0f;
            if (_reactor.GetDial(1) != want) _reactor.SetDial(1, want);

            var chamber = _reactor.ChamberCount > 0 ? _reactor.Chamber(0) : null;
            bool rod = chamber != null && chamber.HasRod || _fuel != null && _fuel.HasItem;
            if (_shown != null && _shown.gameObject.activeSelf != rod) _shown.gameObject.SetActive(rod);
        }

        /// A fuel rod let go near its place in the open cartridge, with no rod in, goes in.
        private void OnItemLetGo(LoadableItem item)
        {
            if (item == null || item.Picture || _slide == null || _slide.Raised < 1f || _fuel == null || _rodPlace == null) return;
            if (!_fuel.Accepts(item) || !item.Grab.enabled || _fuel.HasItem) return;
            if (_reactor.ChamberCount > 0 && _reactor.Chamber(0).HasRod) return;    // one rod; no spare waits
            if (Vector3.Distance(item.transform.position, _rodPlace.position) > _takeDistance) return;

            // Out of sight at once, its picture in its place, while the despawn goes through.
            item.Grab.enabled = false;
            item.Body.isKinematic = true;
            foreach (var c in item.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var r in item.GetComponentsInChildren<Renderer>()) r.enabled = false;
            _fuel.LoadByHand(item);
            item.RemoveForEveryone();
        }

        /// A picture of the rod's model standing upright at its place: its meshes only, nothing to grab or collide with.
        private static Transform Picture(LoadableItem prefab, Transform place)
        {
            var root = new GameObject($"{prefab.name} (in cartridge)").transform;
            root.SetParent(place, false);
            foreach (var source in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var part = new GameObject(source.name).transform;
                part.SetParent(root, false);
                part.localPosition = prefab.transform.InverseTransformPoint(source.transform.position);
                part.localRotation = Quaternion.Inverse(prefab.transform.rotation) * source.transform.rotation;
                part.localScale = source.transform.lossyScale;
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<Renderer>().sharedMaterials;
            }
            root.gameObject.SetActive(false);
            return root;
        }

        /// Whether it is out.
        public int StateCount => 1;

        public void WriteState(float[] to, int at) => to[at] = Open ? 1f : 0f;

        public void ReadState(float[] from, int at)
        {
            if (Time.time < _claimUntil) return;     // a hand here moved it; the master has not caught up yet
            SetOpen(from[at] > 0.5f);
        }
    }
}
