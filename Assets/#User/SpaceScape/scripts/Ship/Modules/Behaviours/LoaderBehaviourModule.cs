using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The item loader (loading.md → the loader's queue). A pack let go in the intake in front of it is drawn
    /// onto the start of its path and becomes the loader's state: which kind of pack, which of its places are
    /// filled, and how far along the path it is. Runners carry each pack along the path to the furthest free
    /// place; the last place is under the mechanism, the pack lifted into it. An empty pack, or every pack while
    /// the eject switch is on, carries on past the last place and out of the loader, and is put back into the
    /// world as a real pack. Unpowered, nothing moves and nothing is taken in.
    ///
    /// Every client runs the conveying from the state, and the master's state corrects it. Only the master puts a
    /// leaving pack out (Decides). A hand on any client feeding a pack, or throwing the eject switch, changes it
    /// here at once and the networking passes it on to the master. The packs are shown with ItemPackPicture.
    public sealed class LoaderBehaviourModule : LoadBehaviourModule, ISwitch
    {
        private const float Epsilon = 0.001f;
        private const float TrustLocalSeconds = 1f;   // after a hand here changes it, before the master's state is taken again
        private const float Overlap = 0.02f;          // how far two packs may touch without counting as in each other's way

        [Header("Queue")]
        [Tooltip("The packs it can hold and put back out. It takes any pack of one of these types (loaders are dumb), and no other.")]
        [SerializeField] private ItemPack[] _packs = new ItemPack[0];
        [Tooltip("The way a pack travels, in order: where a fed pack is set down, through every place, to where a leaving pack drops out. Each point is where a pack's origin (the bottom of its box) goes. Children of the loader.")]
        [SerializeField] private Transform[] _path = new Transform[0];
        [Tooltip("The points on the path where a pack waits, from the front of the queue to the end. The last is under the mechanism, the pack lifted into it. Each must also be on the path. One per pack it holds.")]
        [SerializeField] private Transform[] _places = new Transform[0];
        [Tooltip("How fast a pack moves along the path, in metres a second.")]
        [SerializeField] private float _speed = 0.5f;
        [Tooltip("A pack's size, for keeping packs out of each other: width, height, depth, in metres.")]
        [SerializeField] private Vector3 _packSize = new Vector3(1.1f, 1.15f, 1.1f);
        [Tooltip("Ticked: the eject switch starts on.")]
        [SerializeField] private bool _ejecting;

        [Header("Intake")]
        [Tooltip("The middle of the box where a pack let go is taken in; the box turns with it.")]
        [SerializeField] private Transform _intake;
        [Tooltip("The intake box's size, in metres.")]
        [SerializeField] private Vector3 _intakeSize = new Vector3(1.3f, 1.6f, 1.2f);
        [Tooltip("Seconds a pack taken in takes to settle onto the start of the path.")]
        [SerializeField] private float _drawInSeconds = 0.6f;
        [Tooltip("Seconds between looks at the packs let go in the intake that are still waiting to go in.")]
        [SerializeField] private float _checkSeconds = 0.25f;

        [Header("Sound")]
        [Tooltip("Looped while any pack is moving.")]
        [SerializeField] private AudioClip _runningClip;
        [Tooltip("Plays the running loop. A child object on the Environment layer, so the player's volume controls apply.")]
        [SerializeField] private AudioSource _sound;

        [Header("Front rails")]
        [Tooltip("The rails that slide forward out of the front while a pack it would take is in the intake: the sign it is ready to take it. Slides along its parent's forward.")]
        [SerializeField] private Transform _rails;
        [Tooltip("How far the rails slide out, in metres.")]
        [SerializeField] private float _railsReach = 1.1f;
        [Tooltip("How fast the rails slide, in metres a second.")]
        [SerializeField] private float _railsSpeed = 0.8f;
        [Tooltip("Looped while the rails are sliding.")]
        [SerializeField] private AudioClip _railsClip;
        [Tooltip("Plays the rails loop. A child object on the Environment layer, so the player's volume controls apply.")]
        [SerializeField] private AudioSource _railsSound;

        private static readonly Collider[] Found = new Collider[32];
        private readonly HashSet<ItemPack> _waiting = new HashSet<ItemPack>();
        private readonly List<ItemPack> _gone = new List<ItemPack>();
        private int[] _kind;      // per state entry: 0 none, else 1 + the index in Packs
        private int[] _filled;    // which of the pack's places are filled, one bit each
        private float[] _at;      // how far along the path, in metres
        private int[] _order;
        private ItemPackPicture[] _pictures;
        private Vector3[] _points;
        private float[] _along;   // how far along the path each point is
        private float[] _stops;   // how far along the path each place is
        private float _end;
        private ItemPack _drawing;
        private float _nextCheck;
        private int _sending;        // the item in the tube: 0 none, else 1 + the index in Packs of the pack it came from
        private float _sent;         // how far along the route it has travelled, in metres
        private ClippedItemPicture _sendingPicture;
        private ComponentEdgeModule _edges;
        private Vector3 _railsIn;
        private float _railsOut;     // metres slid out
        private bool _railsWanted;
        private float _nextPutOut;
        private float _trustLocalUntil;

        /// True where this client decides: the master, or anywhere with no network. Set by the networking.
        public bool Decides { get; set; } = true;

        /// Puts a leaving pack back into the world, set by the networking: the pack, or null when it could not
        /// (it tries again). Unset, the pack is made here.
        public Func<ItemPack, Vector3, Quaternion, ItemPack> PutOut;

        /// Raised on the client whose hand fed a pack: its kind (the index in Packs) and its filled places.
        public event Action<int, int> FedByHand;

        /// Raised on the client whose hand threw the eject switch: on or off.
        public event Action<bool> EjectPressed;

        /// True while the eject switch is on. The master sets it from a hand elsewhere.
        public bool Ejecting
        {
            get => _ejecting;
            set => _ejecting = value;
        }

        /// The eject switch, for its rocker: on while ejecting.
        public bool Closed => _ejecting;

        /// Throws the eject switch by hand: here at once, and passed on to the master.
        public void Press(bool closed)
        {
            _ejecting = closed;
            _trustLocalUntil = Time.time + TrustLocalSeconds;
            EjectPressed?.Invoke(closed);
        }

        private int Entries => _places.Length + 1;   // every place, and one leaving

        private void Awake()
        {
            Ready();
            _edges = GetComponent<ComponentEdgeModule>();
            _points = new Vector3[_path.Length];
            _along = new float[_path.Length];
            for (int i = 0; i < _path.Length; i++)
            {
                _points[i] = _path[i] != null ? transform.InverseTransformPoint(_path[i].position) : Vector3.zero;
                _along[i] = i == 0 ? 0f : _along[i - 1] + Vector3.Distance(_points[i - 1], _points[i]);
            }
            _end = _path.Length > 0 ? _along[_path.Length - 1] : 0f;
            _stops = new float[_places.Length];
            for (int i = 0; i < _places.Length; i++)
            {
                int on = Array.IndexOf(_path, _places[i]);
                if (on < 0) Debug.LogError($"Loader: '{name}' place {i} is not on its path", this);
                _stops[i] = on < 0 ? 0f : _along[on];
            }
            if (_rails != null) _railsIn = _rails.localPosition;
            if (_path.Length < 2 || _places.Length == 0) Debug.LogError($"Loader: '{name}' needs a path and at least one place", this);
        }

        private void Ready()
        {
            if (_kind != null) return;
            _kind = new int[Entries];
            _filled = new int[Entries];
            _at = new float[Entries];
            _order = new int[Entries];
            _pictures = new ItemPackPicture[Entries];
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ItemPack.LetGo += OnPackLetGo;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ItemPack.LetGo -= OnPackLetGo;
        }

        private void OnDestroy()
        {
            if (_pictures == null) return;
            foreach (var picture in _pictures) picture?.Destroy();
            _sendingPicture?.Destroy();
        }

        private void Update()
        {
            if (_points == null || _path.Length < 2 || _places.Length == 0) return;
            bool moving = Working && Convey(Time.deltaTime);
            Sound(moving);
            SlideRails(Time.deltaTime);
            Send(Time.deltaTime);
            if (Decides && Time.time >= _nextPutOut) PutOutLeft();
            Show();

            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + _checkSeconds;
                CheckWaiting();
                _railsWanted = _drawing != null || TakeableInRange();
            }
        }

        /// Moves each pack toward the furthest place it may have, the one ahead first; a leaving pack toward the
        /// end of the path. A pack never moves into the one ahead of it. True when any pack moved.
        private bool Convey(float seconds)
        {
            bool moved = false;
            int count = Ordered();
            int last = _places.Length - 1;
            int aheadStop = _places.Length;
            for (int k = 0; k < count; k++)
            {
                int i = _order[k];
                bool leaving = _at[i] > _stops[last] + Epsilon ||
                               (_at[i] >= _stops[last] - Epsilon && (_ejecting || _filled[i] == 0));
                int stop = leaving ? _places.Length : Mathf.Min(aheadStop - 1, last);
                float target = leaving ? _end : stop >= 0 ? _stops[stop] : _at[i];
                aheadStop = stop;
                if (target <= _at[i]) continue;

                float next = Mathf.MoveTowards(_at[i], target, _speed * seconds);
                if (InTheWay(next, k)) continue;
                _at[i] = next;
                moved = true;
            }
            return moved;
        }

        // ---- Sending: one item at a time from the pack in the mechanism, along the item conduit from its ItemOut port.

        /// Starts sending an item when the component at the other end of the item conduit wants one of the kind
        /// the pack in the mechanism holds (its LoadingModule takes that type and holds none), and carries it
        /// there. Every client moves the item; only the master sends one and decides it has arrived, loading it
        /// into that LoadingModule. Unpowered, the item stops where it is.
        private void Send(float seconds)
        {
            var conduits = ItemConduits.Current;
            var route = conduits != null && _edges != null ? conduits.RouteFrom(_edges) : null;
            if (_sending != 0)
            {
                var item = PackOf(_sending) != null ? PackOf(_sending).Contains : null;
                if (route == null || item == null)
                {
                    HideSending();
                    if (!Decides) return;
                    Debug.LogWarning($"Loader: '{name}' lost the item it was sending: its item conduit is gone", this);
                    _sending = 0;
                    return;
                }
                var picture = SendingPicture(item);
                float total = route.Length(picture.Length);
                if (Working) _sent = Mathf.Min(total, _sent + conduits.ItemSpeed * seconds);
                route.Pose(_sent, picture.Length, out var centre, out var turn, out var keepA, out var keepB);
                picture.Show(centre, turn, keepA, keepB);
                if (Decides && _sent >= total) Arrive(route, item);
                return;
            }
            HideSending();
            if (!Decides || !Working || _ejecting || route == null) return;

            int engaged = Engaged();
            if (engaged < 0 || _filled[engaged] == 0) return;
            var pack = PackOf(_kind[engaged]);
            if (pack == null || pack.Contains == null || Wanting(route, pack.Contains) == null) return;
            int top = 31;
            while (top > 0 && (_filled[engaged] & (1 << top)) == 0) top--;
            _filled[engaged] &= ~(1 << top);
            _sending = _kind[engaged];
            _sent = 0f;
        }

        /// The master: the item is inside the receiver, and is loaded into the LoadingModule that wants it.
        private void Arrive(ItemConduits.Route route, LoadableItem item)
        {
            var loading = Wanting(route, item);
            if (loading != null) loading.Load();
            else Debug.LogWarning($"Loader: '{name}' sent '{item.name}' to '{route.Receiver.name}', which no longer wants it; it is lost", this);
            _sending = 0;
            HideSending();
        }

        /// The receiver's LoadingModule that would take this item now, or null.
        private static LoadingModule Wanting(ItemConduits.Route route, LoadableItem item)
        {
            if (route.Receiver == null) return null;
            foreach (var loading in route.Receiver.GetComponents<LoadingModule>())
                if (loading.Accepts(item) && !loading.HasItem) return loading;
            return null;
        }

        /// The pack lifted into the mechanism, or -1.
        private int Engaged()
        {
            float stop = _stops[_stops.Length - 1];
            for (int i = 0; i < Entries; i++)
                if (_kind[i] != 0 && Mathf.Abs(_at[i] - stop) <= Epsilon) return i;
            return -1;
        }

        private ClippedItemPicture SendingPicture(LoadableItem item)
        {
            if (_sendingPicture != null && _sendingPicture.Prefab == item) return _sendingPicture;
            _sendingPicture?.Destroy();
            _sendingPicture = new ClippedItemPicture(item, $"{item.name} (sent by {name})");
            return _sendingPicture;
        }

        private void HideSending() => _sendingPicture?.Hide();

        /// Slides the rails out while a pack it would take is in range, and back in once there is none, looping
        /// their sound while they move. Each client works this out from the packs it sees, so nothing is sent.
        private void SlideRails(float seconds)
        {
            if (_rails == null) return;
            float was = _railsOut;
            _railsOut = Mathf.MoveTowards(_railsOut, _railsWanted ? _railsReach : 0f, _railsSpeed * seconds);
            bool moving = _railsOut != was;
            if (moving) _rails.localPosition = _railsIn + Vector3.forward * _railsOut;
            if (_railsSound == null || _railsClip == null) return;
            if (moving && !_railsSound.isPlaying)
            {
                _railsSound.clip = _railsClip;
                _railsSound.loop = true;
                _railsSound.Play();
            }
            else if (!moving && _railsSound.isPlaying) _railsSound.Stop();
        }

        /// True when a pack it would take now, held or not, is in the intake.
        private bool TakeableInRange()
        {
            if (_intake == null || !CanTakeIn) return false;
            int count = Physics.OverlapBoxNonAlloc(_intake.position, _intakeSize * 0.5f, Found, _intake.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var pack = Found[i] != null ? Found[i].GetComponentInParent<ItemPack>() : null;
                if (pack != null && KindOf(pack) != 0) return true;
            }
            return false;
        }

        /// Each client loops the running sound while it sees a pack moving, so nothing is sent for it.
        private void Sound(bool moving)
        {
            if (_sound == null || _runningClip == null) return;
            if (moving && !_sound.isPlaying)
            {
                _sound.clip = _runningClip;
                _sound.loop = true;
                _sound.Play();
            }
            else if (!moving && _sound.isPlaying) _sound.Stop();
        }

        /// The packs in, furthest along first, in _order. How many there are.
        private int Ordered()
        {
            int count = 0;
            for (int i = 0; i < Entries; i++)
            {
                if (_kind[i] == 0) continue;
                int k = count++;
                while (k > 0 && _at[_order[k - 1]] < _at[i]) { _order[k] = _order[k - 1]; k--; }
                _order[k] = i;
            }
            return count;
        }

        /// True when a pack at this point would be in the way of one of the first `ahead` packs in _order.
        private bool InTheWay(float at, int ahead)
        {
            var here = PointAt(at);
            for (int k = 0; k < ahead; k++)
                if (Touching(here, PointAt(_at[_order[k]]))) return true;
            return false;
        }

        private bool Touching(Vector3 a, Vector3 b)
        {
            var d = a - b;
            return Mathf.Abs(d.x) < _packSize.x - Overlap && Mathf.Abs(d.y) < _packSize.y - Overlap &&
                   Mathf.Abs(d.z) < _packSize.z - Overlap;
        }

        /// Where a pack this far along the path is, in the loader's space.
        private Vector3 PointAt(float at)
        {
            for (int i = 1; i < _points.Length; i++)
            {
                if (at > _along[i]) continue;
                float length = _along[i] - _along[i - 1];
                return length <= 0f ? _points[i] : Vector3.Lerp(_points[i - 1], _points[i], (at - _along[i - 1]) / length);
            }
            return _points[_points.Length - 1];
        }

        /// The master puts every pack that has reached the end of the path back into the world as a real pack,
        /// holding what it held, and drops it from the queue.
        private void PutOutLeft()
        {
            for (int i = 0; i < Entries; i++)
            {
                if (_kind[i] == 0 || _at[i] < _end - Epsilon) continue;
                var prefab = PackOf(_kind[i]);
                var at = transform.TransformPoint(_points[_points.Length - 1]);
                var pack = prefab == null ? null : PutOut != null ? PutOut(prefab, at, transform.rotation) : Instantiate(prefab, at, transform.rotation);
                if (prefab != null && pack == null)
                {
                    _nextPutOut = Time.time + 1f;   // try again in a moment
                    return;
                }
                if (pack != null)
                {
                    pack.Apply(_filled[i], false);
                    pack.Body.isKinematic = false;
                }
                _kind[i] = 0;
            }
        }

        /// Shows a picture of each pack where it is on the path, its lamps showing how many are in.
        private void Show()
        {
            for (int i = 0; i < Entries; i++)
            {
                var prefab = PackOf(_kind[i]);
                if (_pictures[i] != null && _pictures[i].Prefab != prefab)
                {
                    _pictures[i].Destroy();
                    _pictures[i] = null;
                }
                if (prefab == null) continue;
                if (_pictures[i] == null) _pictures[i] = new ItemPackPicture(prefab, transform);
                _pictures[i].Root.localPosition = PointAt(_at[i]);
                _pictures[i].Root.localRotation = Quaternion.identity;
                _pictures[i].ShowCount(_filled[i]);
            }
        }

        private ItemPack PackOf(int kind) => kind > 0 && kind <= _packs.Length ? _packs[kind - 1] : null;

        /// 1 + the index in Packs of a pack of this one's type, or 0 for a type it does not hold.
        private int KindOf(ItemPack pack)
        {
            for (int i = 0; i < _packs.Length; i++)
                if (_packs[i] != null && _packs[i].GetType() == pack.GetType()) return i + 1;
            return 0;
        }

        /// Puts a pack on the start of the path. The master calls it for a pack fed elsewhere; false when there was
        /// no room in the state for it.
        public bool Add(int kind, int filled)
        {
            Ready();
            if (PackOf(kind) == null) return false;
            for (int i = 0; i < Entries; i++)
            {
                if (_kind[i] != 0) continue;
                _kind[i] = kind;
                _filled[i] = filled;
                _at[i] = 0f;
                return true;
            }
            Debug.LogWarning($"Loader: '{name}' had no room for a pack", this);
            return false;
        }

        /// True while a pack can be taken in: powered, a place free, and the start of the path clear. Ejecting takes
        /// it in too, and runs it straight through and out.
        private bool CanTakeIn
        {
            get
            {
                if (!Working || _points == null || _points.Length < 2) return false;
                int queued = 0, free = 0;
                var start = _points[0];
                for (int i = 0; i < Entries; i++)
                {
                    if (_kind[i] == 0) { free++; continue; }
                    if (_at[i] <= _stops[_stops.Length - 1] + Epsilon) queued++;
                    if (Touching(start, PointAt(_at[i]))) return false;
                }
                return free > 0 && queued < _places.Length;
            }
        }

        // ---- Intake: a pack a hand on this client lets go of in the box is drawn in when there is room.

        private void OnPackLetGo(ItemPack pack)
        {
            if (pack == null || !InIntake(pack)) return;
            _waiting.Add(pack);
            TryTakeIn(pack);
        }

        /// Takes in a waiting pack once there is room; forgets one picked up again or moved out of the box.
        private void CheckWaiting()
        {
            foreach (var pack in _waiting)
                if (pack == null || pack.Grab.isSelected || !InIntake(pack)) _gone.Add(pack);
            foreach (var pack in _gone) _waiting.Remove(pack);
            _gone.Clear();
            foreach (var pack in _waiting)
                if (TryTakeIn(pack)) break;
        }

        private bool TryTakeIn(ItemPack pack)
        {
            if (_drawing != null || !CanTakeIn) return false;
            int kind = KindOf(pack);
            if (kind == 0) return false;
            StartCoroutine(DrawIn(pack, kind));
            return true;
        }

        private bool InIntake(ItemPack pack)
        {
            if (_intake == null) return false;
            var local = _intake.InverseTransformPoint(pack.Body.worldCenterOfMass);
            var half = _intakeSize * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// Settles the pack onto the start of the path, upright and shut, then swaps it for a picture there and
        /// removes it. A hand taking it back first, or the room going, leaves it be.
        private IEnumerator DrawIn(ItemPack pack, int kind)
        {
            _drawing = pack;
            pack.Grab.enabled = false;
            pack.Body.isKinematic = true;
            var colliders = pack.GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;

            var fromAt = pack.transform.position;
            var fromTurn = pack.transform.rotation;
            var toAt = transform.TransformPoint(_points[0]);
            for (float t = 0f; t < 1f && pack != null; t += Time.fixedDeltaTime / Mathf.Max(0.01f, _drawInSeconds))
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                pack.Body.MovePosition(Vector3.Lerp(fromAt, toAt, k));
                pack.Body.MoveRotation(Quaternion.Slerp(fromTurn, transform.rotation, k));
                yield return new WaitForFixedUpdate();
            }
            _drawing = null;
            if (pack == null) yield break;

            if (!CanTakeIn)
            {
                foreach (var c in colliders) if (c != null) c.enabled = true;
                pack.Grab.enabled = true;
                pack.Body.isKinematic = false;
                yield break;
            }

            _waiting.Remove(pack);
            foreach (var r in pack.GetComponentsInChildren<Renderer>()) r.enabled = false;
            int filled = pack.Filled;
            Add(kind, filled);
            _trustLocalUntil = Time.time + TrustLocalSeconds;
            FedByHand?.Invoke(kind, filled);
            pack.RemoveForEveryone();
        }

        // ---- State: the eject switch, the item being sent and how far it has gone, then per entry its kind,
        // filled places and how far along.

        public override int StateCount => base.StateCount + 3 + 3 * Entries;

        public override void WriteState(float[] to, int at)
        {
            Ready();
            base.WriteState(to, at);
            at += base.StateCount;
            to[at++] = _ejecting ? 1f : 0f;
            to[at++] = _sending;
            to[at++] = _sent;
            for (int i = 0; i < Entries; i++)
            {
                to[at++] = _kind[i];
                to[at++] = _filled[i];
                to[at++] = _at[i];
            }
        }

        public override void ReadState(float[] from, int at)
        {
            Ready();
            base.ReadState(from, at);
            if (Time.time < _trustLocalUntil) return;   // a hand here just changed it; the master has not caught up
            at += base.StateCount;
            _ejecting = from[at++] > 0.5f;
            int sending = Mathf.RoundToInt(from[at++]);
            float sent = from[at++];
            if (sending != _sending || Mathf.Abs(sent - _sent) > 0.3f) _sent = sent;
            _sending = sending;
            for (int i = 0; i < Entries; i++)
            {
                int kind = Mathf.RoundToInt(from[at++]);
                int filled = Mathf.RoundToInt(from[at++]);
                float along = from[at++];
                // Close enough is left alone, so a pack this client is moving does not jump back and forth.
                if (kind != _kind[i] || Mathf.Abs(along - _at[i]) > 0.15f) _at[i] = along;
                _kind[i] = kind;
                _filled[i] = filled;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            for (int i = 1; i < _path.Length; i++)
                if (_path[i - 1] != null && _path[i] != null) Gizmos.DrawLine(_path[i - 1].position, _path[i].position);
            foreach (var place in _places)
                if (place != null) Gizmos.DrawWireCube(place.position + place.up * (_packSize.y * 0.5f), _packSize);
            if (_intake == null) return;
            Gizmos.matrix = _intake.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _intakeSize);
        }
    }
}
