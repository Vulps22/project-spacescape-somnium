using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Random = UnityEngine.Random;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The ghost of one open conduit, assembled at runtime around the conduit's centre: a segment hologram
    /// per segment, a centre marker, and the addon and upgrade slots above it facing the player. Opens and
    /// closes with an animation; closing plays it backwards and then hides. The configuring hand moves
    /// segments between faces and sets their flow, as a preview the conduit does not take yet.
    public sealed class ConduitHologram : MonoBehaviour
    {
        /// Segments a plain conduit has. A junction addon will raise this.
        public const int PlainSegments = 2;

        /// How the hologram moves and responds, handed over by ConduitHolograms.
        public struct Motion
        {
            public float OpenSeconds;
            public float SlotSpiralDegrees;
            public float SlotSpiralRadius;
            public float SlotStartScale;
            public float CentreJitter;
            public float CentreFlicker;
            public float HoverGrow;
            public float HoverRadius;
            public float HoverSeconds;
            public AudioClip Ambience;
            public float AmbienceVolume;
            public float AmbienceMaxDistance;
            public float ZoneDegrees;
            public float ParkRadius;
            public float ArrowSlide;
            public float BuzzAmplitude;
            public float BuzzSeconds;
            public float ParkGrow;
            public float AddonItemFill;
            public Func<Vector3Int, GridNode> TileAt;
            public Action<GridNode, IReadOnlyList<GridDirection>, IReadOnlyList<bool>> Commit;
            public Action<GridNode, GridDirection, int, bool> CommitAddon;
            public Action<GridNode> Removed;
        }

        private enum Drag { None, Tip, Arrow, Addon }

        private const int MaxHands = 8;

        private static readonly GridDirection[] Faces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        private readonly List<SegmentHologram> _segments = new List<SegmentHologram>();
        private readonly List<GridDirection> _faceOf = new List<GridDirection>();
        private readonly List<bool> _outward = new List<bool>();
        private readonly Vector3[] _hands = new Vector3[MaxHands];
        private readonly Transform[] _slotItems = new Transform[2];
        private readonly Vector3[] _slotHomes = new Vector3[2];
        private readonly float[] _hover = new float[2];
        private SegmentHologram _segmentPrefab;
        private Transform _slots;
        private Transform _centre;
        private Renderer[] _centreRenderers;
        private AudioSource _ambience;
        private Motion _motion;
        private int _segmentCount;
        private float _open;
        private bool _opening;

        private Drag _drag;
        private int _dragged = -1;
        private IXRSelectInteractor _dragger;
        private GridDirection _target;
        private bool _targetPark;
        private Vector3 _slideStart;
        private bool _slideOutward;
        private float _nextBuzz;
        private float _parkHover;

        private AddonItem _display;
        private AddonItem _displayPrefab;
        private UnityAction<SelectEnterEventArgs> _onDisplayGrabbed;

        private SegmentHologram _addonHandlePrefab;
        private SegmentHologram _addonHandle;
        private bool _addonPresent;
        private bool _hasHandle;
        private GridDirection _addonFace;
        private int _addonTurns;
        private bool _addonParked;
        private int _previewTurns;
        private Quaternion _twistStartHand;
        private Vector3 _twistStartUp;

        /// The conduit the ghost is showing, or null once fully closed.
        public GridNode Tile { get; private set; }

        /// True while it is open or opening, rather than closing or closed.
        public bool IsOpen => _opening && Tile != null;

        /// The hand holding the conduit open, by its index in PlayerHands. Its grabs are ignored.
        public int Holder { get; set; } = -1;

        /// The addon slot, where an addon item is shown and let go of to install it.
        public Transform AddonSlot => _slotItems[0] != null ? _slotItems[0] : _slots;

        /// Builds a closed hologram from its parts. Nothing but the parts' prefabs is needed.
        public static ConduitHologram Create(SegmentHologram segment, SegmentHologram addonHandle, GameObject centre,
            GameObject addonSlot, GameObject upgradeSlot, float addonHeight, float upgradeHeight, float slotSpread, Motion motion)
        {
            var root = new GameObject("ConduitHologram");
            var hologram = root.AddComponent<ConduitHologram>();
            hologram._segmentPrefab = segment;
            hologram._addonHandlePrefab = addonHandle;
            hologram._motion = motion;

            if (centre != null)
            {
                hologram._centre = Instantiate(centre, root.transform, false).transform;
                hologram._centreRenderers = hologram._centre.GetComponentsInChildren<Renderer>(true);
            }

            if (motion.Ambience != null)
            {
                var audio = root.AddComponent<AudioSource>();
                audio.clip = motion.Ambience;
                audio.loop = true;
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.rolloffMode = AudioRolloffMode.Linear;
                audio.minDistance = 0.3f;
                audio.maxDistance = motion.AmbienceMaxDistance;
                audio.volume = 0f;
                hologram._ambience = audio;
            }

            hologram._slots = new GameObject("Slots").transform;
            hologram._slots.SetParent(root.transform, false);
            hologram._slotHomes[0] = new Vector3(-slotSpread, addonHeight, 0f);
            hologram._slotHomes[1] = new Vector3(slotSpread, upgradeHeight, 0f);
            if (addonSlot != null) hologram._slotItems[0] = Instantiate(addonSlot, hologram._slots, false).transform;
            if (upgradeSlot != null) hologram._slotItems[1] = Instantiate(upgradeSlot, hologram._slots, false).transform;

            root.SetActive(false);
            return hologram;
        }

        /// Opens the ghost over a conduit, from wherever it has got to if it was already on this one.
        public void Show(GridNode tile)
        {
            if (tile != Tile)
            {
                CancelDrag();
                RevealLabels(Tile, false);
                Tile = tile;
                RevealLabels(Tile, true);
                _open = 0f;
                Layout(tile);
            }
            _opening = true;
            gameObject.SetActive(true);
            if (_ambience != null && !_ambience.isPlaying)
            {
                _ambience.time = Random.Range(0f, _ambience.clip.length);
                _ambience.Play();
            }
        }

        /// Shows a conduit's debug text while the ghost is open around it, or hides it again.
        private static void RevealLabels(GridNode tile, bool revealed)
        {
            if (tile == null) return;
            foreach (var label in tile.GetComponentsInChildren<DebugLabel>(true)) label.Revealed = revealed;
        }

        /// Lays the ghost out again after the conduit changed under it, such as an addon going in or out.
        public void Refresh()
        {
            if (Tile == null) return;
            CancelDrag();
            Layout(Tile);
        }

        /// Starts closing. Any drag in progress is dropped; the animation plays backwards and the ghost hides.
        public void Close()
        {
            if (_opening) CancelDrag();
            _opening = false;
        }

        /// Works out where every segment belongs from the conduit as it stands: on its face, or parked.
        private void Layout(GridNode tile)
        {
            transform.SetPositionAndRotation(tile.transform.position, Quaternion.identity);
            var size = tile.Size;
            transform.localScale = Vector3.one * (GridCell.Size * Mathf.Min(size.x, Mathf.Min(size.y, size.z)));

            _faceOf.Clear();
            _outward.Clear();
            var edges = tile.Edges;
            foreach (var face in tile.UsedFaces)
            {
                if (_faceOf.Contains(face)) continue;
                _faceOf.Add(face);
                _outward.Add(edges != null && edges.GetFace(face) == FlowDirection.Out);
            }
            int segments = tile.TryGetComponent<ConduitBehaviourModule>(out var conduit) ? conduit.Segments : PlainSegments;
            while (_faceOf.Count < segments) { _faceOf.Add(GridDirection.None); _outward.Add(true); }
            _segmentCount = _faceOf.Count;

            while (_segments.Count < _segmentCount && _segmentPrefab != null) AddSegment();

            for (int i = 0; i < _segments.Count; i++)
            {
                bool exists = i < _segmentCount;
                _segments[i].gameObject.SetActive(exists);
                if (exists) Apply(i);
            }

            tile.TryGetComponent<AddonModule>(out var slot);
            var addon = slot != null ? slot.Addon : null;
            _addonPresent = addon != null;
            _hasHandle = _addonPresent && addon.Repositionable && _addonHandlePrefab != null;
            if (_addonPresent)
            {
                _addonFace = slot.Facing;
                _addonTurns = slot.Turns;
                _addonParked = slot.Parked;
            }
            if (_hasHandle && _addonHandle == null) AddAddonHandle();
            if (_addonHandle != null)
            {
                _addonHandle.gameObject.SetActive(_hasHandle);
                if (_hasHandle) ApplyAddon();
            }

            ShowAddon(addon);
        }

        /// Spawns the addon's handle and listens for it being grabbed.
        private void AddAddonHandle()
        {
            _addonHandle = Instantiate(_addonHandlePrefab, transform, false);
            if (_addonHandle.TipGrab == null) return;
            _addonHandle.TipGrab.selectEntered.AddListener(args => Begin(Drag.Addon, -1, args.interactorObject));
            _addonHandle.TipGrab.selectExited.AddListener(args => End(Drag.Addon, -1, args.interactorObject));
        }

        /// Shows the addon's handle where the preview says it is: on its face, or parked in the middle.
        private void ApplyAddon()
        {
            if (_addonParked) _addonHandle.Park();
            else _addonHandle.PlaceOnFace(_addonFace.Vector(), Vector3.up, false);
        }

        /// Stands the handle's capsule along the addon's up, so its twist can be read at a glance.
        private void TwistHandle(GridDirection face, int turns)
        {
            if (_addonHandle == null || _addonHandle.Tip == null || face == GridDirection.None) return;
            _addonHandle.Tip.rotation = Quaternion.LookRotation(face.Vector(), AddonModule.UpFor(face, turns));
        }

        /// Puts the installed addon's item in the addon slot, small enough to fit, for a hand to take out.
        private void ShowAddon(ConduitAddon addon)
        {
            DropDisplay();
            var slot = AddonSlot;
            if (addon == null || addon.Item == null || slot == null) return;

            _displayPrefab = addon.Item;
            var frame = LocalBounds(slot, slot.GetComponentsInChildren<Renderer>());
            _display = Instantiate(_displayPrefab, slot, false);
            _display.Hold();
            var t = _display.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.Euler(0f, 180f, 0f);     // its face toward the player, as the slot faces them
            t.localScale = Vector3.one;

            // Fitted to the frame's face and centred in it, measured by its solid parts: its label is
            // text that can sit well off to one side.
            var solid = new List<Renderer>();
            foreach (var r in _display.GetComponentsInChildren<Renderer>())
                if (r.GetComponent<TMPro.TMP_Text>() == null) solid.Add(r);
            var item = LocalBounds(slot, solid);
            float across = Mathf.Max(item.size.x, item.size.y);
            if (frame.size.sqrMagnitude > 0f && across > 1e-6f)
            {
                float scale = _motion.AddonItemFill * Mathf.Min(frame.size.x, frame.size.y) / across;
                t.localScale = Vector3.one * scale;
                t.localPosition = frame.center - item.center * scale;
            }

            if (_onDisplayGrabbed == null) _onDisplayGrabbed = OnDisplayGrabbed;
            _display.Grab.selectEntered.AddListener(_onDisplayGrabbed);
        }

        /// The box a set of renderers fills, in a transform's own space.
        private static Bounds LocalBounds(Transform space, IEnumerable<Renderer> renderers)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (var r in renderers)
            {
                var local = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var point = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        /// Takes the addon off the conduit as its item is lifted out of the slot, full size in the hand.
        private void OnDisplayGrabbed(SelectEnterEventArgs args)
        {
            var item = _display;
            var prefab = _displayPrefab;
            if (item == null) return;
            item.Grab.selectEntered.RemoveListener(_onDisplayGrabbed);
            _display = null;
            _displayPrefab = null;

            item.transform.SetParent(null, true);
            item.transform.localScale = prefab.transform.localScale;
            if (Tile != null && Tile.TryGetComponent<AddonModule>(out var module) && module.Remove() != null)
                _motion.Removed?.Invoke(Tile);
            Haptics.Pulse(args.interactorObject, _motion.BuzzAmplitude, _motion.BuzzSeconds);
            Refresh();
        }

        /// Clears the item out of the slot, unless a hand has already taken it.
        private void DropDisplay()
        {
            if (_display == null) return;
            _display.Grab.selectEntered.RemoveListener(_onDisplayGrabbed);
            Destroy(_display.gameObject);
            _display = null;
            _displayPrefab = null;
        }

        /// Spawns one more segment and listens for its handle and arrow being grabbed.
        private void AddSegment()
        {
            int index = _segments.Count;
            var segment = Instantiate(_segmentPrefab, transform, false);
            _segments.Add(segment);
            if (segment.TipGrab != null)
            {
                segment.TipGrab.selectEntered.AddListener(args => Begin(Drag.Tip, index, args.interactorObject));
                segment.TipGrab.selectExited.AddListener(args => End(Drag.Tip, index, args.interactorObject));
            }
            if (segment.ArrowGrab != null)
            {
                segment.ArrowGrab.selectEntered.AddListener(args => Begin(Drag.Arrow, index, args.interactorObject));
                segment.ArrowGrab.selectExited.AddListener(args => End(Drag.Arrow, index, args.interactorObject));
            }
        }

        /// Shows a segment where the preview says it is: on its face with its flow, or parked.
        private void Apply(int i)
        {
            if (_faceOf[i] == GridDirection.None) { _segments[i].Park(); return; }
            Vector3 direction = _faceOf[i].Vector();
            _segments[i].PlaceOnFace(direction, Above(direction), _outward[i]);
        }

        private void Update()
        {
            if (Tile == null) return;
            float step = _motion.OpenSeconds > 0f ? Time.deltaTime / _motion.OpenSeconds : 1f;
            _open = Mathf.MoveTowards(_open, _opening ? 1f : 0f, step);
            if (!_opening && _open <= 0f)
            {
                RevealLabels(Tile, false);
                Tile = null;
                DropDisplay();
                if (_ambience != null) _ambience.Stop();
                gameObject.SetActive(false);
                return;
            }

            float eased = Mathf.SmoothStep(0f, 1f, _open);
            for (int i = 0; i < _segmentCount && i < _segments.Count; i++) _segments[i].Pose(eased);
            if (_hasHandle)
            {
                _addonHandle.Pose(eased);
                if (_drag != Drag.Addon) TwistHandle(_addonFace, _addonTurns);
            }
            PoseCentre(eased);
            PoseSlots(eased);
            if (_ambience != null) _ambience.volume = _motion.AmbienceVolume * eased;

            if (_drag == Drag.Tip || _drag == Drag.Addon) DragTip();
            else if (_drag == Drag.Arrow) DragArrow();
        }

        /// Starts a drag, unless the hand is the one holding the conduit open or something is already being dragged.
        private void Begin(Drag kind, int index, IXRSelectInteractor interactor)
        {
            if (!IsOpen || _drag != Drag.None || index >= _segmentCount) return;
            if (HandOf(interactor) == Holder) return;
            if (kind == Drag.Arrow && _faceOf[index] == GridDirection.None) return;

            _drag = kind;
            _dragged = index;
            _dragger = interactor;
            _target = GridDirection.None;
            _targetPark = false;
            if (kind == Drag.Tip) { _segments[index].TipHeld = true; return; }
            if (kind == Drag.Addon)
            {
                _addonHandle.TipHeld = true;
                _twistStartHand = AttachRotation();
                _twistStartUp = AddonModule.UpFor(_addonFace, _addonTurns);
                _previewTurns = _addonTurns;
                return;
            }

            _slideStart = Attach();
            _slideOutward = _outward[index];
        }

        /// Ends a drag: a handle lands on the face or in the middle it was held over, or goes back where it
        /// was; an arrow keeps the flow it was slid to.
        private void End(Drag kind, int index, IXRSelectInteractor interactor)
        {
            if (_drag != kind || _dragged != index || interactor != _dragger) return;

            bool changed = false;
            if (kind == Drag.Addon)
            {
                var face = !_targetPark && _target != GridDirection.None ? _target : _addonFace;
                bool parked = _targetPark || (_addonParked && _target == GridDirection.None);
                int turns = parked ? _addonTurns : _previewTurns;
                changed = face != _addonFace || turns != _addonTurns || parked != _addonParked;
                _addonFace = face;
                _addonTurns = turns;
                _addonParked = parked;
                Vector3 letGo = _addonHandle.Tip.position;
                ApplyAddon();
                _addonHandle.SlideHome(letGo, _motion.OpenSeconds);
                ClearDrag();
                if (changed) _motion.CommitAddon?.Invoke(Tile, face, turns, parked);
                return;
            }
            if (kind == Drag.Tip)
            {
                if (_targetPark) { changed = _faceOf[index] != GridDirection.None; _faceOf[index] = GridDirection.None; }
                else if (_target != GridDirection.None && _target != _faceOf[index])
                {
                    if (_faceOf[index] == GridDirection.None) _outward[index] = PairWithNeighbour(_target);
                    _faceOf[index] = _target;
                    changed = true;
                }
                Vector3 letGo = _segments[index].Tip.position;
                Apply(index);
                _segments[index].SlideHome(letGo, _motion.OpenSeconds);
            }
            else
            {
                changed = _outward[index] != _slideOutward;
                _outward[index] = _slideOutward;
                Apply(index);
            }

            ClearDrag();
            if (changed) _motion.Commit?.Invoke(Tile, _faceOf, _outward);
        }

        /// Drops a drag without changing anything.
        private void CancelDrag()
        {
            if (_drag == Drag.None) return;
            if (_drag == Drag.Addon && _addonHandle != null)
            {
                _addonHandle.TipHeld = false;
                ApplyAddon();
            }
            else if (_dragged >= 0 && _dragged < _segments.Count)
            {
                _segments[_dragged].TipHeld = false;
                Apply(_dragged);
            }
            ClearDrag();
        }

        private void ClearDrag()
        {
            _drag = Drag.None;
            _dragged = -1;
            _dragger = null;
        }

        /// Moves the held handle freely with the hand anywhere inside the tile, noting which zone it is in:
        /// a free face, the middle, or neither. The zone is only acted on at release; while it is somewhere
        /// new the hand buzzes, and the middle swells the centre cube too.
        private void DragTip()
        {
            bool addon = _drag == Drag.Addon;
            var segment = addon ? _addonHandle : _segments[_dragged];
            Vector3 local = transform.InverseTransformPoint(Attach());
            float limit = segment.FaceDistance;
            local = new Vector3(Mathf.Clamp(local.x, -limit, limit), Mathf.Clamp(local.y, -limit, limit), Mathf.Clamp(local.z, -limit, limit));
            segment.Tip.position = transform.TransformPoint(local);

            float distance = local.magnitude;
            Vector3 direction = distance > 1e-5f ? local / distance : Vector3.up;
            _targetPark = distance < _motion.ParkRadius;
            _target = GridDirection.None;
            if (!_targetPark)
                foreach (var face in Faces)
                    if (IsFree(face, addon ? -1 : _dragged, addon) && Vector3.Angle(direction, face.Vector()) <= _motion.ZoneDegrees) { _target = face; break; }

            var current = addon ? (_addonParked ? GridDirection.None : _addonFace) : _faceOf[_dragged];
            bool somewhereNew = _targetPark ? current != GridDirection.None
                : _target != GridDirection.None && _target != current;
            if (addon && TwistTo(_targetPark ? GridDirection.None : _target != GridDirection.None ? _target : current))
                somewhereNew = true;
            if (somewhereNew && Time.time >= _nextBuzz)
            {
                Haptics.Pulse(_dragger, _motion.BuzzAmplitude, _motion.BuzzSeconds);
                _nextBuzz = Time.time + _motion.BuzzSeconds * 0.8f;
            }
        }

        /// Works out the held addon's twist from how far the hand has rolled since it took hold, snapped to
        /// quarter turns about the face it would land on, and stands the capsule that way. True on a new quarter.
        private bool TwistTo(GridDirection face)
        {
            if (face == GridDirection.None) return false;
            Vector3 up = AttachRotation() * Quaternion.Inverse(_twistStartHand) * _twistStartUp;
            int turns = AddonModule.NearestTurns(face, up);
            bool changed = turns >= 0 && turns != _previewTurns;
            if (turns >= 0) _previewTurns = turns;
            TwistHandle(face, _previewTurns);
            return changed;
        }

        /// Turns the held arrow outward or inward once it has been slid far enough along its segment, with a tick.
        private void DragArrow()
        {
            Vector3 axis = transform.TransformDirection(_faceOf[_dragged].Vector()).normalized;
            float slid = Vector3.Dot(Attach() - _slideStart, axis);
            if (Mathf.Abs(slid) < _motion.ArrowSlide) return;

            bool outward = slid > 0f;
            if (outward == _slideOutward) return;
            _slideOutward = outward;
            _segments[_dragged].SetOutward(outward);
            Haptics.Pulse(_dragger, _motion.BuzzAmplitude, _motion.BuzzSeconds);
        }

        /// True when no other segment, and no working addon, is on a face.
        private bool IsFree(GridDirection face, int exceptSegment, bool exceptAddon = false)
        {
            for (int i = 0; i < _segmentCount; i++)
                if (i != exceptSegment && _faceOf[i] == face) return false;
            return exceptAddon || !_addonPresent || _addonParked || _addonFace != face;
        }

        /// The flow a segment pulled out onto a face should have: whatever meets the neighbour on that side,
        /// and out when there is no neighbour.
        private bool PairWithNeighbour(GridDirection face)
        {
            var neighbour = _motion.TileAt?.Invoke(Tile.Coordinate + face.Offset());
            var edges = neighbour != null ? neighbour.Edges : null;
            if (edges == null) return true;
            return edges.GetFace(GridNode.Opposite(face)) != FlowDirection.Out;
        }

        /// Where the dragging hand's grip is.
        private Vector3 Attach()
        {
            var attach = _dragger?.GetAttachTransform(Grabbed());
            return attach != null ? attach.position : (_dragger as Component)?.transform.position ?? transform.position;
        }

        /// Which way the dragging hand's grip is turned.
        private Quaternion AttachRotation()
        {
            var attach = _dragger?.GetAttachTransform(Grabbed());
            return attach != null ? attach.rotation : (_dragger as Component)?.transform.rotation ?? Quaternion.identity;
        }

        /// What the dragging hand has hold of.
        private UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable Grabbed()
        {
            if (_drag == Drag.Addon) return _addonHandle.TipGrab;
            return _drag == Drag.Tip ? _segments[_dragged].TipGrab : _segments[_dragged].ArrowGrab;
        }

        /// Which of PlayerHands' hands an interactor belongs to: the one nearest its grip.
        private int HandOf(IXRSelectInteractor interactor)
        {
            var point = (interactor as Component)?.transform.position;
            if (point == null) return -1;
            int count = PlayerHands.Positions(_hands);
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float d = (_hands[i] - point.Value).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        /// Grows the centre cube, jittering and flickering until it has resolved, and swells it while a held
        /// handle is in the middle, where letting go would park it.
        private void PoseCentre(float open)
        {
            if (_centre == null) return;
            float hoverStep = _motion.HoverSeconds > 0f ? Time.deltaTime / _motion.HoverSeconds : 1f;
            bool parking = (_drag == Drag.Tip || _drag == Drag.Addon) && _targetPark;
            _parkHover = Mathf.MoveTowards(_parkHover, parking ? 1f : 0f, hoverStep);

            float unsettled = 1f - open;
            _centre.localScale = Vector3.one * (open * (1f + _motion.ParkGrow * Mathf.SmoothStep(0f, 1f, _parkHover)));
            _centre.localPosition = Random.insideUnitSphere * (_motion.CentreJitter * unsettled);

            bool visible = Random.value >= _motion.CentreFlicker * unsettled;
            foreach (var r in _centreRenderers)
                if (r != null) r.enabled = visible;
        }

        /// Spirals the slots out from the centre as they grow, spinning with the spiral, and swells each one a
        /// hand is inside.
        private void PoseSlots(float open)
        {
            int handCount = PlayerHands.Positions(_hands);
            float hoverStep = _motion.HoverSeconds > 0f ? Time.deltaTime / _motion.HoverSeconds : 1f;

            for (int i = 0; i < _slotItems.Length; i++)
            {
                var slot = _slotItems[i];
                if (slot == null) continue;

                float turn = (1f - open) * _motion.SlotSpiralDegrees + i * 180f;
                float rad = turn * Mathf.Deg2Rad;
                float swing = Mathf.Sin(open * Mathf.PI) * _motion.SlotSpiralRadius;
                slot.localPosition = _slotHomes[i] * open + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * swing;
                slot.localRotation = Quaternion.Euler(0f, 0f, (1f - open) * _motion.SlotSpiralDegrees);

                bool touched = false;
                for (int h = 0; h < handCount && _opening; h++)
                    if ((_hands[h] - slot.position).sqrMagnitude <= _motion.HoverRadius * _motion.HoverRadius) { touched = true; break; }
                _hover[i] = Mathf.MoveTowards(_hover[i], touched ? 1f : 0f, hoverStep);

                float grow = Mathf.Lerp(_motion.SlotStartScale, 1f, open) * (1f + _motion.HoverGrow * Mathf.SmoothStep(0f, 1f, _hover[i]));
                slot.localScale = Vector3.one * grow;
            }
        }

        /// The side a segment's arrow floats on: up for a horizontal segment, toward the player for a vertical one.
        private Vector3 Above(Vector3 direction)
        {
            if (Mathf.Abs(direction.y) < 0.5f) return Vector3.up;
            if (!PlayerHands.TryHead(out var head)) return Vector3.forward;
            Vector3 toHead = head - transform.position;
            toHead.y = 0f;
            return toHead.sqrMagnitude > 1e-4f ? toHead.normalized : Vector3.forward;
        }

        private void LateUpdate()
        {
            if (Tile == null || _slots == null || !PlayerHands.TryHead(out var head)) return;
            Vector3 toHead = head - _slots.position;
            toHead.y = 0f;
            if (toHead.sqrMagnitude > 1e-4f) _slots.rotation = Quaternion.LookRotation(toHead);
        }
    }
}
