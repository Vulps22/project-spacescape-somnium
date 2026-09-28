using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Opens a conduit while a hand is inside its cell. The first hand in holds it open; the hologram
    /// closes when that hand leaves. The other hand is the one that configures it.
    ///
    /// A conduit another player has open is boxed in the handles' material and cannot be opened here.
    /// Who has what open arrives from the network; this only draws it and keeps hands out.
    public sealed class ConduitHolograms : MonoBehaviour
    {
        private const int MaxHands = 8;

        [Header("Parts")]
        [Tooltip("One segment's hologram, pointing up (+Y) from the conduit's centre. Spawned once per segment.")]
        [SerializeField] private SegmentHologram _segmentPrefab;
        [Tooltip("The handle a repositionable addon gets: moved like a segment's handle, twisted with the wrist, and parked in the middle. Carries no power.")]
        [SerializeField] private SegmentHologram _addonHandlePrefab;
        [Tooltip("Marks the conduit's centre.")]
        [SerializeField] private GameObject _centrePrefab;
        [Tooltip("The addon slot: applies to this tile only.")]
        [SerializeField] private GameObject _addonSlotPrefab;
        [Tooltip("The upgrade slot: applies to the whole branch.")]
        [SerializeField] private GameObject _upgradeSlotPrefab;

        [Header("Layout")]
        [Tooltip("Height of the addon slot above the conduit's centre, in metres at scale 1.")]
        [SerializeField] private float _addonHeight = 0.15f;
        [Tooltip("Height of the upgrade slot above the conduit's centre, in metres at scale 1.")]
        [SerializeField] private float _upgradeHeight = 0.15f;
        [Tooltip("How far each slot sits to the side of the conduit's centre, in metres at scale 1: the addon to one side, the upgrade to the other, clear of a vertical cable.")]
        [SerializeField] private float _slotSpread = 0.07f;

        [Header("Opening")]
        [Tooltip("Seconds the hologram takes to open, and to close again.")]
        [SerializeField] private float _openSeconds = 0.4f;
        [Tooltip("Degrees the slots turn through as they spiral out to their places, spinning with it.")]
        [SerializeField] private float _slotSpiralDegrees = 540f;
        [Tooltip("How wide the slots' spiral swings at its widest, in metres at scale 1.")]
        [SerializeField] private float _slotSpiralRadius = 0.06f;
        [Tooltip("Size the slots start at, as a share of their full size.")]
        [SerializeField, Range(0f, 1f)] private float _slotStartScale = 0.1f;
        [Tooltip("How far the centre cube shakes while it resolves, in metres. Settles to nothing once open.")]
        [SerializeField] private float _centreJitter = 0.015f;
        [Tooltip("Chance per frame, 0 to 1, that the centre cube drops out while it resolves. Settles to nothing once open.")]
        [SerializeField, Range(0f, 1f)] private float _centreFlicker = 0.5f;

        [Header("Sound")]
        [Tooltip("Looped quietly from the hologram while it is open, fading with the open and close animation. Optional.")]
        [SerializeField] private AudioClip _ambience;
        [Tooltip("Volume of the ambience once fully open, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.25f;
        [Tooltip("Distance, in metres, beyond which the ambience cannot be heard.")]
        [SerializeField] private float _ambienceMaxDistance = 3f;

        [Header("Hover")]
        [Tooltip("How much bigger a slot grows while a hand is inside it: 0.25 is 25%.")]
        [SerializeField] private float _hoverGrow = 0.25f;
        [Tooltip("How close a hand must be to a slot's centre to count as inside it, in metres.")]
        [SerializeField] private float _hoverRadius = 0.05f;
        [Tooltip("Seconds a slot takes to grow or shrink back.")]
        [SerializeField] private float _hoverSeconds = 0.12f;

        [Header("Handles and arrows")]
        [Tooltip("Degrees either side of a face's centre within which a held handle snaps to that face.")]
        [SerializeField, Range(1f, 44f)] private float _zoneDegrees = 15f;
        [Tooltip("How close to the conduit's centre a held handle must come to park its segment, in metres at scale 1.")]
        [SerializeField] private float _parkRadius = 0.12f;
        [Tooltip("How far an arrow must be slid along its segment to turn it round, in metres.")]
        [SerializeField] private float _arrowSlide = 0.04f;
        [Tooltip("Strength of the buzz while a handle is in a new face's zone, and of an arrow's tick, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float _buzzAmplitude = 0.3f;
        [Tooltip("Length of each buzz pulse, in seconds. They repeat for as long as a handle stays in a new zone.")]
        [SerializeField] private float _buzzSeconds = 0.05f;
        [Tooltip("How much bigger the centre cube grows while a held handle is in the middle, where letting go parks it: 1 is double.")]
        [SerializeField] private float _parkGrow = 1f;

        [Header("Addons")]
        [Tooltip("How close to the addon slot's centre an addon item must be let go to install it, in metres.")]
        [SerializeField] private float _installRadius = 0.1f;
        [Tooltip("How much of the addon slot's frame an installed addon's item fills, 0 to 1. It is centred in the frame.")]
        [SerializeField, Range(0.1f, 1f)] private float _addonItemFill = 0.85f;

        [Header("Unlock")]
        [Tooltip("The cube shown at a tile's unlock point, on tiles that need one. Should have no collider.")]
        [SerializeField] private GameObject _unlockPrefab;
        [Tooltip("How close a hand must come to an unlock cube's centre to pass through it, in metres.")]
        [SerializeField] private float _unlockRadius = 0.06f;
        [Tooltip("Seconds for one pulse of an unlock cube, from small to large and back.")]
        [SerializeField] private float _unlockPulseSeconds = 1.2f;
        [Tooltip("How much an unlock cube grows at the top of its pulse: 0.2 is 20%.")]
        [SerializeField] private float _unlockPulse = 0.2f;

        [Header("Open elsewhere")]
        [Tooltip("Material of the box drawn round a conduit another player has open: the handles' material.")]
        [SerializeField] private Material _openElsewhereMaterial;
        [Tooltip("How far the box stands off the conduit on every side, in metres.")]
        [SerializeField] private float _openElsewherePadding = 0.01f;

        /// Raised when this client's hologram opens on a conduit.
        public event Action<GridNode> Opened;

        /// Raised when this client's hologram closes on a conduit.
        public event Action<GridNode> Closed;

        /// Raised when a hand on this client has changed a conduit: rewired it, or installed, removed, moved,
        /// twisted or parked its addon.
        public event Action<GridNode> Edited;

        private readonly Dictionary<GridNode, HashSet<string>> _openElsewhere = new Dictionary<GridNode, HashSet<string>>();
        private readonly Dictionary<GridNode, Transform> _boxes = new Dictionary<GridNode, Transform>();
        private GridNode _shown;

        private readonly Dictionary<Vector3Int, GridNode> _conduits = new Dictionary<Vector3Int, GridNode>();
        private readonly Dictionary<Vector3Int, GridNode> _tiles = new Dictionary<Vector3Int, GridNode>();
        private readonly Vector3[] _hands = new Vector3[MaxHands];
        private readonly List<Lock> _locks = new List<Lock>();
        private ConduitHologram _hologram;
        private GridNode _unlocked;
        private PowerGrid _grid;

        private static readonly GridDirection[] AllFaces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };
        private int _holder = -1;

        private struct Lock
        {
            public GridNode Tile;
            public Transform Point;
            public Transform Cube;
            public Vector3 Scale;
        }

        /// The hand holding a conduit open, by its index in PlayerHands, or -1 when none is.
        public int Holder => _holder;

        /// The conduit held open, or null.
        public GridNode Open => _hologram != null && _hologram.IsOpen ? _hologram.Tile : null;

        private void Start()
        {
            _grid = FindFirstObjectByType<PowerGrid>();
            foreach (var tile in FindObjectsByType<GridNode>(FindObjectsSortMode.None))
            {
                bool conduit = tile.TryGetComponent<ConduitBehaviourModule>(out var module);
                foreach (var cell in tile.Cells)
                {
                    _tiles[cell] = tile;
                    if (conduit) _conduits[cell] = tile;
                }
                if (conduit && tile.TryGetComponent<AddonModule>(out var slot))
                {
                    var owner = tile;
                    slot.Changed += () => RefreshLock(owner);
                }
                if (conduit) RefreshLock(tile);
            }

            _hologram = ConduitHologram.Create(_segmentPrefab, _addonHandlePrefab, _centrePrefab, _addonSlotPrefab, _upgradeSlotPrefab,
                _addonHeight, _upgradeHeight, _slotSpread, new ConduitHologram.Motion
                {
                    OpenSeconds = _openSeconds,
                    SlotSpiralDegrees = _slotSpiralDegrees,
                    SlotSpiralRadius = _slotSpiralRadius,
                    SlotStartScale = _slotStartScale,
                    CentreJitter = _centreJitter,
                    CentreFlicker = _centreFlicker,
                    HoverGrow = _hoverGrow,
                    HoverRadius = _hoverRadius,
                    HoverSeconds = _hoverSeconds,
                    Ambience = _ambience,
                    AmbienceVolume = _ambienceVolume,
                    AmbienceMaxDistance = _ambienceMaxDistance,
                    ZoneDegrees = _zoneDegrees,
                    ParkRadius = _parkRadius,
                    ArrowSlide = _arrowSlide,
                    BuzzAmplitude = _buzzAmplitude,
                    BuzzSeconds = _buzzSeconds,
                    ParkGrow = _parkGrow,
                    AddonItemFill = _addonItemFill,
                    TileAt = cell => _tiles.TryGetValue(cell, out var found) ? found : null,
                    Commit = Commit,
                    CommitAddon = CommitAddon,
                    Removed = tile => Edited?.Invoke(tile),
                });
        }

        private void OnEnable() => AddonItem.Released += Install;

        private void OnDisable() => AddonItem.Released -= Install;

        /// Installs an addon item let go of in the open conduit's addon slot, when that slot is empty.
        private void Install(AddonItem item)
        {
            if (_hologram == null || !_hologram.IsOpen || item == null || item.Installs == null) return;
            var slot = _hologram.AddonSlot;
            if (slot == null || (item.transform.position - slot.position).sqrMagnitude > _installRadius * _installRadius) return;
            var tile = _hologram.Tile;
            if (!tile.TryGetComponent<AddonModule>(out var module) || !module.Install(item.Installs, FaceTowardPlayer(tile))) return;

            Destroy(item.gameObject);
            _hologram.Refresh();
            Edited?.Invoke(tile);
        }

        private void Update()
        {
            FitBoxes();
            if (_hologram == null) return;
            int count = PlayerHands.Positions(_hands);
            UpdateLocks(count);

            if (_holder >= 0 && _holder < count && _hologram.IsOpen && ConduitAt(_hands[_holder]) == _hologram.Tile
                && !IsOpenElsewhere(_hologram.Tile)) return;

            _holder = -1;
            for (int i = 0; i < count; i++)
            {
                var conduit = ConduitAt(_hands[i]);
                if (conduit == null || IsOpenElsewhere(conduit) || (IsLocked(conduit) && conduit != _unlocked)) continue;
                _holder = i;
                _hologram.Holder = i;
                _hologram.Show(conduit);
                Shown(conduit);
                return;
            }
            _hologram.Holder = -1;
            _hologram.Close();
            Shown(null);
        }

        /// Raises Closed and Opened as the conduit shown here changes.
        private void Shown(GridNode tile)
        {
            if (tile == _shown) return;
            var was = _shown;
            _shown = tile;
            if (was != null) Closed?.Invoke(was);
            if (tile != null) Opened?.Invoke(tile);
        }

        /// True while another player has this conduit open.
        public bool IsOpenElsewhere(GridNode tile) =>
            tile != null && _openElsewhere.TryGetValue(tile, out var who) && who.Count > 0;

        /// Records that a player has a conduit open, or has closed it, and boxes it while anyone has.
        public void SetOpenElsewhere(GridNode tile, string player, bool open)
        {
            if (tile == null || string.IsNullOrEmpty(player)) return;
            if (!_openElsewhere.TryGetValue(tile, out var who)) _openElsewhere[tile] = who = new HashSet<string>();
            if (open) who.Add(player);
            else who.Remove(player);
            ShowBox(tile, who.Count > 0);
        }

        /// Forgets every conduit a player had open, for when they leave.
        public void ClearOpenElsewhere(string player)
        {
            foreach (var pair in _openElsewhere)
                if (pair.Value.Remove(player)) ShowBox(pair.Key, pair.Value.Count > 0);
        }

        private void ShowBox(GridNode tile, bool show)
        {
            if (!show)
            {
                if (_boxes.TryGetValue(tile, out var old) && old != null) Destroy(old.gameObject);
                _boxes.Remove(tile);
                return;
            }
            if (_boxes.ContainsKey(tile)) return;

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = $"Open elsewhere ({tile.name})";
            Destroy(box.GetComponent<Collider>());
            if (_openElsewhereMaterial != null) box.GetComponent<Renderer>().sharedMaterial = _openElsewhereMaterial;
            box.transform.SetParent(transform, false);
            _boxes[tile] = box.transform;
        }

        /// Keeps each box tight round its conduit, which can change shape while someone works on it.
        private void FitBoxes()
        {
            foreach (var pair in _boxes)
            {
                if (pair.Key == null || pair.Value == null) continue;
                bool any = false;
                var bounds = new Bounds(pair.Key.transform.position, Vector3.zero);
                foreach (var r in pair.Key.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r.GetComponent<TMPro.TMP_Text>() != null) continue;
                    if (any) bounds.Encapsulate(r.bounds);
                    else { bounds = r.bounds; any = true; }
                }
                pair.Value.SetPositionAndRotation(bounds.center, Quaternion.identity);
                pair.Value.localScale = bounds.size + Vector3.one * (2f * _openElsewherePadding);
            }
        }

        /// Matches a conduit's lock to the addon on it now: a lock for an addon that asks for one, none otherwise.
        private void RefreshLock(GridNode tile)
        {
            for (int i = _locks.Count - 1; i >= 0; i--)
            {
                if (_locks[i].Tile != tile) continue;
                if (_locks[i].Cube != null) Destroy(_locks[i].Cube.gameObject);
                _locks.RemoveAt(i);
            }
            if (!tile.TryGetComponent<ConduitBehaviourModule>(out var conduit) || !conduit.ShouldBeLocked()) return;
            var point = conduit.UnlockPoint;
            if (point != null) AddLock(tile, point);
        }

        /// Puts an unlock cube at a tile's unlock point.
        private void AddLock(GridNode tile, Transform point)
        {
            Transform cube = null;
            if (_unlockPrefab != null) cube = Instantiate(_unlockPrefab, point, false).transform;
            _locks.Add(new Lock { Tile = tile, Point = point, Cube = cube, Scale = cube != null ? cube.localScale : Vector3.one });
        }

        /// Pulses the unlock cubes, unlocks a tile whose cube a hand passes through, and locks it again once
        /// no hand is left in its cell or its cube.
        private void UpdateLocks(int count)
        {
            float pulse = 1f + _unlockPulse * 0.5f * (1f - Mathf.Cos(Time.time * 2f * Mathf.PI / Mathf.Max(0.01f, _unlockPulseSeconds)));
            var open = Open;
            Transform unlockedPoint = null;

            foreach (var entry in _locks)
            {
                if (entry.Point == null) continue;
                if (entry.Cube != null)
                {
                    entry.Cube.gameObject.SetActive(entry.Tile != open);
                    entry.Cube.localScale = entry.Scale * pulse;
                }
                for (int i = 0; i < count; i++)
                    if (InCube(_hands[i], entry.Point)) _unlocked = entry.Tile;
                if (entry.Tile == _unlocked) unlockedPoint = entry.Point;
            }

            if (_unlocked == null) return;
            for (int i = 0; i < count; i++)
                if (ConduitAt(_hands[i]) == _unlocked || (unlockedPoint != null && InCube(_hands[i], unlockedPoint))) return;
            _unlocked = null;
        }

        /// True when a hand is inside an unlock cube.
        private bool InCube(Vector3 hand, Transform point)
        {
            return (hand - point.position).sqrMagnitude <= _unlockRadius * _unlockRadius;
        }

        /// True when a conduit needs its unlock cube touched before it opens.
        private bool IsLocked(GridNode tile)
        {
            foreach (var entry in _locks)
                if (entry.Tile == tile) return true;
            return false;
        }

        /// The free side of a conduit nearest the player's head, where a newly installed addon faces.
        private static GridDirection FaceTowardPlayer(GridNode tile)
        {
            var used = new HashSet<GridDirection>(tile.UsedFaces);
            Vector3 toHead = PlayerHands.TryHead(out var head) ? head - tile.transform.position : Vector3.forward;
            var best = GridDirection.ZPlus;
            float bestDot = float.MinValue;
            foreach (var face in AllFaces)
            {
                if (used.Contains(face)) continue;
                float dot = Vector3.Dot(toHead, face.Vector());
                if (dot > bestDot) { bestDot = dot; best = face; }
            }
            return best;
        }

        /// Moves, twists, parks or unparks the addon on a conduit, as its hologram was left. Not rewiring: the
        /// conduit's faces stay as they were.
        private void CommitAddon(GridNode tile, GridDirection face, int turns, bool parked)
        {
            if (tile == null || !tile.TryGetComponent<AddonModule>(out var module)) return;
            module.Arrange(face, turns, parked);
            Edited?.Invoke(tile);
        }

        /// Gives a conduit the shape its hologram was left in: its segments' faces In or Out, every other face
        /// None, and the grid rewired around it. Giving a blown conduit an output again repairs it.
        private void Commit(GridNode tile, IReadOnlyList<GridDirection> faces, IReadOnlyList<bool> outward)
        {
            var edges = tile.Edges;
            if (edges == null) return;
            foreach (var face in AllFaces) edges.SetFace(face, FlowDirection.None);
            bool hasOutput = false;
            for (int i = 0; i < faces.Count; i++)
            {
                if (faces[i] == GridDirection.None) continue;
                edges.SetFace(faces[i], outward[i] ? FlowDirection.Out : FlowDirection.In);
                hasOutput |= outward[i];
            }
            if (_grid == null) return;
            if (hasOutput && tile.Node != null && tile.Node.IsPopped) _grid.Repair(tile);
            _grid.Rewire(tile);
            Edited?.Invoke(tile);
        }

        /// The conduit whose cell a point is in, or null.
        private GridNode ConduitAt(Vector3 point)
        {
            var cell = GridCell.ToCell(point);
            return _conduits.TryGetValue(cell, out var tile) ? tile : null;
        }
    }
}
