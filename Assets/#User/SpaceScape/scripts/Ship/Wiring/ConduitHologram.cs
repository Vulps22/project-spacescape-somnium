using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The ghost of one open conduit, assembled at runtime around the conduit's centre: a segment hologram
    /// per segment, a centre marker, and the addon and upgrade slots above it facing the player. Opens and
    /// closes with an animation; closing plays it backwards and then hides.
    public sealed class ConduitHologram : MonoBehaviour
    {
        /// Segments a plain conduit has. A junction addon will raise this.
        public const int PlainSegments = 2;

        /// How the hologram moves, handed over by ConduitHolograms.
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
        }

        private const int MaxHands = 8;

        private readonly List<SegmentHologram> _segments = new List<SegmentHologram>();
        private readonly List<GridDirection> _used = new List<GridDirection>();
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

        /// The conduit the ghost is showing, or null once fully closed.
        public GridNode Tile { get; private set; }

        /// True while it is open or opening, rather than closing or closed.
        public bool IsOpen => _opening && Tile != null;

        /// Builds a closed hologram from its parts. Nothing but the parts' prefabs is needed.
        public static ConduitHologram Create(SegmentHologram segment, GameObject centre, GameObject addonSlot,
            GameObject upgradeSlot, float addonHeight, float upgradeHeight, Motion motion)
        {
            var root = new GameObject("ConduitHologram");
            var hologram = root.AddComponent<ConduitHologram>();
            hologram._segmentPrefab = segment;
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
            hologram._slotHomes[0] = Vector3.up * addonHeight;
            hologram._slotHomes[1] = Vector3.up * upgradeHeight;
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
                Tile = tile;
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

        /// Starts closing. The animation plays backwards and the ghost hides once it is shut.
        public void Close() => _opening = false;

        /// Works out where every segment belongs: one per segment the conduit has, on its face or parked.
        private void Layout(GridNode tile)
        {
            transform.SetPositionAndRotation(tile.transform.position, Quaternion.identity);
            var size = tile.Size;
            transform.localScale = Vector3.one * Mathf.Min(size.x, Mathf.Min(size.y, size.z));

            _used.Clear();
            foreach (var face in tile.UsedFaces) if (!_used.Contains(face)) _used.Add(face);
            _segmentCount = Mathf.Max(PlainSegments, _used.Count);

            while (_segments.Count < _segmentCount && _segmentPrefab != null)
                _segments.Add(Instantiate(_segmentPrefab, transform, false));

            var edges = tile.Edges;
            for (int i = 0; i < _segments.Count; i++)
            {
                bool exists = i < _segmentCount;
                _segments[i].gameObject.SetActive(exists);
                if (!exists) continue;
                if (i < _used.Count)
                {
                    Vector3 direction = _used[i].Vector();
                    _segments[i].PlaceOnFace(direction, Above(direction), edges != null && edges.GetFace(_used[i]) == FlowDirection.Out);
                }
                else _segments[i].Park();
            }
        }

        private void Update()
        {
            if (Tile == null) return;
            float step = _motion.OpenSeconds > 0f ? Time.deltaTime / _motion.OpenSeconds : 1f;
            _open = Mathf.MoveTowards(_open, _opening ? 1f : 0f, step);
            if (!_opening && _open <= 0f)
            {
                Tile = null;
                if (_ambience != null) _ambience.Stop();
                gameObject.SetActive(false);
                return;
            }

            float eased = Mathf.SmoothStep(0f, 1f, _open);
            for (int i = 0; i < _segmentCount && i < _segments.Count; i++) _segments[i].Pose(eased);
            PoseCentre(eased);
            PoseSlots(eased);
            if (_ambience != null) _ambience.volume = _motion.AmbienceVolume * eased;
        }

        /// Grows the centre cube, jittering and flickering until it has resolved.
        private void PoseCentre(float open)
        {
            if (_centre == null) return;
            float unsettled = 1f - open;
            _centre.localScale = Vector3.one * open;
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
