using TMPro;
using UnityEngine;
using static SomniumSpace.Worlds.SpaceScape.Ship.TutorialParts;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A free-standing, looping demonstration of changing a component: two slots side by side, one holding a
    /// component. A ghost hand pulls the handle bar to unlock it, lifts the component out, carries it to the
    /// empty slot, lines it up by the placement guide and lets go, and it snaps in; the next loop takes it
    /// back. Built from the real slot and component prefabs when the world starts, shrunk, with everything that
    /// could be grabbed or wired stripped out, so it only ever shows. Faces this transform's forward; the
    /// slots are centred on its origin, where the conduit displays put their conduit.
    ///
    /// Networking: none. It holds no state, and runs the same on every client from when the world started.
    public sealed class SlotTutorialDisplay : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("The slot, shown twice. Both copies are this prefab.")]
        [SerializeField] private ComponentSlot _slotPrefab;
        [Tooltip("The component moved between them. Must be the slot's size.")]
        [SerializeField] private GridNode _componentPrefab;
        [Tooltip("What the ghost hand is drawn with: something see-through, so it reads as a demonstration.")]
        [SerializeField] private Material _ghostMaterial;
        [Tooltip("The text board above the example, listing the steps.")]
        [SerializeField] private TMP_Text _caption;

        [Header("Layout")]
        [Tooltip("How much smaller than life the slots and component are drawn.")]
        [SerializeField] private float _scale = 0.1f;
        [Tooltip("Space between the two slots, in metres as drawn.")]
        [SerializeField] private float _gap = 0.06f;

        private const float Loop = 13f;
        private const string Title = "CHANGING A COMPONENT";

        private static readonly string[] Steps =
        {
            "Pull the bar on the slot's bottom edge up until it turns green: the slot is unlocked for a few seconds",
            "Grab the component and pull it out",
            "Carry it to an empty slot of the same size",
            "Line it up: the slot shows a box, red, then orange, then green once it is centred and square",
            "Let go on green: it snaps in, and the slot locks",
        };

        private Transform _model;               // the shrunk slots and component; its space is life-size metres
        private readonly Slot[] _slots = new Slot[2];
        private Transform _component;
        private Quaternion _seat;               // how the component sits in a slot
        private GhostHand _hand;
        private Vector3 _extent;
        private float _seatDistance = 0.5f;
        private float _seatAngle = 30f;
        private float _unlockSeconds = 10f;
        private float _clock;
        private int _lastLoop = -1;
        private int _from;                      // the slot the component starts this loop in
        private float _unlockedAt;

        /// One shown slot: its bar, and the box it shows while a component is lined up in it.
        private sealed class Slot
        {
            public Transform Root;
            public Transform Bar;
            public Vector3 BarRest;
            public float Travel = 0.1f;
            public Renderer BarRenderer;
            public Material Locked, Unlocked;
            public MeshRenderer Guide;
            public Material Neither, OneOf, Both;
        }

        private void Start()
        {
            WriteBoard();
            if (_slotPrefab == null || _componentPrefab == null) return;

            var parts = new GameObject("Parts").transform;
            parts.SetParent(transform, false);
            parts.gameObject.SetActive(false);   // stripped before anything in it wakes

            _model = new GameObject("Model").transform;
            _model.SetParent(parts, false);
            _model.localScale = Vector3.one * _scale;

            _extent = _slotPrefab.Extent;
            _seatDistance = _slotPrefab.SeatDistance;
            _seatAngle = _slotPrefab.SeatAngle;
            _unlockSeconds = _slotPrefab.UnlockSeconds;
            float apart = (_extent.x + _gap / _scale) * 0.5f;
            for (int i = 0; i < 2; i++) _slots[i] = MakeSlot(new Vector3(i == 0 ? -apart : apart, 0f, 0f));

            _seat = _componentPrefab.TryGetComponent<SlottedComponent>(out var slotted) ? slotted.SeatedIn(Quaternion.identity) : Quaternion.identity;
            _component = Strip(Instantiate(_componentPrefab, _model, false).gameObject).transform;

            _hand = new GhostHand(parts, _ghostMaterial, false);

            parts.gameObject.SetActive(true);
        }

        private Slot MakeSlot(Vector3 at)
        {
            var made = Instantiate(_slotPrefab, _model, false);
            var slot = new Slot { Root = made.transform };
            var handle = made.GetComponentInChildren<SlotHandle>(true);
            if (handle != null && handle.Bar != null)
            {
                slot.Bar = handle.Bar;
                slot.BarRest = slot.Bar.localPosition;
                slot.BarRenderer = handle.BarRenderer;
                slot.Locked = handle.MaterialFor(false);
                slot.Unlocked = handle.MaterialFor(true);
                if (slot.Bar.TryGetComponent<SlideGrabTransformer>(out var slide)) slot.Travel = slide.Travel;
            }
            slot.Neither = made.GuideMaterial(ComponentSlot.Guide.Neither);
            slot.OneOf = made.GuideMaterial(ComponentSlot.Guide.OneOf);
            slot.Both = made.GuideMaterial(ComponentSlot.Guide.Both);
            Strip(made.gameObject);

            slot.Root.localPosition = at;
            slot.Root.localRotation = Quaternion.identity;

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.name = "Placement Guide";
            box.transform.SetParent(slot.Root, false);
            box.transform.localScale = _extent;
            slot.Guide = box.GetComponent<MeshRenderer>();
            slot.Guide.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            slot.Guide.receiveShadows = false;
            slot.Guide.enabled = false;
            return slot;
        }

        private void Update()
        {
            if (_component == null) return;
            _clock += Time.deltaTime;
            int loop = Mathf.FloorToInt(_clock / Loop);
            if (loop != _lastLoop) { _lastLoop = loop; Reset(loop); }
            Play(_clock - loop * Loop);
        }

        /// Starts a loop over: the component seated in one slot, both slots locked, bars home.
        private void Reset(int loop)
        {
            _from = loop % 2;
            _unlockedAt = float.MaxValue;
            foreach (var slot in _slots)
            {
                Pull(slot, 0f);
                ShowLock(slot, false);
                ShowGuide(slot, null);
            }
            Place(Centre(_from), 0f);
        }

        // ---- The demonstration. Component positions are in the model's space, in life-size metres; the
        // hand's are in the display's, where whoever is watching is out along +Z.

        private static readonly Vector3 Front = Vector3.forward * 0.03f;

        private void Play(float t)
        {
            Slot from = _slots[_from], to = _slots[1 - _from];
            Vector3 start = Centre(_from), end = Centre(1 - _from);
            float depth = _extent.z, height = _extent.y;
            Vector3 clear = Vector3.forward * (depth * 1.15f);            // far enough out to be free of the slot
            Vector3 crooked = new Vector3(0f, height * 0.3f, depth * 0.35f); // inside the empty slot, off-centre
            const float Crooked = 35f;                                      // and turned, in degrees

            // The bar: reach it, pull it up until it turns green, let go and it slides home.
            float pulled = Ease(t, 1.3f, 2.1f) * (1f - Ease(t, 2.2f, 2.4f));
            Pull(from, pulled);
            if (pulled >= 0.8f && _unlockedAt > t) _unlockedAt = t;
            ShowLock(from, t >= _unlockedAt && t < _unlockedAt + _unlockSeconds);
            Vector3 bar = BarAt(from);

            if (t < 2.4f)
            {
                var away = Away();
                _hand.Place(Vector3.Lerp(away, bar, Ease(t, 0.2f, 1.1f)) + Front, Reaching, Ease(t, 0.9f, 1.2f) * (1f - Ease(t, 2.1f, 2.4f)));
                return;
            }

            // The component: take hold, draw it out, carry it across, in crooked, then centre and square it.
            Vector3 at = start;
            float yaw = 0f;
            if (t >= 3.5f)
            {
                at = start + clear * Ease(t, 3.5f, 4.5f);
                at = Vector3.Lerp(at, end + clear + Vector3.up * crooked.y, Ease(t, 4.5f, 5.8f));
                at = Vector3.Lerp(at, end + crooked, Ease(t, 5.8f, 6.6f));
                at = Vector3.Lerp(at, end, Ease(t, 6.8f, 7.8f));
                yaw = Crooked * Ease(t, 4.6f, 6.2f) * (1f - Ease(t, 7.9f, 8.8f));
            }

            if (t < 9.0f)
            {
                Place(at, yaw);
                Guide(to, at, yaw);
                Vector3 grip = Display(at + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * (depth * 0.5f));
                var turn = Quaternion.Euler(0f, yaw, 0f) * Reaching;
                _hand.Place(Vector3.Lerp(bar, grip, Ease(t, 2.4f, 3.3f)) + Front, turn, Ease(t, 3.1f, 3.4f) * (1f - Ease(t, 8.9f, 9.0f)));
                return;
            }

            // Let go on green: it snaps in and the slot locks; the hand leaves.
            Place(end, 0f);
            ShowGuide(to, null);
            Vector3 face = Display(end + Vector3.forward * (depth * 0.5f));
            _hand.Place(Vector3.Lerp(face, Away(), Ease(t, 9.1f, 10.1f)) + Front, Reaching, 0f);
        }

        /// Colours the empty slot's guide by the game's own rules, while the component's centre is inside it.
        private void Guide(Slot slot, Vector3 at, float yaw)
        {
            var local = at - slot.Root.localPosition;
            bool inside = Mathf.Abs(local.x) <= _extent.x * 0.5f && Mathf.Abs(local.y) <= _extent.y * 0.5f && Mathf.Abs(local.z) <= _extent.z * 0.5f;
            if (!inside) { ShowGuide(slot, null); return; }
            bool positioned = local.magnitude <= _seatDistance;
            bool aligned = Mathf.Abs(yaw) <= _seatAngle;
            ShowGuide(slot, positioned && aligned ? slot.Both : positioned || aligned ? slot.OneOf : slot.Neither);
        }

        private static void ShowGuide(Slot slot, Material material)
        {
            if (slot.Guide == null) return;
            slot.Guide.enabled = material != null;
            if (material != null && slot.Guide.sharedMaterial != material) slot.Guide.sharedMaterial = material;
        }

        private static void ShowLock(Slot slot, bool unlocked)
        {
            var material = unlocked ? slot.Unlocked : slot.Locked;
            if (slot.BarRenderer != null && material != null && slot.BarRenderer.sharedMaterial != material) slot.BarRenderer.sharedMaterial = material;
        }

        /// Slides a slot's bar up its track, 0 home to 1 fully pulled.
        private static void Pull(Slot slot, float share)
        {
            if (slot.Bar != null) slot.Bar.localPosition = slot.BarRest + Vector3.up * (slot.Travel * share);
        }

        private void Place(Vector3 at, float yaw)
        {
            _component.localPosition = at;
            _component.localRotation = Quaternion.Euler(0f, yaw, 0f) * _seat;
        }

        private Vector3 Centre(int i) => _slots[i].Root.localPosition;

        /// Where a slot's bar is, in the display's space.
        private Vector3 BarAt(Slot slot) =>
            transform.InverseTransformPoint(slot.Bar != null ? slot.Bar.position : slot.Root.position);

        /// A point in the model's space, in the display's.
        private Vector3 Display(Vector3 model) => transform.InverseTransformPoint(_model.TransformPoint(model));

        /// Where the hand waits: off to the right, in front.
        private Vector3 Away() => new Vector3(_extent.x * _scale + _gap, _extent.y * _scale * 0.1f, 0.45f);

        /// Writes the board once: the title, then every step, to be read at a glance.
        private void WriteBoard()
        {
            if (_caption == null) return;
            var text = new System.Text.StringBuilder($"<b>{Title}</b>\n\n");
            for (int i = 0; i < Steps.Length; i++) text.Append($"{i + 1}. {Steps[i]}\n");
            _caption.SetText(text.ToString());
        }
    }
}
