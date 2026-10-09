using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Grows, turns and cuts item conduits by hand (loading.md → Item conduits), as a conduit's hologram wires it.
    /// A hand in a run's tip, or in a run's last cell, opens a small hologram there: a fixed handle on the face the
    /// run comes in by, and a handle a hand grabs. In a tip it starts in the middle: moved onto a face and let go,
    /// the run grows into this cell and leaves by that face. In the last cell it sits on the face the run leaves
    /// by: moved onto another face it turns the run that way, moved into the middle it cuts the cell off.
    ///
    /// Which cell each player has open is told to everyone (ShipNetwork), as a conduit's hologram is, so a cell
    /// open elsewhere does not open here.
    public sealed class ItemConduitHolograms : MonoBehaviour
    {
        private const int MaxHands = 8;

        [Tooltip("One segment's hologram, pointing up (+Y) from the centre: the same prefab conduits use.")]
        [SerializeField] private SegmentHologram _segmentPrefab;
        [Tooltip("Marks the cell's centre.")]
        [SerializeField] private GameObject _centrePrefab;
        [Tooltip("Seconds the hologram takes to open, and to close again.")]
        [SerializeField] private float _openSeconds = 0.3f;
        [Tooltip("Degrees either side of a face's centre within which the held handle snaps to that face.")]
        [SerializeField, Range(1f, 44f)] private float _zoneDegrees = 15f;
        [Tooltip("How close to the centre the held handle must come to cut the cell off, in metres at scale 1.")]
        [SerializeField] private float _parkRadius = 0.12f;
        [Tooltip("How much bigger the handle grows while a hand is in reach of it: 0.25 is 25%.")]
        [SerializeField] private float _hoverGrow = 0.25f;
        [Tooltip("Strength of the buzz while the held handle is somewhere new, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float _buzzAmplitude = 0.3f;
        [Tooltip("Length of each buzz pulse, in seconds.")]
        [SerializeField] private float _buzzSeconds = 0.05f;

        private static readonly GridDirection[] AllFaces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        private readonly Vector3[] _hands = new Vector3[MaxHands];
        private readonly Dictionary<Vector3Int, HashSet<string>> _openElsewhere = new Dictionary<Vector3Int, HashSet<string>>();
        private Transform _root;
        private Transform _centre;
        private SegmentHologram _entry;
        private SegmentHologram _exit;
        private bool _hasCell;
        private Vector3Int _cell;
        private bool _isTip;                // a tip, or a run's last cell
        private GridDirection _entryFace;
        private GridDirection _exitFace;    // None while the handle is in the middle
        private Vector3Int? _shown;
        private int _holder = -1;
        private float _open;
        private bool _opening;
        private IXRSelectInteractor _dragger;
        private GridDirection _target;
        private bool _targetPark;
        private float _nextBuzz;

        /// Raised when this client's hologram opens on a cell.
        public event Action<Vector3Int> Opened;

        /// Raised when this client's hologram closes on a cell.
        public event Action<Vector3Int> Closed;

        /// The cell this client has open, or null.
        public Vector3Int? Open => _shown;

        /// Records that a player has a cell open, or has closed it.
        public void SetOpenElsewhere(Vector3Int cell, string player, bool open)
        {
            if (string.IsNullOrEmpty(player)) return;
            if (!_openElsewhere.TryGetValue(cell, out var who)) _openElsewhere[cell] = who = new HashSet<string>();
            if (open) who.Add(player);
            else who.Remove(player);
        }

        /// Forgets every cell a player had open, for when they leave.
        public void ClearOpenElsewhere(string player)
        {
            foreach (var who in _openElsewhere.Values) who.Remove(player);
        }

        private bool Dragging => _dragger != null;

        private void Start()
        {
            _root = new GameObject("ItemConduitHologram").transform;
            _root.SetParent(transform, false);
            if (_centrePrefab != null) _centre = Instantiate(_centrePrefab, _root, false).transform;
            _entry = MakeSegment("Entry", false);
            _exit = MakeSegment("Exit", true);
            _root.gameObject.SetActive(false);
        }

        private SegmentHologram MakeSegment(string name, bool grabbable)
        {
            if (_segmentPrefab == null) return null;
            var segment = Instantiate(_segmentPrefab, _root, false);
            segment.name = name;
            segment.HasArrow = false;
            if (segment.ArrowGrab != null) segment.ArrowGrab.gameObject.SetActive(false);
            if (segment.TipGrab == null) return segment;
            if (!grabbable) segment.TipGrab.enabled = false;
            else
            {
                segment.TipGrab.selectEntered.AddListener(OnGrabbed);
                segment.TipGrab.selectExited.AddListener(OnLetGo);
            }
            return segment;
        }

        private void Update()
        {
            var conduits = ItemConduits.Current;
            if (conduits == null || _root == null) return;
            int count = PlayerHands.Positions(_hands);

            bool keep = _hasCell && _opening && (Dragging ||
                        (_holder >= 0 && _holder < count && GridCell.ToCell(_hands[_holder]) == _cell)) && !OpenElsewhere(_cell);
            if (!keep)
            {
                _holder = -1;
                for (int i = 0; i < count; i++)
                {
                    var cell = GridCell.ToCell(_hands[i]);
                    if (!Editable(conduits, cell) || OpenElsewhere(cell) || AccessPanel.Blocks(cell)) continue;
                    _holder = i;
                    Show(conduits, cell);
                    break;
                }
                if (_holder < 0) Close();
            }
            Animate();
            if (Dragging) Drag();
        }

        private static bool Editable(ItemConduits conduits, Vector3Int cell) =>
            conduits.TipAt(cell, out _) || conduits.RunEndingAt(cell) >= 0;

        private bool OpenElsewhere(Vector3Int cell) => _openElsewhere.TryGetValue(cell, out var who) && who.Count > 0;

        /// Opens on a cell, or lays it out again when it changed under the hologram.
        private void Show(ItemConduits conduits, Vector3Int cell)
        {
            bool isTip = conduits.TipAt(cell, out var tip);
            var entry = isTip ? tip.Entry : GridDirection.None;
            var exit = GridDirection.None;
            if (!isTip)
            {
                var run = conduits.Runs[conduits.RunEndingAt(cell)];
                int last = run.Exits.Count - 1;
                entry = ItemConduits.EntryOf(run, last);
                exit = run.Exits[last];
            }
            bool fresh = !_hasCell || cell != _cell;
            if (fresh || isTip != _isTip || entry != _entryFace || (!Dragging && exit != _exitFace))
            {
                if (fresh) _open = 0f;
                _hasCell = true;
                _cell = cell;
                _isTip = isTip;
                _entryFace = entry;
                if (!Dragging) _exitFace = exit;
                Layout();
            }
            _opening = true;
            _root.gameObject.SetActive(true);
            Shown(cell);
        }

        private void Close()
        {
            if (Dragging) CancelDrag();
            _opening = false;
            Shown(null);
        }

        private void Shown(Vector3Int? cell)
        {
            if (cell == _shown) return;
            if (_shown.HasValue) Closed?.Invoke(_shown.Value);
            _shown = cell;
            if (cell.HasValue) Opened?.Invoke(cell.Value);
        }

        private void Layout()
        {
            _root.SetPositionAndRotation(GridCell.ToWorld(_cell), Quaternion.identity);
            _root.localScale = Vector3.one * GridCell.Size;
            if (_entry != null) _entry.PlaceOnFace(_entryFace.Vector(), Vector3.up, false);
            ApplyExit();
        }

        private void ApplyExit()
        {
            if (_exit == null) return;
            if (_exitFace == GridDirection.None) _exit.Park();
            else _exit.PlaceOnFace(_exitFace.Vector(), Vector3.up, true);
        }

        private void Animate()
        {
            if (!_hasCell) return;
            _open = Mathf.MoveTowards(_open, _opening ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, _openSeconds));
            if (!_opening && _open <= 0f)
            {
                _hasCell = false;
                _root.gameObject.SetActive(false);
                return;
            }
            float eased = Mathf.SmoothStep(0f, 1f, _open);
            if (_entry != null) _entry.Pose(eased);
            if (_exit != null) _exit.Pose(eased, _hoverGrow, 0.12f);
            if (_centre != null) _centre.localScale = Vector3.one * (1f + (_targetPark && Dragging ? 1f : 0f));
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (!_hasCell || !_opening || Dragging) return;
            _dragger = args.interactorObject;
            _target = GridDirection.None;
            _targetPark = false;
            _exit.TipHeld = true;
        }

        /// Lets go of the handle: grows, turns or cuts the run as it was left, or puts it back.
        private void OnLetGo(SelectExitEventArgs args)
        {
            if (args.interactorObject != _dragger) return;
            var conduits = ItemConduits.Current;
            Vector3 letGo = _exit.Tip.position;
            var cell = _cell;
            var entry = _entryFace;
            bool changed = false;
            if (conduits != null)
            {
                if (_isTip && !_targetPark && _target != GridDirection.None)
                    changed = conduits.EditByHand(ItemConduits.Edit.Grow, cell, entry, _target);
                else if (!_isTip && _targetPark)
                    changed = conduits.EditByHand(ItemConduits.Edit.Cut, cell, entry, GridDirection.None);
                else if (!_isTip && _target != GridDirection.None && _target != _exitFace)
                    changed = conduits.EditByHand(ItemConduits.Edit.Aim, cell, entry, _target);
            }
            _dragger = null;
            if (changed && conduits != null)
            {
                // The cell is now a run's last cell, or a tip again: lay it out as it stands.
                bool isTip = conduits.TipAt(cell, out _);
                _isTip = isTip;
                _exitFace = isTip ? GridDirection.None : _targetPark ? GridDirection.None : _target;
            }
            ApplyExit();
            _exit.SlideHome(letGo, _openSeconds);
        }

        private void CancelDrag()
        {
            _dragger = null;
            if (_exit == null) return;
            _exit.TipHeld = false;
            ApplyExit();
        }

        /// Moves the held handle with the hand inside the cell, noting which face it is over, or the middle.
        private void Drag()
        {
            var attach = _dragger.GetAttachTransform(_exit.TipGrab as IXRSelectInteractable);
            Vector3 hand = attach != null ? attach.position : (_dragger as Component)?.transform.position ?? _root.position;
            Vector3 local = _root.InverseTransformPoint(hand);
            float limit = _exit.FaceDistance;
            local = new Vector3(Mathf.Clamp(local.x, -limit, limit), Mathf.Clamp(local.y, -limit, limit), Mathf.Clamp(local.z, -limit, limit));
            _exit.Tip.position = _root.TransformPoint(local);

            float distance = local.magnitude;
            Vector3 direction = distance > 1e-5f ? local / distance : Vector3.up;
            var was = _targetPark ? GridDirection.None : _target;
            bool wasPark = _targetPark;
            _targetPark = !_isTip && distance < _parkRadius;
            _target = GridDirection.None;
            if (!_targetPark)
                foreach (var face in AllFaces)
                    if (face != _entryFace && Vector3.Angle(direction, face.Vector()) <= _zoneDegrees) { _target = face; break; }

            bool somewhereNew = (_targetPark && !wasPark) || (_target != GridDirection.None && _target != was && _target != _exitFace);
            if (somewhereNew && Time.time >= _nextBuzz)
            {
                Haptics.Pulse(_dragger, _buzzAmplitude, _buzzSeconds);
                _nextBuzz = Time.time + _buzzSeconds * 0.8f;
            }
        }
    }
}
