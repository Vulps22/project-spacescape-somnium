using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The radar, phase 1 (docs/radar.md). Sees every DetectableModule within Range and plots them on its screen,
    /// top down: the back of its slot is the top of the screen. Each contact is drawn as an arrowhead turned to
    /// the way it faces (its local x+). Previous and Next move a cursor (amber) through the contacts; Select makes
    /// the cursor's contact the selected target: a square round its marker, with lines from the square to the
    /// screen's edges. A lever under the buttons zooms: fully left shows all of Range, fully right Zoomed In
    /// Range, on a log scale. Works only while powered: a LoadBehaviourModule beside it must be working.
    ///
    /// Networking (ComponentNetwork): the cursor, the selection and the zoom are the radar's state, written by
    /// whoever decides; a button pressed or the lever moved on another client is sent there. The contacts need nothing sent: every client
    /// works them out from the same targets.
    [RequireComponent(typeof(LoadBehaviourModule))]
    public sealed class Radar : MonoBehaviour, INetworkedState, ILeverDial
    {
        [Header("Seeing")]
        [Tooltip("How far it sees, in metres. The edge of the screen's height is this far.")]
        [SerializeField] private float _range = 1000f;
        [Tooltip("What the screen's height covers fully zoomed in (the zoom lever fully right), in metres.")]
        [SerializeField] private float _zoomedInRange = 100f;

        [Header("Screen")]
        [Tooltip("The face contacts are drawn on: its centre, facing out of the screen (+Z), unscaled.")]
        [SerializeField] private Transform _display;
        [Tooltip("How wide and tall the drawable face is, in metres.")]
        [SerializeField] private Vector2 _displaySize = new Vector2(0.95f, 0.62f);
        [Tooltip("A contact's arrowhead, flat, facing +Z, tip toward +Y, about one unit across.")]
        [SerializeField] private Mesh _contactMesh;
        [Tooltip("A flat square one unit across, facing +Z, for the selection's square and lines.")]
        [SerializeField] private Mesh _quadMesh;
        [Tooltip("How big a contact is drawn, in metres.")]
        [SerializeField] private float _contactSize = 0.04f;
        [Tooltip("A contact's material.")]
        [SerializeField] private Material _contactMaterial;
        [Tooltip("The cursor's contact's material.")]
        [SerializeField] private Material _cursorMaterial;
        [Tooltip("The selection's square and lines.")]
        [SerializeField] private Material _selectionMaterial;
        [Tooltip("Thickness of the selection's square and lines, in metres.")]
        [SerializeField] private float _lineWidth = 0.004f;

        [Header("Buttons")]
        [SerializeField] private PushButton _previous;
        [SerializeField] private PushButton _select;
        [SerializeField] private PushButton _next;

        private readonly List<DetectableModule> _contacts = new List<DetectableModule>();
        private readonly List<MeshRenderer> _markers = new List<MeshRenderer>();
        private readonly Transform[] _lines = new Transform[8];
        private LoadBehaviourModule _load;
        private int _cursor = -1;
        private int _selected = -1;
        private float _zoom;
        private bool _zoomHeld;

        /// How far the screen's top and bottom edges reach now, in metres: Range fully zoomed out, Zoomed In
        /// Range fully in, evenly by ratio between.
        public float ShownRange => _range * Mathf.Pow(Mathf.Max(1f, _zoomedInRange) / Mathf.Max(1f, _range), _zoom);

        /// Raised when a hand here moves the zoom lever, for the network to send to whoever decides.
        public event Action<float> ZoomMoved;

        // The zoom lever's one dial: 0 fully zoomed out, 1 fully in.
        public float GetDial(int axis) => _zoom;
        public void SetDialHeld(int axis, bool held) => _zoomHeld = held;
        public void TurnDial(int axis, float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, _zoom)) return;
            _zoom = value;
            ZoomMoved?.Invoke(_zoom);
        }

        /// Sets the zoom, for the decider taking a hand's from another client.
        public void SetZoom(float zoom) => _zoom = Mathf.Clamp01(zoom);

        /// The contact the cursor is on, by id, or -1.
        public int Cursor => _cursor;

        /// The selected target, by id, or -1.
        public int Selected => _selected;

        /// True while it is powered and seeing.
        public bool On => _load != null && _load.Working;

        /// Everything it sees now, nearest id first. Empty while it is off.
        public IReadOnlyList<DetectableModule> Contacts => _contacts;

        /// True where this client moves the cursor and selects. Set by the network; true with no network.
        public bool Decides { get; set; } = true;

        /// Raised on a client that does not decide when a button is pressed: -1 Previous, +1 Next, 0 Select.
        public event Action<int> ButtonRequested;

        private void Awake()
        {
            _load = GetComponent<LoadBehaviourModule>();
            if (_display != null)
                for (int i = 0; i < _lines.Length; i++) _lines[i] = MakePiece($"Selection {i}", _quadMesh, _selectionMaterial).transform;
        }

        private void OnEnable()
        {
            if (_previous != null) _previous.Pressed += OnPrevious;
            if (_next != null) _next.Pressed += OnNext;
            if (_select != null) _select.Pressed += OnSelect;
        }

        private void OnDisable()
        {
            if (_previous != null) _previous.Pressed -= OnPrevious;
            if (_next != null) _next.Pressed -= OnNext;
            if (_select != null) _select.Pressed -= OnSelect;
        }

        private void OnPrevious() => Press(-1);
        private void OnNext() => Press(1);
        private void OnSelect() => Press(0);

        private void Press(int button)
        {
            if (!On) return;
            if (Decides) Apply(button);
            else ButtonRequested?.Invoke(button);
        }

        /// Moves the cursor (-1, +1) or selects what it is on (0). Where it decides; everyone else is told.
        public void Apply(int button)
        {
            See();
            if (button == 0)
            {
                _selected = _cursor;
                return;
            }
            if (_contacts.Count == 0) { _cursor = -1; return; }
            int at = -1;
            for (int i = 0; i < _contacts.Count; i++) if (_contacts[i].Id == _cursor) at = i;
            if (at < 0) at = button > 0 ? -1 : 0;
            at = ((at + button) % _contacts.Count + _contacts.Count) % _contacts.Count;
            _cursor = _contacts[at].Id;
        }

        private void Update()
        {
            See();
            Draw();
        }

        /// Everything detectable within range, in id order, so stepping through them goes the same way everywhere.
        private void See()
        {
            _contacts.Clear();
            if (!On) return;
            float rangeSquared = _range * _range;
            foreach (var detectable in DetectableModule.All)
                if (detectable != null && detectable.Id >= 0 && (detectable.Position - transform.position).sqrMagnitude <= rangeSquared)
                    _contacts.Add(detectable);
            _contacts.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        /// Where a point in the world is drawn on the display, in the display's own space: top down, the back of
        /// the slot up the screen, out to Range at the screen's top and bottom edges.
        private Vector3 OnScreen(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            float scale = _displaySize.y * 0.5f / Mathf.Max(1f, ShownRange);
            // Seen from in front, the back of the slot (-z) is up and the viewer's right is -x. The display's own
            // x runs the other way to the viewer's right, so its x is the radar's x.
            return new Vector3(local.x * scale, -local.z * scale, 0f);
        }

        private void Draw()
        {
            if (_display == null) return;
            int shown = 0;
            DetectableModule selected = null;
            foreach (var contact in _contacts)
            {
                var at = OnScreen(contact.Position);
                if (Mathf.Abs(at.x) > _displaySize.x * 0.5f || Mathf.Abs(at.y) > _displaySize.y * 0.5f) continue;
                if (shown >= _markers.Count) _markers.Add(MakePiece($"Contact {_markers.Count}", _contactMesh, _contactMaterial));
                var marker = _markers[shown++];
                marker.gameObject.SetActive(true);
                var facing = transform.InverseTransformDirection(contact.Forward);
                var up = new Vector3(facing.x, -facing.z, 0f);
                marker.transform.localPosition = at + new Vector3(0f, 0f, 0.001f);   // just off the screen, toward the viewer
                marker.transform.localRotation = up.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(Vector3.forward, up) : Quaternion.identity;
                marker.transform.localScale = Vector3.one * _contactSize;
                var material = contact.Id == _cursor ? _cursorMaterial : _contactMaterial;
                if (material != null && marker.sharedMaterial != material) marker.sharedMaterial = material;
                if (contact.Id == _selected) selected = contact;
            }
            for (int i = shown; i < _markers.Count; i++)
                if (_markers[i].gameObject.activeSelf) _markers[i].gameObject.SetActive(false);
            DrawSelection(selected);
        }

        /// A square round the selected contact, and lines from each of its sides out to the screen's edges.
        private void DrawSelection(DetectableModule selected)
        {
            bool show = selected != null;
            foreach (var line in _lines) if (line != null && line.gameObject.activeSelf != show) line.gameObject.SetActive(show);
            if (!show) return;

            var at = OnScreen(selected.Position);
            float half = _contactSize * 0.9f, w = _lineWidth;
            float left = -_displaySize.x * 0.5f, right = _displaySize.x * 0.5f, bottom = -_displaySize.y * 0.5f, top = _displaySize.y * 0.5f;
            // The square.
            Bar(0, at.x - half, at.x + half, at.y + half - w * 0.5f, at.y + half + w * 0.5f);
            Bar(1, at.x - half, at.x + half, at.y - half - w * 0.5f, at.y - half + w * 0.5f);
            Bar(2, at.x - half - w * 0.5f, at.x - half + w * 0.5f, at.y - half, at.y + half);
            Bar(3, at.x + half - w * 0.5f, at.x + half + w * 0.5f, at.y - half, at.y + half);
            // From it to the edges.
            Bar(4, left, at.x - half, at.y - w * 0.5f, at.y + w * 0.5f);
            Bar(5, at.x + half, right, at.y - w * 0.5f, at.y + w * 0.5f);
            Bar(6, at.x - w * 0.5f, at.x + w * 0.5f, bottom, at.y - half);
            Bar(7, at.x - w * 0.5f, at.x + w * 0.5f, at.y + half, top);
        }

        private void Bar(int i, float x0, float x1, float y0, float y1)
        {
            var t = _lines[i];
            if (t == null) return;
            t.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0.002f);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(Mathf.Max(0.0001f, x1 - x0), Mathf.Max(0.0001f, y1 - y0), 1f);
        }

        private MeshRenderer MakePiece(string name, Mesh mesh, Material material)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(_display, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            piece.SetActive(false);
            return renderer;
        }

        public int StateCount => 3;
        public void WriteState(float[] to, int at) { to[at] = _cursor; to[at + 1] = _selected; to[at + 2] = _zoom; }
        public void ReadState(float[] from, int at)
        {
            _cursor = Mathf.RoundToInt(from[at]);
            _selected = Mathf.RoundToInt(from[at + 1]);
            if (!_zoomHeld) _zoom = Mathf.Clamp01(from[at + 2]);
        }
    }
}
