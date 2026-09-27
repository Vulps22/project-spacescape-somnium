using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A free-standing, looping demonstration of the wiring system: a conduit with ghost hands acting out
    /// one use of it, and a board listing the steps with the current one lit. Built from the real hologram
    /// and switch prefabs when the world starts, with everything that could be grabbed, pressed or wired
    /// stripped out, so it only ever shows. Faces this transform's forward; the conduit sits at its origin.
    public sealed class TutorialDisplay : MonoBehaviour
    {
        public enum Demo { OpenConduit, OpenSwitch, TwoHands }

        [Tooltip("Which use of the system this display acts out.")]
        [SerializeField] private Demo _demo;

        [Header("Parts")]
        [Tooltip("One segment's hologram, the same prefab the real hologram uses.")]
        [SerializeField] private SegmentHologram _segmentPrefab;
        [Tooltip("The hologram's centre marker.")]
        [SerializeField] private GameObject _centrePrefab;
        [Tooltip("The hologram's addon slot.")]
        [SerializeField] private GameObject _addonSlotPrefab;
        [Tooltip("The hologram's upgrade slot.")]
        [SerializeField] private GameObject _upgradeSlotPrefab;
        [Tooltip("The pulsing cube a hand passes through to unlock a switch's conduit.")]
        [SerializeField] private GameObject _unlockPrefab;
        [Tooltip("The rocker switch, shown on the switch display.")]
        [SerializeField] private GameObject _rockerPrefab;
        [Tooltip("What the cable is drawn with.")]
        [SerializeField] private Material _cableMaterial;
        [Tooltip("What the ghost hands are drawn with: something see-through, so they read as a demonstration.")]
        [SerializeField] private Material _ghostMaterial;
        [Tooltip("The text board listing the steps. Optional.")]
        [SerializeField] private TMP_Text _caption;

        [Header("Timing")]
        [Tooltip("Seconds the hologram takes to open or close, as in the real one.")]
        [SerializeField] private float _openSeconds = 0.4f;
        [Tooltip("Colour of the step being acted out on the board.")]
        [SerializeField] private Color _currentStep = new Color(0.4f, 0.9f, 1f);
        [Tooltip("Colour of the other steps on the board.")]
        [SerializeField] private Color _otherSteps = new Color(0.6f, 0.6f, 0.65f);

        private const float CableRadius = 0.015f;

        private readonly List<SegmentHologram> _segments = new List<SegmentHologram>();
        private readonly Transform[] _stubs = new Transform[2];
        private readonly GridDirection[] _faces = { GridDirection.XMinus, GridDirection.XPlus };
        private readonly bool[] _outward = { false, true };
        private Transform _hologram;
        private Transform _centre;
        private Transform _addonSlot;
        private Transform _upgradeSlot;
        private Transform _unlock;
        private Vector3 _unlockScale;
        private GhostHand _left;
        private GhostHand _right;
        private float _open;
        private float _clock;
        private int _step = -1;
        private int _lastLoop = -1;

        private static readonly string[][] Steps =
        {
            new[] { "Reach into a conduit", "Its hologram opens around your hand", "Take your hand out to close it" },
            new[] { "A switch locks its conduit: pass a hand through the pulsing cube", "Then reach into the conduit", "Its hologram opens", "Take your hand out to close it" },
            new[] { "One hand holds the conduit open", "The other grabs a handle", "Swing it to another face and let go: the cable follows", "Slide an arrow along its segment: out, or in", "Let go of the conduit to close it" },
        };

        private static readonly string[] Titles = { "OPENING A CONDUIT", "OPENING A SWITCH", "REWIRING, TWO HANDS" };

        private float Loop => _demo == Demo.TwoHands ? 13f : _demo == Demo.OpenSwitch ? 8f : 6f;

        private void Start()
        {
            var parts = new GameObject("Parts").transform;
            parts.SetParent(transform, false);
            parts.gameObject.SetActive(false);   // stripped before anything in it wakes

            for (int i = 0; i < 2; i++) _stubs[i] = Stub(parts);

            _hologram = new GameObject("Hologram").transform;
            _hologram.SetParent(parts, false);
            _hologram.localScale = Vector3.one * GridCell.Size;
            for (int i = 0; i < 2; i++) _segments.Add(Strip(Instantiate(_segmentPrefab, _hologram, false).gameObject).GetComponent<SegmentHologram>());
            if (_centrePrefab != null) _centre = Strip(Instantiate(_centrePrefab, _hologram, false)).transform;
            if (_addonSlotPrefab != null) _addonSlot = Strip(Instantiate(_addonSlotPrefab, _hologram, false)).transform;
            if (_upgradeSlotPrefab != null) _upgradeSlot = Strip(Instantiate(_upgradeSlotPrefab, _hologram, false)).transform;

            if (_demo == Demo.OpenSwitch && _rockerPrefab != null)
            {
                var rocker = Strip(Instantiate(_rockerPrefab, parts, false)).transform;
                rocker.localPosition = Vector3.forward * GridCell.Half;
                var paddle = rocker.Find("Paddle");
                if (paddle != null) paddle.localRotation = Quaternion.Euler(12f, 0f, 0f);
                var point = rocker.Find("HologramUnlock");
                if (_unlockPrefab != null && point != null)
                {
                    _unlock = Strip(Instantiate(_unlockPrefab, point, false)).transform;
                    _unlockScale = _unlock.localScale;
                }
            }

            _right = new GhostHand(parts, _ghostMaterial, false);
            if (_demo == Demo.TwoHands) _left = new GhostHand(parts, _ghostMaterial, true);

            parts.gameObject.SetActive(true);
            Reset();
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            int loop = Mathf.FloorToInt(_clock / Loop);
            if (loop != _lastLoop) { _lastLoop = loop; Reset(); }
            float t = _clock - loop * Loop;

            switch (_demo)
            {
                case Demo.OpenConduit: OpenConduit(t); break;
                case Demo.OpenSwitch: OpenSwitch(t); break;
                default: TwoHands(t); break;
            }

            PoseHologram();
        }

        // ---- The three demonstrations. Positions are in the display's space, in metres; the conduit is at
        // the origin, its cable along X, and whoever is watching stands out along +Z.

        private void OpenConduit(float t)
        {
            var away = new Vector3(0.12f, -0.05f, 0.55f);
            var inside = new Vector3(0.06f, -0.03f, 0.12f);
            float reach = Ease(t, 0.3f, 1.3f), leave = Ease(t, 3.6f, 4.6f);
            _right.Place(Vector3.Lerp(Vector3.Lerp(away, inside, reach), away, leave), Reaching, 0.2f);
            Opening(t > 1.2f && t < 3.9f);
            Step(t < 1.3f ? 0 : t < 3.6f ? 1 : 2);
        }

        private void OpenSwitch(float t)
        {
            var away = new Vector3(0.2f, 0.15f, 0.6f);
            var cube = transform.InverseTransformPoint(_unlock != null ? _unlock.position : transform.position);
            var past = cube + new Vector3(-0.02f, 0f, -0.08f);
            var inside = new Vector3(0.05f, -0.06f, 0.12f);

            Vector3 hand = Vector3.Lerp(away, cube + Vector3.forward * 0.06f, Ease(t, 0.3f, 1.4f));
            hand = Vector3.Lerp(hand, past, Ease(t, 1.4f, 2.0f));
            hand = Vector3.Lerp(hand, inside, Ease(t, 2.2f, 3.2f));
            hand = Vector3.Lerp(hand, away, Ease(t, 5.6f, 6.6f));
            _right.Place(hand, Reaching, 0.2f);

            bool unlocked = t > 1.9f && t < 6.2f;
            if (_unlock != null)
            {
                _unlock.gameObject.SetActive(!unlocked);
                _unlock.localScale = _unlockScale * (1f + 0.1f * (1f - Mathf.Cos(t * 2f * Mathf.PI / 1.2f)));
            }
            Opening(t > 3.1f && t < 5.9f);
            Step(t < 1.9f ? 0 : t < 3.1f ? 1 : t < 5.6f ? 2 : 3);
        }

        private void TwoHands(float t)
        {
            // The holding hand, in from below and to the left, for the whole of it.
            var leftAway = new Vector3(-0.25f, -0.2f, 0.55f);
            var leftIn = new Vector3(-0.1f, -0.09f, 0.1f);
            float hold = Ease(t, 0.2f, 1.2f) * (1f - Ease(t, 11.2f, 12.2f));
            _left.Place(Vector3.Lerp(leftAway, leftIn, hold), Reaching, 0.3f);
            Opening(t > 1.1f && t < 11.5f);

            var seg = _segments[1];
            Vector3 tipRest = Vector3.right * GridCell.Half;
            Vector3 tipUp = Vector3.up * GridCell.Half;
            var rightAway = new Vector3(0.45f, 0.1f, 0.5f);

            if (t < 2.6f)
            {
                // Reach for the +X handle.
                _right.Place(Vector3.Lerp(rightAway, tipRest + Vector3.forward * 0.03f, Ease(t, 1.4f, 2.4f)), Reaching, Ease(t, 2.2f, 2.5f));
                Step(t < 1.4f ? 0 : 1);
            }
            else if (t < 5f)
            {
                // Swing it round to +Y, the handle in the hand.
                float swing = Ease(t, 2.8f, 4.4f);
                Vector3 at = Vector3.Slerp(tipRest, tipUp, swing);
                seg.TipHeld = true;
                seg.Tip.position = transform.TransformPoint(at);
                _right.Place(at + Vector3.forward * 0.03f, Reaching, 1f);
                Step(2);
            }
            else if (t < 5.2f)
            {
                // Let go: it lands on the face, and the cable goes with it.
                if (_faces[1] != GridDirection.YPlus)
                {
                    Vector3 letGo = seg.Tip.position;
                    _faces[1] = GridDirection.YPlus;
                    Apply(1);
                    seg.SlideHome(letGo, _openSeconds);
                    PointStub(1);
                }
                _right.Place(tipUp + Vector3.forward * 0.03f, Reaching, 0f);
                Step(2);
            }
            else if (t < 10.8f)
            {
                // Slide its arrow in along the segment, then back out.
                Vector3 arrow = ArrowAt(1);
                float slideIn = Ease(t, 6.6f, 7.4f), slideOut = Ease(t, 8.6f, 9.4f);
                Vector3 along = Vector3.up * 0.06f;
                Vector3 at = arrow - along * slideIn + along * slideOut;
                float grip = Ease(t, 6.0f, 6.3f) * (1f - Ease(t, 10.0f, 10.3f));
                Vector3 approach = Vector3.Lerp(tipUp + Vector3.forward * 0.03f, arrow + Vector3.forward * 0.03f, Ease(t, 5.3f, 6.0f));
                _right.Place(t < 6.0f ? approach : at + Vector3.forward * 0.03f, Reaching, grip);

                bool outward = !(t > 7.1f && t < 9.1f);
                if (_outward[1] != outward) { _outward[1] = outward; seg.SetOutward(outward); }
                Step(3);
            }
            else
            {
                _right.Place(Vector3.Lerp(ArrowAt(1) + Vector3.forward * 0.03f, rightAway, Ease(t, 10.8f, 11.6f)), Reaching, 0f);
                Step(4);
            }
        }

        // ---- Hologram, cable and board.

        /// Starts a loop over: straight cable, -X in, +X out, hologram shut.
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
            if (_centre != null) _centre.localScale = Vector3.one * eased;
            if (_addonSlot != null) Slot(_addonSlot, new Vector3(-0.07f, 0.15f, 0f), eased);
            if (_upgradeSlot != null) Slot(_upgradeSlot, new Vector3(0.07f, 0.15f, 0f), eased);
        }

        private static void Slot(Transform slot, Vector3 home, float open)
        {
            slot.localPosition = home * open;
            slot.localScale = Vector3.one * Mathf.Lerp(0.1f, 1f, open);
        }

        private void Apply(int i)
        {
            Vector3 direction = _faces[i].Vector();
            Vector3 above = Mathf.Abs(direction.y) < 0.5f ? transform.up : transform.forward;
            _segments[i].PlaceOnFace(direction, above, _outward[i]);
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

        private void PointStub(int i)
        {
            Vector3 along = _faces[i].Vector();
            _stubs[i].localPosition = along * (GridCell.Half * 0.5f);
            _stubs[i].localRotation = Quaternion.FromToRotation(Vector3.up, along);
            _stubs[i].localScale = new Vector3(CableRadius * 2f, GridCell.Half * 0.5f, CableRadius * 2f);
        }

        /// Writes the board: the title, then the steps with the one being acted out lit.
        private void Step(int step)
        {
            if (_caption == null || step == _step) return;
            _step = step;
            var lines = Steps[(int)_demo];
            var text = new System.Text.StringBuilder($"<b>{Titles[(int)_demo]}</b>\n\n");
            for (int i = 0; i < lines.Length; i++)
            {
                var colour = ColorUtility.ToHtmlStringRGB(i == step ? _currentStep : _otherSteps);
                text.Append($"<color=#{colour}>{(i == step ? "<b>" : "")}{i + 1}. {lines[i]}{(i == step ? "</b>" : "")}</color>\n");
            }
            _caption.SetText(text.ToString());
        }

        /// Takes out everything that could be grabbed, pressed, wired or collided with, leaving the look.
        private static GameObject Strip(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(true)) DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(c is SegmentHologram) && !(c is TMP_Text)) DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Joint>(true)) DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            return go;
        }

        /// 0 before a span of time, 1 after it, eased in between.
        private static float Ease(float t, float from, float to) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));

        /// A hand reaching toward the conduit from where the watcher stands: fingers pointing in, palm down.
        private static readonly Quaternion Reaching = Quaternion.LookRotation(Vector3.back, Vector3.up);

        /// A see-through hand built from simple shapes: a palm, four fingers and a thumb, which curl to grip.
        private sealed class GhostHand
        {
            private readonly Transform _root;
            private readonly Transform[] _fingers = new Transform[5];

            public GhostHand(Transform parent, Material material, bool left)
            {
                _root = new GameObject(left ? "GhostHandLeft" : "GhostHandRight").transform;
                _root.SetParent(parent, false);
                float side = left ? -1f : 1f;

                // The palm, fingers forward (+Z of the hand), back of the hand up.
                Part(PrimitiveType.Cube, _root, new Vector3(0f, 0f, -0.045f), new Vector3(0.08f, 0.022f, 0.09f), material);
                for (int i = 0; i < 4; i++)
                {
                    var knuckle = new GameObject("Finger").transform;
                    knuckle.SetParent(_root, false);
                    knuckle.localPosition = new Vector3(side * (-0.03f + i * 0.02f), 0f, 0f);
                    Part(PrimitiveType.Capsule, knuckle, new Vector3(0f, 0f, 0.035f), new Vector3(0.017f, 0.035f, 0.017f), material, 90f);
                    _fingers[i] = knuckle;
                }
                var thumb = new GameObject("Thumb").transform;
                thumb.SetParent(_root, false);
                thumb.localPosition = new Vector3(side * -0.045f, -0.005f, -0.05f);
                thumb.localRotation = Quaternion.Euler(0f, side * -40f, 0f);
                Part(PrimitiveType.Capsule, thumb, new Vector3(0f, 0f, 0.03f), new Vector3(0.019f, 0.03f, 0.019f), material, 90f);
                _fingers[4] = thumb;
            }

            /// Puts the hand's fingertips at a point, turned a way, with its fingers curled 0 (open) to 1 (gripping).
            public void Place(Vector3 fingertips, Quaternion turn, float grip)
            {
                _root.localRotation = turn;
                _root.localPosition = fingertips - turn * (Vector3.forward * 0.07f);
                for (int i = 0; i < 4; i++) _fingers[i].localRotation = Quaternion.Euler(grip * 80f, 0f, 0f);
                _fingers[4].localRotation = Quaternion.Euler(0f, (_fingers[4].localPosition.x > 0f ? 40f : -40f) * (1f - grip * 0.8f), 0f);
            }

            private static void Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, float pitch = 0f)
            {
                var part = GameObject.CreatePrimitive(type);
                Object.Destroy(part.GetComponent<Collider>());
                part.transform.SetParent(parent, false);
                part.transform.localPosition = position;
                part.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                part.transform.localScale = scale;
                var r = part.GetComponent<Renderer>();
                if (material != null) r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
