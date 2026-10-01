using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static SomniumSpace.Worlds.SpaceScape.Ship.TutorialParts;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A free-standing, looping demonstration of the wiring system: a conduit with ghost hands acting out
    /// one use of it, under a board listing the steps, so a player can take it in at a glance before
    /// trying. Built from the real hologram, switch and addon prefabs when the world starts, with
    /// everything that could be grabbed, pressed or wired stripped out, so it only ever shows. Faces this
    /// transform's forward; the conduit sits at its origin.
    public sealed class TutorialDisplay : MonoBehaviour
    {
        public enum Demo { OpenConduit, OpenSwitch, TwoHands, ParkSegment, Addons, MoveSwitch }

        [Tooltip("Which use of the system this display acts out.")]
        [SerializeField] private Demo _demo;

        [Header("Parts")]
        [Tooltip("One segment's hologram, the same prefab the real hologram uses.")]
        [SerializeField] private SegmentHologram _segmentPrefab;
        [Tooltip("The capsule handle a repositionable addon gets in the hologram.")]
        [SerializeField] private SegmentHologram _addonHandlePrefab;
        [Tooltip("The hologram's centre marker.")]
        [SerializeField] private GameObject _centrePrefab;
        [Tooltip("The hologram's addon slot.")]
        [SerializeField] private GameObject _addonSlotPrefab;
        [Tooltip("The hologram's upgrade slot.")]
        [SerializeField] private GameObject _upgradeSlotPrefab;
        [Tooltip("The pulsing cube a hand passes through to unlock a switch's conduit.")]
        [SerializeField] private GameObject _unlockPrefab;
        [Tooltip("The rocker switch.")]
        [SerializeField] private GameObject _rockerPrefab;
        [Tooltip("The switch addon as a carried item.")]
        [SerializeField] private GameObject _addonItemPrefab;
        [Tooltip("What the cable is drawn with.")]
        [SerializeField] private Material _cableMaterial;
        [Tooltip("What the ghost hands are drawn with: something see-through, so they read as a demonstration.")]
        [SerializeField] private Material _ghostMaterial;
        [Tooltip("The text board above the example, listing the steps.")]
        [SerializeField] private TMP_Text _caption;

        [Tooltip("Seconds the hologram takes to open or close, as in the real one.")]
        [SerializeField] private float _openSeconds = 0.4f;

        private const float CableRadius = 0.015f;

        private readonly List<SegmentHologram> _segments = new List<SegmentHologram>();
        private readonly Transform[] _stubs = new Transform[2];
        private readonly GridDirection[] _faces = new GridDirection[2];
        private readonly bool[] _outward = new bool[2];
        private Transform _hologram;
        private Transform _centre;
        private Transform _addonSlot;
        private Transform _upgradeSlot;
        private Transform _unlock;
        private Vector3 _unlockScale;
        private Transform _rocker;
        private SegmentHologram _handle;
        private Transform _carried;
        private Transform _inSlot;
        private GhostHand _left;
        private GhostHand _right;
        private float _open;
        private float _swell;
        private float _clock;
        private int _lastLoop = -1;

        private GridDirection _switchFace;
        private int _switchTurns;
        private bool _switchParked;
        private bool _installed;

        private static readonly string[] Titles =
        {
            "OPENING A CONDUIT", "OPENING A SWITCH", "REWIRING, TWO HANDS",
            "PARKING A SEGMENT", "INSTALLING AN ADDON", "MOVING A SWITCH",
        };

        private static readonly string[][] Steps =
        {
            new[] { "Reach into a conduit", "Its hologram opens around your hand", "Take your hand out to close it" },
            new[] { "A switch locks its conduit: pass a hand through the pulsing cube", "Then reach into the conduit", "Its hologram opens", "Take your hand out to close it" },
            new[] { "One hand holds the conduit open", "The other grabs a handle", "Swing it to another face and let go: the cable follows", "Slide an arrow along its segment: out, or in", "Let go of the conduit to close it" },
            new[] { "One hand holds the conduit open", "Drag a handle into the middle and let go: it parks, and that side is sealed", "Drag a parked handle out to a face to use it again", "Let go of the conduit to close it" },
            new[] { "One hand holds the conduit open", "Carry an addon to the hologram's addon slot and let go: it installs", "While installed it shows in the slot: grab it to take it out", "Let go of the conduit to close it" },
            new[] { "Unlock the switch, then hold its conduit open", "Swing the capsule handle to another face: the switch moves with it", "Twist your wrist while holding it to turn the switch", "Drag it into the middle to park it: the switch is off, the conduit plain cable", "Pull it out to a face to bring it back" },
        };

        private float Loop
        {
            get
            {
                switch (_demo)
                {
                    case Demo.OpenConduit: return 6f;
                    case Demo.OpenSwitch: return 8f;
                    case Demo.MoveSwitch: return 14f;
                    default: return 12f;
                }
            }
        }

        private bool HasRocker => _demo == Demo.OpenSwitch || _demo == Demo.Addons || _demo == Demo.MoveSwitch;

        private bool TwoHanded => _demo != Demo.OpenConduit && _demo != Demo.OpenSwitch;

        private void Start()
        {
            WriteBoard();

            var parts = new GameObject("Parts").transform;
            parts.SetParent(transform, false);
            parts.gameObject.SetActive(false);   // stripped before anything in it wakes

            for (int i = 0; i < 2; i++) _stubs[i] = Stub(parts);

            _hologram = new GameObject("Hologram").transform;
            _hologram.SetParent(parts, false);
            _hologram.localScale = Vector3.one * GridCell.Size;
            for (int i = 0; i < 2; i++) _segments.Add(Make(_segmentPrefab, _hologram));
            if (_centrePrefab != null) _centre = Strip(Instantiate(_centrePrefab, _hologram, false)).transform;
            if (_addonSlotPrefab != null) _addonSlot = Strip(Instantiate(_addonSlotPrefab, _hologram, false)).transform;
            if (_upgradeSlotPrefab != null) _upgradeSlot = Strip(Instantiate(_upgradeSlotPrefab, _hologram, false)).transform;
            if (_demo == Demo.MoveSwitch && _addonHandlePrefab != null) _handle = Make(_addonHandlePrefab, _hologram);

            if (HasRocker && _rockerPrefab != null)
            {
                _rocker = Strip(Instantiate(_rockerPrefab, parts, false)).transform;
                var paddle = _rocker.Find("Paddle");
                if (paddle != null) paddle.localRotation = Quaternion.Euler(12f, 0f, 0f);
                var point = _rocker.Find("HologramUnlock");
                if (_demo == Demo.OpenSwitch && _unlockPrefab != null && point != null)
                {
                    _unlock = Strip(Instantiate(_unlockPrefab, point, false)).transform;
                    _unlockScale = _unlock.localScale;
                }
            }

            if (_demo == Demo.Addons && _addonItemPrefab != null)
            {
                _carried = Strip(Instantiate(_addonItemPrefab, parts, false)).transform;
                _carried.localScale = _addonItemPrefab.transform.localScale;
                if (_addonSlot != null)
                {
                    _inSlot = Strip(Instantiate(_addonItemPrefab, _addonSlot, false)).transform;
                    _inSlot.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    _inSlot.localScale = _addonItemPrefab.transform.localScale * 0.2f;
                }
            }

            _right = new GhostHand(parts, _ghostMaterial, false);
            if (TwoHanded) _left = new GhostHand(parts, _ghostMaterial, true);

            parts.gameObject.SetActive(true);
            Reset();
            PoseSwitch();
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            int loop = Mathf.FloorToInt(_clock / Loop);
            if (loop != _lastLoop) { _lastLoop = loop; Reset(); }
            float t = _clock - loop * Loop;

            _swell = 0f;
            switch (_demo)
            {
                case Demo.OpenConduit: OpenConduit(t); break;
                case Demo.OpenSwitch: OpenSwitch(t); break;
                case Demo.TwoHands: TwoHands(t); break;
                case Demo.ParkSegment: ParkSegment(t); break;
                case Demo.Addons: Addons(t); break;
                default: MoveSwitch(t); break;
            }

            PoseHologram();
            PoseSwitch();
        }

        // ---- The demonstrations. Positions are in the display's space, in metres: the conduit at the
        // origin, its cable along X, and whoever is watching out along +Z.

        private static readonly Vector3 Front = Vector3.forward * 0.03f;
        private Vector3 TipAt(GridDirection face) => face == GridDirection.None ? Vector3.zero : face.Vector() * GridCell.Half;
        private static readonly Vector3 RightAway = new Vector3(0.45f, 0.1f, 0.5f);

        private void OpenConduit(float t)
        {
            var away = new Vector3(0.12f, -0.05f, 0.55f);
            var inside = new Vector3(0.06f, -0.03f, 0.12f);
            _right.Place(Vector3.Lerp(Vector3.Lerp(away, inside, Ease(t, 0.3f, 1.3f)), away, Ease(t, 3.6f, 4.6f)), Reaching, 0.2f);
            Opening(t > 1.2f && t < 3.9f);
        }

        private void OpenSwitch(float t)
        {
            var away = new Vector3(0.2f, 0.15f, 0.6f);
            var cube = transform.InverseTransformPoint(_unlock != null ? _unlock.position : transform.position);
            var inside = new Vector3(0.05f, -0.06f, 0.12f);

            Vector3 hand = Vector3.Lerp(away, cube + Vector3.forward * 0.06f, Ease(t, 0.3f, 1.4f));
            hand = Vector3.Lerp(hand, cube + new Vector3(-0.02f, 0f, -0.08f), Ease(t, 1.4f, 2.0f));
            hand = Vector3.Lerp(hand, inside, Ease(t, 2.2f, 3.2f));
            hand = Vector3.Lerp(hand, away, Ease(t, 5.6f, 6.6f));
            _right.Place(hand, Reaching, 0.2f);

            if (_unlock != null)
            {
                _unlock.gameObject.SetActive(!(t > 1.9f && t < 6.2f));
                _unlock.localScale = _unlockScale * (1f + 0.1f * (1f - Mathf.Cos(t * 2f * Mathf.PI / 1.2f)));
            }
            Opening(t > 3.1f && t < 5.9f);
        }

        /// The left hand in from below, holding the conduit open between two times.
        private void Hold(float t, float until)
        {
            var away = new Vector3(-0.25f, -0.2f, 0.55f);
            var inside = new Vector3(-0.1f, -0.09f, 0.1f);
            _left.Place(Vector3.Lerp(away, inside, Ease(t, 0.2f, 1.2f) * (1f - Ease(t, until, until + 1f))), Reaching, 0.3f);
            Opening(t > 1.1f && t < until + 0.3f);
        }

        private void TwoHands(float t)
        {
            Hold(t, 11.2f);
            var seg = _segments[1];
            Vector3 tipRest = TipAt(GridDirection.XPlus), tipUp = TipAt(GridDirection.YPlus);

            if (t < 2.6f) Reach(t, 1.4f, tipRest);
            else if (t < 5f) Drag(seg, Vector3.Slerp(tipRest, tipUp, Ease(t, 2.8f, 4.4f)));
            else if (t < 5.2f) { LetGo(1, GridDirection.YPlus, true); _right.Place(tipUp + Front, Reaching, 0f); }
            else if (t < 10.8f)
            {
                Vector3 arrow = ArrowAt(1), along = Vector3.up * 0.06f;
                Vector3 at = arrow - along * Ease(t, 6.6f, 7.4f) + along * Ease(t, 8.6f, 9.4f);
                float grip = Ease(t, 6.0f, 6.3f) * (1f - Ease(t, 10.0f, 10.3f));
                Vector3 approach = Vector3.Lerp(tipUp, arrow, Ease(t, 5.3f, 6.0f));
                _right.Place((t < 6.0f ? approach : at) + Front, Reaching, grip);
                bool outward = !(t > 7.1f && t < 9.1f);
                if (_outward[1] != outward) { _outward[1] = outward; seg.SetOutward(outward); }
            }
            else _right.Place(Vector3.Lerp(ArrowAt(1), RightAway, Ease(t, 10.8f, 11.6f)) + Front, Reaching, 0f);
        }

        private void ParkSegment(float t)
        {
            Hold(t, 10.2f);
            var seg = _segments[1];
            Vector3 tipRest = TipAt(GridDirection.XPlus), tipUp = TipAt(GridDirection.YPlus);

            if (t < 2.6f) Reach(t, 1.4f, tipRest);
            else if (t < 4.0f) { Drag(seg, Vector3.Lerp(tipRest, Vector3.zero, Ease(t, 2.8f, 3.8f))); _swell = Ease(t, 3.3f, 3.8f); }
            else if (t < 4.2f) { LetGo(1, GridDirection.None, true); _right.Place(Front, Reaching, 0f); }
            else if (t < 5.8f) _right.Place(Front * (1f + 2f * Mathf.Sin(Mathf.InverseLerp(4.2f, 5.8f, t) * Mathf.PI)), Reaching, Ease(t, 5.3f, 5.6f));
            else if (t < 7.0f) Drag(seg, Vector3.Lerp(Vector3.zero, tipUp, Ease(t, 5.9f, 6.9f)));
            else if (t < 7.2f) { LetGo(1, GridDirection.YPlus, true); _right.Place(tipUp + Front, Reaching, 0f); }
            else _right.Place(Vector3.Lerp(tipUp, RightAway, Ease(t, 7.2f, 8.2f)) + Front, Reaching, 0f);
        }

        private void Addons(float t)
        {
            Hold(t, 10.0f);
            Vector3 slot = _addonSlot != null ? transform.InverseTransformPoint(_addonSlot.position) : new Vector3(-0.035f, 0.075f, 0f);
            var start = new Vector3(0.45f, -0.05f, 0.5f);

            if (t < 3.6f)
            {
                // In from the side with the addon in hand, to the slot, and let go.
                Vector3 at = Vector3.Lerp(start, slot, Ease(t, 1.4f, 3.3f));
                _right.Place(at + Front, Reaching, 0.7f);
                Carry(at, true);
            }
            else if (t < 5.5f)
            {
                if (!_installed) { _installed = true; Carry(Vector3.zero, false); }
                _right.Place(Vector3.Lerp(slot, RightAway, Ease(t, 3.7f, 4.6f)) + Front, Reaching, 0f);
            }
            else if (t < 7.0f)
            {
                _right.Place(Vector3.Lerp(RightAway, slot, Ease(t, 5.5f, 6.6f)) + Front, Reaching, Ease(t, 6.6f, 6.9f) * 0.7f);
            }
            else
            {
                // Taken out: the switch comes off the cable and the addon is in the hand again.
                if (_installed) _installed = false;
                Vector3 at = Vector3.Lerp(slot, start, Ease(t, 7.1f, 8.6f));
                _right.Place(at + Front, Reaching, 0.7f);
                Carry(at, t < 9.2f);
            }
        }

        private void MoveSwitch(float t)
        {
            Hold(t, 12.2f);
            if (_handle == null) return;
            Vector3 front = TipAt(GridDirection.ZPlus), up = TipAt(GridDirection.YPlus);

            if (t < 2.6f) Reach(t, 1.4f, front);
            else if (t < 4.0f) DragHandle(Vector3.Slerp(front, up, Ease(t, 2.7f, 3.9f)), GridDirection.ZPlus);
            else if (t < 4.3f) { Settle(GridDirection.YPlus, 0, false); _right.Place(up + Front, Reaching, 0f); }
            else if (t < 6.8f)
            {
                // Grip the capsule where it now is and roll the wrist a quarter turn.
                float roll = Ease(t, 5.1f, 6.4f) * 90f;
                _right.Place(up + Front, Reaching * Quaternion.Euler(0f, 0f, -roll), Ease(t, 4.5f, 4.8f) * (1f - Ease(t, 6.5f, 6.8f)));
                _handle.TipHeld = t < 6.5f;
                if (_handle.TipHeld) _handle.Tip.position = transform.TransformPoint(up);
                int turns = roll >= 45f ? 1 : 0;
                if (turns != _switchTurns) _switchTurns = turns;
                TwistHandle(GridDirection.YPlus, _switchTurns);
            }
            else if (t < 9.0f)
            {
                if (t < 7.6f) _right.Place(up + Front, Reaching, Ease(t, 7.2f, 7.5f));
                else { DragHandle(Vector3.Lerp(up, Vector3.zero, Ease(t, 7.7f, 8.7f)), GridDirection.YPlus); _swell = Ease(t, 8.2f, 8.7f); }
            }
            else if (t < 9.2f) { Settle(GridDirection.YPlus, _switchTurns, true); _right.Place(Front, Reaching, 0f); }
            else if (t < 11.2f)
            {
                if (t < 9.9f) _right.Place(Front, Reaching, Ease(t, 9.5f, 9.8f));
                else DragHandle(Vector3.Lerp(Vector3.zero, front, Ease(t, 10.0f, 11.0f)), GridDirection.ZPlus);
            }
            else if (t < 11.4f) { Settle(GridDirection.ZPlus, 0, false); _right.Place(front + Front, Reaching, 0f); }
            else _right.Place(Vector3.Lerp(front, RightAway, Ease(t, 11.4f, 12.2f)) + Front, Reaching, 0f);
        }

        // ---- Hand actions.

        /// The right hand in from the side to a handle, closing on it as it arrives.
        private void Reach(float t, float from, Vector3 target)
        {
            _right.Place(Vector3.Lerp(RightAway, target, Ease(t, from, from + 1f)) + Front, Reaching, Ease(t, from + 0.8f, from + 1.1f));
        }

        /// Carries a segment's handle in the right hand.
        private void Drag(SegmentHologram segment, Vector3 at)
        {
            segment.TipHeld = true;
            segment.Tip.position = transform.TransformPoint(at);
            _right.Place(at + Front, Reaching, 1f);
        }

        /// Lets go of a segment's handle: it lands on a face, or parks in the middle, and the cable follows.
        private void LetGo(int i, GridDirection face, bool outward)
        {
            if (_faces[i] == face && !_segments[i].TipHeld) return;
            Vector3 letGo = _segments[i].Tip.position;
            _faces[i] = face;
            _outward[i] = outward;
            Apply(i);
            _segments[i].SlideHome(letGo, _openSeconds);
            PointStub(i);
        }

        /// Carries the switch's capsule handle, showing the twist it would land with.
        private void DragHandle(Vector3 at, GridDirection showing)
        {
            _handle.TipHeld = true;
            _handle.Tip.position = transform.TransformPoint(at);
            TwistHandle(showing, _switchTurns);
            _right.Place(at + Front, Reaching, 1f);
        }

        /// Lets go of the capsule: the switch goes to that face and twist, or parks.
        private void Settle(GridDirection face, int turns, bool parked)
        {
            if (_switchFace == face && _switchTurns == turns && _switchParked == parked && !_handle.TipHeld) return;
            Vector3 letGo = _handle.Tip.position;
            _switchFace = face;
            _switchTurns = turns;
            _switchParked = parked;
            ApplyHandle();
            _handle.SlideHome(letGo, _openSeconds);
        }

        /// Shows or hides the addon in the right hand, at a point.
        private void Carry(Vector3 at, bool shown)
        {
            if (_carried == null) return;
            _carried.gameObject.SetActive(shown);
            _carried.localPosition = at + new Vector3(0f, -0.02f, 0.02f);
            _carried.localRotation = Quaternion.identity;
        }

        // ---- Hologram, switch, cable and board.

        /// Starts a loop over: straight cable, -X in, +X out, switch on the front, hologram shut.
        private void Reset()
        {
            _faces[0] = GridDirection.XMinus; _faces[1] = GridDirection.XPlus;
            _outward[0] = false; _outward[1] = true;
            _open = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                _segments[i].TipHeld = false;
                Apply(i);
                PointStub(i);
            }

            _switchFace = GridDirection.ZPlus;
            _switchTurns = 0;
            _switchParked = false;
            _installed = _demo != Demo.Addons;
            if (_handle != null) { _handle.TipHeld = false; ApplyHandle(); }
            Carry(Vector3.zero, false);
        }

        private void Opening(bool open)
        {
            float step = _openSeconds > 0f ? Time.deltaTime / _openSeconds : 1f;
            _open = Mathf.MoveTowards(_open, open ? 1f : 0f, step);
        }

        private void PoseHologram()
        {
            float eased = Mathf.SmoothStep(0f, 1f, _open);
            _hologram.gameObject.SetActive(_open > 0f);
            foreach (var s in _segments) s.Pose(eased);
            if (_handle != null)
            {
                _handle.Pose(eased);
                if (!_handle.TipHeld) TwistHandle(_switchParked ? GridDirection.YPlus : _switchFace, _switchTurns);
            }
            if (_centre != null) _centre.localScale = Vector3.one * (eased * (1f + _swell));
            if (_addonSlot != null) Slot(_addonSlot, new Vector3(-0.07f, 0.15f, 0f), eased);
            if (_upgradeSlot != null) Slot(_upgradeSlot, new Vector3(0.07f, 0.15f, 0f), eased);
            if (_inSlot != null) _inSlot.gameObject.SetActive(_installed);
        }

        /// Puts the switch on its face at its twist, or hides it while parked or not installed.
        private void PoseSwitch()
        {
            if (_rocker == null) return;
            bool shown = _installed && !_switchParked;
            _rocker.gameObject.SetActive(shown);
            if (!shown) return;
            Vector3 face = _switchFace.Vector();
            _rocker.localPosition = face * GridCell.Half;
            _rocker.localRotation = Quaternion.LookRotation(face, AddonModule.UpFor(_switchFace, _switchTurns));
        }

        private static void Slot(Transform slot, Vector3 home, float open)
        {
            slot.localPosition = home * open;
            slot.localScale = Vector3.one * Mathf.Lerp(0.1f, 1f, open);
        }

        private void Apply(int i)
        {
            if (_faces[i] == GridDirection.None) { _segments[i].Park(); return; }
            Vector3 direction = _faces[i].Vector();
            Vector3 above = Mathf.Abs(direction.y) < 0.5f ? transform.up : transform.forward;
            _segments[i].PlaceOnFace(direction, above, _outward[i]);
        }

        private void ApplyHandle()
        {
            if (_switchParked) _handle.Park();
            else _handle.PlaceOnFace(_switchFace.Vector(), transform.up, false);
        }

        /// Stands the capsule along the switch's up, so its twist can be read.
        private void TwistHandle(GridDirection face, int turns)
        {
            if (_handle == null || _handle.Tip == null) return;
            _handle.Tip.rotation = transform.rotation * Quaternion.LookRotation(face.Vector(), AddonModule.UpFor(face, turns));
        }

        /// Where a segment's arrow is, in the display's space.
        private Vector3 ArrowAt(int i)
        {
            var arrow = _segments[i].transform.Find("ArrowPivot");
            return transform.InverseTransformPoint(arrow != null ? arrow.position : _segments[i].Tip.position);
        }

        /// One stub of cable from the centre out to its face, as a conduit draws itself.
        private Transform Stub(Transform parent)
        {
            var stub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stub.GetComponent<Collider>());
            stub.name = "Cable";
            stub.transform.SetParent(parent, false);
            if (_cableMaterial != null) stub.GetComponent<Renderer>().sharedMaterial = _cableMaterial;
            return stub.transform;
        }

        /// Points a stub at its face, or hides it while its segment is parked.
        private void PointStub(int i)
        {
            bool used = _faces[i] != GridDirection.None;
            _stubs[i].gameObject.SetActive(used);
            if (!used) return;
            Vector3 along = _faces[i].Vector();
            _stubs[i].localPosition = along * (GridCell.Half * 0.5f);
            _stubs[i].localRotation = Quaternion.FromToRotation(Vector3.up, along);
            _stubs[i].localScale = new Vector3(CableRadius * 2f, GridCell.Half * 0.5f, CableRadius * 2f);
        }

        /// Writes the board once: the title, then every step, to be read at a glance.
        private void WriteBoard()
        {
            if (_caption == null) return;
            var text = new System.Text.StringBuilder($"<b>{Titles[(int)_demo]}</b>\n\n");
            var lines = Steps[(int)_demo];
            for (int i = 0; i < lines.Length; i++) text.Append($"{i + 1}. {lines[i]}\n");
            _caption.SetText(text.ToString());
        }

        private SegmentHologram Make(SegmentHologram prefab, Transform parent) =>
            Strip(Instantiate(prefab, parent, false).gameObject).GetComponent<SegmentHologram>();
    }
}
