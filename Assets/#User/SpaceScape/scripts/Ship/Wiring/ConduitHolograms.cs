using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Opens a conduit while a hand is inside its cell. The first hand in holds it open; the hologram
    /// closes when that hand leaves. The other hand is the one that configures it.
    public sealed class ConduitHolograms : MonoBehaviour
    {
        private const int MaxHands = 8;

        [Header("Parts")]
        [Tooltip("One segment's hologram, pointing up (+Y) from the conduit's centre. Spawned once per segment.")]
        [SerializeField] private SegmentHologram _segmentPrefab;
        [Tooltip("Marks the conduit's centre.")]
        [SerializeField] private GameObject _centrePrefab;
        [Tooltip("The addon slot: applies to this tile only.")]
        [SerializeField] private GameObject _addonSlotPrefab;
        [Tooltip("The upgrade slot: applies to the whole branch.")]
        [SerializeField] private GameObject _upgradeSlotPrefab;

        [Header("Layout")]
        [Tooltip("Height of the addon slot above the conduit's centre, in metres at scale 1.")]
        [SerializeField] private float _addonHeight = 0.07f;
        [Tooltip("Height of the upgrade slot above the conduit's centre, in metres at scale 1.")]
        [SerializeField] private float _upgradeHeight = 0.15f;

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

        private readonly Dictionary<Vector3Int, GridNode> _conduits = new Dictionary<Vector3Int, GridNode>();
        private readonly Vector3[] _hands = new Vector3[MaxHands];
        private ConduitHologram _hologram;
        private int _holder = -1;

        /// The hand holding a conduit open, by its index in PlayerHands, or -1 when none is.
        public int Holder => _holder;

        /// The conduit held open, or null.
        public GridNode Open => _hologram != null && _hologram.IsOpen ? _hologram.Tile : null;

        private void Start()
        {
            foreach (var tile in FindObjectsByType<GridNode>(FindObjectsSortMode.None))
            {
                if (!tile.TryGetComponent<ConduitBehaviourModule>(out _)) continue;
                foreach (var cell in tile.Cells) _conduits[cell] = tile;
            }

            _hologram = ConduitHologram.Create(_segmentPrefab, _centrePrefab, _addonSlotPrefab, _upgradeSlotPrefab,
                _addonHeight, _upgradeHeight, new ConduitHologram.Motion
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
                });
        }

        private void Update()
        {
            if (_hologram == null) return;
            int count = PlayerHands.Positions(_hands);

            if (_holder >= 0 && _holder < count && ConduitAt(_hands[_holder]) == _hologram.Tile) return;

            _holder = -1;
            for (int i = 0; i < count; i++)
            {
                var conduit = ConduitAt(_hands[i]);
                if (conduit == null) continue;
                _holder = i;
                _hologram.Show(conduit);
                return;
            }
            _hologram.Close();
        }

        /// The conduit whose cell a point is in, or null.
        private GridNode ConduitAt(Vector3 point)
        {
            var cell = Vector3Int.RoundToInt(point);
            return _conduits.TryGetValue(cell, out var tile) ? tile : null;
        }
    }
}
