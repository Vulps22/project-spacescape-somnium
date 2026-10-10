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
        [Tooltip("The middle of the box where a pack let go is taken in; the box turns with it. It is stretched along the rails to reach the extended rails' end (Rails Reach out from the path's first point), so a pack can be put down anywhere from there to just inside the frame.")]
        [SerializeField] private Transform _intake;
        [Tooltip("The intake box's size, in metres, before it is stretched to the extended rails' end.")]
        [SerializeField] private Vector3 _intakeSize = new Vector3(1.3f, 1.6f, 1.2f);
        [Tooltip("Seconds a pack let go takes to line up on the rails, where they are.")]
        [SerializeField] private float _drawInSeconds = 0.6f;
        [Tooltip("Seconds between looks at the packs let go in the intake that are still waiting to go in.")]
        [SerializeField] private float _checkSeconds = 0.25f;

        [Header("Sending")]
        [Tooltip("Seconds between an item fully clearing the Out port and the next leaving: the gap between items in the tube, as time at the conduit's speed.")]
        [SerializeField] private float _sendGapSeconds = 1f;

        [Header("Sound")]
        [Tooltip("Looped while any pack is moving.")]
        [SerializeField] private AudioClip _runningClip;
        [Tooltip("Plays the running loop. A child object on the Environment layer, so the player's volume controls apply.")]
        [SerializeField] private AudioSource _sound;

        [Header("Front rails")]
        [Tooltip("The rails that slide forward out of the front while a pack it would take is in the intake: the sign it is ready to take it. Slides along its parent's forward. A pack let go on them rides them back in: the path's first stretch, out from its first point by Rails Reach, which is not a place and is never counted as room.")]
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
        private readonly Dictionary<ItemPack, float> _waiting = new Dictionary<ItemPack, float>();   // when each was let go
        private const float WatchSeconds = 3f;   // how long a pack let go outside the intake is watched for falling in
        private readonly List<ItemPack> _gone = new List<ItemPack>();
        private int[] _kind;      // per state entry: 0 none, else 1 + the index in Packs
        private int[] _filled;    // which of the pack's places are filled, one bit each
        private int[] _contents;  // what is in each place, for a pack whose places differ (ItemPack.Contents)
        private float[] _at;      // how far along the path, in metres
        private int[] _order;
        private ItemPackPicture[] _pictures;
        private Vector3[] _points;
        private float[] _along;   // how far along the path each point is
        private float[] _stops;   // how far along the path each place is
        private float _end;
        private ItemPack _drawing;
        private float _nextCheck;
        // The items in the tube, up to MaxInFlight: each one's kind (0 none, else 1 + the index in Packs of the pack it
        // came from), how far along its route it has travelled in metres, and the cell of the port it is heading for.
        private const int MaxInFlight = 6;
        private readonly int[] _flightKind = new int[MaxInFlight];
        private readonly float[] _flightAt = new float[MaxInFlight];
        private readonly Vector3Int[] _flightTo = new Vector3Int[MaxInFlight];
        private readonly ClippedItemPicture[] _flightPictures = new ClippedItemPicture[MaxInFlight];
        private readonly Pose?[] _flightPose = new Pose?[MaxInFlight];   // where each was last shown, to fall out from
        private ComponentEdgeModule _edges;
        private Vector3 _railsIn;
        private float _railsOut;     // metres slid out
        private bool _railsWanted;
        private bool _riding;        // a pack is riding the rails in: they retract, whatever is in the intake
        private float _nextPutOut;
        private float _trustLocalUntil;

        /// True where this client decides: the master, or anywhere with no network. Set by the networking.
        public bool Decides { get; set; } = true;

        /// Puts a leaving pack back into the world, set by the networking: the pack, or null when it could not
        /// (it tries again). Unset, the pack is made here.
        public Func<ItemPack, Vector3, Quaternion, ItemPack> PutOut;

        /// Puts an item that fell out of a cut-off item conduit into the world, set by the networking: the item made, or
        /// null. Unset, it is made here.
        public Func<LoadableItem, Vector3, Quaternion, LoadableItem> DropOut;

        /// Raised on the client whose hand fed a pack: its kind (the index in Packs), its filled places and its
        /// contents.
        public event Action<int, int, int> FedByHand;

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
            _contents = new int[Entries];
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
            foreach (var picture in _flightPictures) picture?.Destroy();
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
                _railsWanted = !_riding && (_drawing != null || TakeableInRange());
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

        // ---- Sending: a stream of items from the pack in the mechanism, along the item conduit from its ItemOut port.

        /// Moves every item in the tube along its route (one whose section has been cut off from this loader falls out
        /// of the tube), and starts another when the tube's mouth is clear: the last
        /// one sent has fully left the Out port and gone on for the send gap, and some port the tree reaches wants
        /// one of the kind the pack in the mechanism holds (its LoadingModule takes that type, holds none, and no item
        /// is already on its way to it). The junctions choose which port (ItemConduits.Choose). It never waits for
        /// an item to arrive. Every client moves the items; only the master sends one and decides it has arrived,
        /// loading it into that LoadingModule. Unpowered, they stop where they are.
        private void Send(float seconds)
        {
            var conduits = ItemConduits.Current;
            bool tubed = conduits != null && _edges != null;
            float mouth = float.MaxValue;   // how far behind it the last item sent has left the Out port
            for (int s = 0; s < MaxInFlight; s++)
            {
                if (_flightKind[s] == 0) { _flightPictures[s]?.Hide(); continue; }
                var item = PackOf(_flightKind[s]) != null ? PackOf(_flightKind[s]).Contains : null;
                var route = tubed && item != null ? conduits.RouteTo(_edges, _flightTo[s]) : null;
                if (route == null)
                {
                    // Its section was cut off from this loader: it falls out of the tube where it was, as a real item.
                    _flightPictures[s]?.Hide();
                    if (!Decides) continue;
                    if (item != null && _flightPose[s] is Pose at)
                    {
                        if (DropOut != null) DropOut(item, at.position, at.rotation);
                        else Instantiate(item, at.position, at.rotation);
                    }
                    _flightKind[s] = 0;
                    _flightPose[s] = null;
                    continue;
                }
                var picture = FlightPicture(s, item);
                float total = route.Length(picture.Length);
                if (Working) _flightAt[s] = Mathf.Min(total, _flightAt[s] + conduits.ItemSpeed * seconds);
                route.Pose(_flightAt[s], picture.Length, out var centre, out var turn, out var keepA, out var keepB);
                picture.Show(centre, turn, keepA, keepB);
                _flightPose[s] = new Pose(centre, turn);
                mouth = Mathf.Min(mouth, _flightAt[s] - picture.Length);
                if (Decides && _flightAt[s] >= total) Arrive(s, route, item);
            }
            if (!Decides || !Working || _ejecting || !tubed) return;

            int free = Array.IndexOf(_flightKind, 0);
            if (free < 0 || mouth < conduits.ItemSpeed * _sendGapSeconds) return;
            int engaged = Engaged();
            if (engaged < 0 || _filled[engaged] == 0) return;
            var pack = PackOf(_kind[engaged]);
            if (pack == null || pack.Contains == null) return;
            var chosen = conduits.Choose(_edges, way => Wanting(way, pack.Contains) != null && !Heading(way.ReceiverCell));
            if (chosen == null) return;
            int top = 31;
            while (top > 0 && (_filled[engaged] & (1 << top)) == 0) top--;
            _filled[engaged] &= ~(1 << top);
            _flightKind[free] = _kind[engaged];
            _flightAt[free] = 0f;
            _flightPose[free] = null;
            _flightTo[free] = chosen.ReceiverCell;
        }

        /// True while an item in the tube is on its way to the port in this cell.
        private bool Heading(Vector3Int cell)
        {
            for (int s = 0; s < MaxInFlight; s++)
                if (_flightKind[s] != 0 && _flightTo[s] == cell) return true;
            return false;
        }

        /// The master: the item is inside the receiver, and is loaded into the LoadingModule that wants it.
        private void Arrive(int slot, ItemConduits.Route route, LoadableItem item)
        {
            var loading = Wanting(route, item);
            if (loading != null) loading.Load();
            else Debug.LogWarning($"Loader: '{name}' sent '{item.name}' to '{route.Receiver.name}', which no longer wants it; it is lost", this);
            _flightKind[slot] = 0;
            _flightPose[slot] = null;
            _flightPictures[slot]?.Hide();
        }

        /// The receiver's LoadingModule fed from the port the route arrives at that would take this item now, or null.
        private static LoadingModule Wanting(ItemConduits.Route route, LoadableItem item)
        {
            if (route.Receiver == null) return null;
            foreach (var loading in route.Receiver.GetComponents<LoadingModule>())
                if (loading.FedFrom(route.Receiver, route.ReceiverCell) && loading.Accepts(item) && !loading.HasItem) return loading;
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

        private ClippedItemPicture FlightPicture(int slot, LoadableItem item)
        {
            var picture = _flightPictures[slot];
            if (picture != null && picture.Prefab == item) return picture;
            picture?.Destroy();
            return _flightPictures[slot] = new ClippedItemPicture(item, $"{item.name} (sent by {name})");
        }

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
            var box = IntakeBox();
            int count = Physics.OverlapBoxNonAlloc(_intake.TransformPoint(box.center), box.extents, Found, _intake.rotation, ~0, QueryTriggerInteraction.Ignore);
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
                    pack.Contents = _contents[i];
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
        public bool Add(int kind, int filled, int contents = 0)
        {
            Ready();
            if (PackOf(kind) == null) return false;
            for (int i = 0; i < Entries; i++)
            {
                if (_kind[i] != 0) continue;
                _kind[i] = kind;
                _filled[i] = filled;
                _contents[i] = contents;
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

        // ---- Intake: a pack a hand on this client lets go of is drawn in, when there is room, once any part of it is in
        // the box: the same test that sends the rails out, so if the rails come out for it, letting go takes it.

        /// Watches every pack a hand on this client lets go of: only that client draws it in, as it holds the pack.
        private void OnPackLetGo(ItemPack pack)
        {
            if (pack == null) return;
            _waiting[pack] = Time.time;
            if (InIntake(pack)) TryTakeIn(pack);
        }

        /// Takes in a waiting pack that is in the box once there is room. One in the box waits there for as long as it
        /// stays; one outside it is watched for Watch Seconds after it was let go, in case it falls or slides in. A pack
        /// picked up again is forgotten.
        private void CheckWaiting()
        {
            foreach (var pair in _waiting)
                if (pair.Key == null || pair.Key.Grab.isSelected || (!InIntake(pair.Key) && Time.time - pair.Value > WatchSeconds))
                    _gone.Add(pair.Key);
            foreach (var pack in _gone) _waiting.Remove(pack);
            _gone.Clear();
            foreach (var pair in _waiting)
                if (InIntake(pair.Key) && TryTakeIn(pair.Key)) break;
        }

        private bool TryTakeIn(ItemPack pack)
        {
            if (_drawing != null || !CanTakeIn) return false;
            int kind = KindOf(pack);
            if (kind == 0) return false;
            StartCoroutine(DrawIn(pack, kind));
            return true;
        }

        /// True when any part of the pack is in the intake box.
        private bool InIntake(ItemPack pack)
        {
            if (_intake == null) return false;
            var box = IntakeBox();
            int count = Physics.OverlapBoxNonAlloc(_intake.TransformPoint(box.center), box.extents, Found, _intake.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (Found[i] != null && Found[i].GetComponentInParent<ItemPack>() == pack) return true;
            return false;
        }

        /// The way the rails slide, in the world.
        private Vector3 RailsDirection => _rails != null && _rails.parent != null ? _rails.parent.forward : transform.forward;

        /// The intake box in the intake's own space: as set, stretched along the rails to take in the extended rails'
        /// end, Rails Reach out from the path's first point, with half the box's depth to spare beyond it.
        private Bounds IntakeBox()
        {
            var box = new Bounds(Vector3.zero, _intakeSize);
            if (_path.Length == 0 || _path[0] == null) return box;
            var outEnd = _path[0].position + RailsDirection * _railsReach;
            box.Encapsulate(_intake.InverseTransformPoint(outEnd) + _intake.InverseTransformDirection(RailsDirection) * (_intakeSize.z * 0.5f));
            return box;
        }

        /// Lines the pack up on the rails, upright and square, where along them it was put down (no further out than
        /// they reach now); then the rails retract and carry it in, at their own speed, onto the path's first point,
        /// where it is swapped for a picture and removed. The rails' stretch is not a place: it holds nothing and is
        /// never room. A hand taking it back first, or the room going, leaves it be.
        private IEnumerator DrawIn(ItemPack pack, int kind)
        {
            _drawing = pack;
            pack.Grab.enabled = false;
            pack.Body.isKinematic = true;
            var colliders = pack.GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;

            var inner = transform.TransformPoint(_points[0]);
            var dir = RailsDirection;
            float along = Mathf.Clamp(Vector3.Dot(pack.transform.position - inner, dir), 0f, Mathf.Min(_railsReach, _railsOut));

            var fromAt = pack.transform.position;
            var fromTurn = pack.transform.rotation;
            for (float t = 0f; t < 1f && pack != null; t += Time.fixedDeltaTime / Mathf.Max(0.01f, _drawInSeconds))
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                pack.Body.MovePosition(Vector3.Lerp(fromAt, inner + dir * along, k));
                pack.Body.MoveRotation(Quaternion.Slerp(fromTurn, transform.rotation, k));
                yield return new WaitForFixedUpdate();
            }

            // The rails come in at their own speed, and once they reach the pack it rides their end in with them.
            _riding = true;
            _railsWanted = false;
            while (pack != null && along > 0f)
            {
                along = Mathf.Min(along, _railsOut);
                pack.Body.MovePosition(inner + dir * along);
                pack.Body.MoveRotation(transform.rotation);
                yield return new WaitForFixedUpdate();
            }
            _riding = false;
            if (pack != null)
            {
                pack.Body.MovePosition(inner);
                pack.Body.MoveRotation(transform.rotation);
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
            int filled = pack.Filled, contents = pack.Contents;
            Add(kind, filled, contents);
            _trustLocalUntil = Time.time + TrustLocalSeconds;
            FedByHand?.Invoke(kind, filled, contents);
            pack.RemoveForEveryone();
        }

        // ---- State: the eject switch; per item in the tube its kind, how far it has gone, and the cell of the port it
        // is heading for in two halves (each exact as a float); then per entry its kind, filled places, how far along,
        // and its contents in two halves.

        public override int StateCount => base.StateCount + 1 + 4 * MaxInFlight + 5 * Entries;

        public override void WriteState(float[] to, int at)
        {
            Ready();
            base.WriteState(to, at);
            at += base.StateCount;
            to[at++] = _ejecting ? 1f : 0f;
            for (int s = 0; s < MaxInFlight; s++)
            {
                int cell = ItemConduits.PackCell(_flightTo[s]);
                to[at++] = _flightKind[s];
                to[at++] = _flightAt[s];
                to[at++] = cell & 0x7FFF;
                to[at++] = (cell >> 15) & 0x7FFF;
            }
            for (int i = 0; i < Entries; i++)
            {
                to[at++] = _kind[i];
                to[at++] = _filled[i];
                to[at++] = _at[i];
                to[at++] = _contents[i] & 0xFFFF;
                to[at++] = (_contents[i] >> 16) & 0xFFFF;
            }
        }

        public override void ReadState(float[] from, int at)
        {
            Ready();
            base.ReadState(from, at);
            if (Time.time < _trustLocalUntil) return;   // a hand here just changed it; the master has not caught up
            at += base.StateCount;
            _ejecting = from[at++] > 0.5f;
            for (int s = 0; s < MaxInFlight; s++)
            {
                int kind = Mathf.RoundToInt(from[at++]);
                float along = from[at++];
                var cell = ItemConduits.UnpackCell(Mathf.RoundToInt(from[at++]) | (Mathf.RoundToInt(from[at++]) << 15));
                // Close enough is left alone, so an item this client is moving does not jump back and forth.
                if (kind != _flightKind[s] || cell != _flightTo[s] || Mathf.Abs(along - _flightAt[s]) > 0.3f) _flightAt[s] = along;
                _flightKind[s] = kind;
                _flightTo[s] = cell;
            }
            for (int i = 0; i < Entries; i++)
            {
                int kind = Mathf.RoundToInt(from[at++]);
                int filled = Mathf.RoundToInt(from[at++]);
                float along = from[at++];
                _contents[i] = Mathf.RoundToInt(from[at++]) | (Mathf.RoundToInt(from[at++]) << 16);
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
            if (_path.Length > 0 && _path[0] != null)
                Gizmos.DrawLine(_path[0].position, _path[0].position + RailsDirection * _railsReach);   // the rails' stretch
            var box = IntakeBox();
            Gizmos.matrix = _intake.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
