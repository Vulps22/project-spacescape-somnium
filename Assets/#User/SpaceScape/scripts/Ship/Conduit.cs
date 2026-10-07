using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Cable occupying a single tile. It carries no topology of its own — the GridNode beside it
    /// declares what it feeds — and it exists to show what is passing through it.
    ///
    /// It draws one stub per face the tile actually uses, running from the tile's centre out to that
    /// face. Two opposite faces meet in the middle and read as one straight run; two faces at right
    /// angles read as a corner; three read as a tee. No case is special. A parked conduit uses no face,
    /// so it draws nothing.
    ///
    /// Nothing here runs per frame (docs/conduits.md, When a conduit updates). The stubs are rebuilt when
    /// the faces or the addon change; the colour is looked at when the grid ticks, and a stub's material is
    /// swapped only when its colour band changes. Each band is one material shared by every conduit, so
    /// URP batches them.
    ///
    /// On a data connection between two or more components (DataGrid.Links), a conduit carrying no power glows
    /// Data blue, and one carrying power keeps its colour with thin Data blue lines along each segment. Worked
    /// out on every client from the same data network: nothing is sent.
    [RequireComponent(typeof(GridNode))]
    public sealed class Conduit : MonoBehaviour
    {
        [Tooltip("Cable pieces, one per face this tile uses. Spares are hidden.")]
        [SerializeField] private Transform[] _stubs = new Transform[0];
        [Tooltip("Renderer of the cable body. Found automatically.")]
        [SerializeField] private Renderer _bodyRenderer;
        [Tooltip("Length of the stub mesh at scale 1, in metres (2 for Unity's cylinder).")]
        [SerializeField] private float _bodyLengthAtScaleOne = 2f;   // Unity's cylinder primitive

        [Header("Appearance")]
        [Tooltip("Watts at which the cable shows fully lit. Less shows proportionally dimmer.")]
        [SerializeField] private double _referenceWatts = 100.0;
        [Tooltip("How many steps the colour takes from carrying nothing to Reference Watts. Fewer steps, fewer materials and fewer swaps.")]
        [SerializeField, Range(2, 32)] private int _bands = 8;
        [Tooltip("Colour when carrying nothing.")]
        [SerializeField] private Color _idle = new Color(0.16f, 0.18f, 0.2f);
        [Tooltip("Colour at Reference Watts.")]
        [SerializeField] private Color _loaded = new Color(1f, 0.85f, 0.3f);
        [Tooltip("Colour while this tile is switched off.")]
        [SerializeField] private Color _open = new Color(0.35f, 0.08f, 0.08f);
        [Tooltip("Colour while this tile is dumping power as heat.")]
        [SerializeField] private Color _wasting = new Color(1f, 0.25f, 0.1f);
        [Tooltip("Colour of a data connection: the whole conduit while it carries data and no power, its lines while it carries both.")]
        [SerializeField] private Color _data = new Color(0.15f, 0.45f, 1f);
        [Tooltip("Thickness of the data lines along a segment carrying power, in metres.")]
        [SerializeField] private float _dataLineWidth = 0.006f;

        [Header("Data pulse (debugging)")]
        [Tooltip("Ticked: a conduit whose data network reaches a component's data port, with nothing yet on the other end, pulses Data blue, so where data would go can be seen.")]
        [SerializeField] private bool _pulseWhereDataReaches = true;
        [Tooltip("Seconds a pulse takes from the conduit's own colour to Data blue.")]
        [SerializeField] private float _pulseUpSeconds = 2.5f;
        [Tooltip("Seconds it takes back again.")]
        [SerializeField] private float _pulseDownSeconds = 2.5f;
        [Tooltip("Seconds at its own colour before the next pulse.")]
        [SerializeField] private float _pulseRestSeconds = 3f;

        [Header("Naming")]
        [Tooltip("Renames the object after its grid position, so log lines say where to look.")]
        [SerializeField] private bool _autoName = true;
        [Tooltip("Text before the coordinates in the automatic name.")]
        [SerializeField] private string _namePrefix = "Conduit";

        private GridNode _tile;
        private NodeEdgeModule _edges;
        private AddonModule _addon;
        private PowerGrid _grid;
        private Material _source;
        private int _band = int.MinValue;

        private const int OpenBand = -1;
        private const int WastingBand = -2;
        private const int DataBand = -3;
        private const int LinesPerStub = 4;   // round each segment, so one shows from any side

        private bool _carriesData;
        private bool _pulsing;
        private bool _pulsingShown;
        private bool _linesShown;
        private Transform[] _lines = new Transform[0];

        // Every band's material, made once from a stub's own material and shared by every conduit using it.
        private static readonly System.Collections.Generic.Dictionary<(Material, int), Material> Shared =
            new System.Collections.Generic.Dictionary<(Material, int), Material>();

        // Each band's pulsing twin, and its own colour, animated together once a frame so every pulse is in step.
        private static readonly System.Collections.Generic.Dictionary<(Material, int), Material> Pulsing =
            new System.Collections.Generic.Dictionary<(Material, int), Material>();
        private static readonly System.Collections.Generic.List<(Material material, Color own)> PulseColours =
            new System.Collections.Generic.List<(Material, Color)>();
        private static int _pulsedFrame = -1;

        /// The tile this cable occupies.
        public GridNode Tile => _tile != null ? _tile : _tile = GetComponent<GridNode>();

        /// True while this tile is switched off and the graph is skipping it.
        public bool IsOpen => Tile != null && !Tile.On;

        private void Awake()
        {
            _tile = GetComponent<GridNode>();
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            if (_stubs.Length > 0 && _stubs[0] != null && _stubs[0].TryGetComponent<Renderer>(out var first)) _source = first.sharedMaterial;

            Rename();
            _edges = Tile.Edges;
            if (_edges != null) _edges.Changed += Reshape;
            if (TryGetComponent(out _addon)) _addon.Changed += Reshape;
            _grid = PowerGrid.Current;
            if (_grid != null) _grid.Ticked += OnTicked;
            Reshape();
        }

        private void OnDestroy()
        {
            if (_edges != null) _edges.Changed -= Reshape;
            if (_addon != null) _addon.Changed -= Reshape;
            if (_grid != null) _grid.Ticked -= OnTicked;
        }

        /// The faces or the addon changed: lay the stubs out again, and colour any new ones.
        private void Reshape()
        {
            Fit();
            _band = int.MinValue;
            Recolour();
            if (_linesShown) PlaceDataLines();
        }

        private void OnTicked()
        {
            if (!HasStubShowing()) return;   // parked: nothing to colour
            _carriesData = _grid != null && _grid.Data.Links(Tile);
            _pulsing = _pulseWhereDataReaches && !_carriesData && _grid != null && _grid.Data.Reaches(Tile);
            Recolour();
            if (_pulsing) AnimatePulses();
            ShowDataLines(_carriesData && _band != DataBand);
        }

        private bool HasStubShowing()
        {
            foreach (var stub in _stubs)
                if (stub != null && stub.gameObject.activeSelf) return true;
            return false;
        }

        /// Puts a stub on every face the tile uses, each running from the centre out to its face.
        private void Fit()
        {
            if (_stubs == null || _stubs.Length == 0) return;
            GrowStubs();

            int used = 0;
            float half = GridCell.Half;

            foreach (var face in Tile != null ? Tile.UsedFaces : NoFaces)
            {
                if (used >= _stubs.Length) break;                 // more faces than stubs authored
                var stub = _stubs[used];
                if (stub == null) { used++; continue; }

                Vector3 along = face.Vector();
                if (along.sqrMagnitude < 0.0001f) continue;

                // Written only when it actually differs: this runs in the editor, and setting a
                // transform to the value it already holds still marks the scene dirty.
                if (!stub.gameObject.activeSelf) stub.gameObject.SetActive(true);

                Vector3 want = transform.position + along * (half * 0.5f);
                if ((stub.position - want).sqrMagnitude > 1e-8f) stub.position = want;

                if (Vector3.Dot(stub.up, along) < 0.999999f) stub.up = along;

                Vector3 scale = stub.localScale;
                float wantY = half / _bodyLengthAtScaleOne;
                if (Mathf.Abs(scale.y - wantY) > 1e-6f)
                    stub.localScale = new Vector3(scale.x, wantY, scale.z);

                used++;
            }

            for (int i = used; i < _stubs.Length; i++)
                if (_stubs[i] != null && _stubs[i].gameObject.activeSelf)
                    _stubs[i].gameObject.SetActive(false);
        }

        /// Adds stubs, copies of the first, while the tile uses more faces than were authored: a junction's
        /// extra segments. Without them the faces past the last stub went undrawn, and since outputs are drawn
        /// first, a new output pushed an input off the end. Play mode only, so nothing is added while editing.
        private void GrowStubs()
        {
            if (!Application.isPlaying || Tile == null || _stubs[0] == null) return;
            int faces = 0;
            foreach (var _ in Tile.UsedFaces) faces++;
            if (faces <= _stubs.Length) return;

            int had = _stubs.Length;
            System.Array.Resize(ref _stubs, faces);
            for (int i = had; i < faces; i++)
            {
                _stubs[i] = Instantiate(_stubs[0], _stubs[0].parent, false);
                _stubs[i].name = $"{_stubs[0].name} {i}";
            }
        }

        private static readonly GridDirection[] NoFaces = new GridDirection[0];

        /// Names it after the cell it sits in, once, so a line in the log says where to look. Named for the
        /// grid coordinate rather than the transform, because that is what the graph reports and what a
        /// neighbour is worked out from. A conduit never moves once built.
        private void Rename()
        {
            if (!_autoName || Tile == null) return;
            var at = Tile.Coordinate;
            name = $"{_namePrefix}_{at.x}_{at.y}_{at.z}";
        }

        /// Which colour band it is in now: open, wasting, or how much it carries in steps.
        private int CurrentBand()
        {
            var node = Tile != null ? Tile.Node : null;
            if (IsOpen) return OpenBand;
            if (node == null) return 0;
            if (node.Dumped > 0.01) return WastingBand;
            if (_carriesData && node.Inflow < 0.01) return DataBand;
            float load = _referenceWatts > 0.0 ? Mathf.Clamp01((float)(node.Inflow / _referenceWatts)) : 0f;
            return Mathf.RoundToInt(load * (_bands - 1));
        }

        /// Puts the band's shared material on every stub, when the band has changed.
        private void Recolour()
        {
            if (_source == null) return;
            int band = CurrentBand();
            bool pulsing = _pulsing && band != OpenBand && band != WastingBand;
            if (band == _band && pulsing == _pulsingShown) return;
            _band = band;
            _pulsingShown = pulsing;

            var material = pulsing ? PulsingFor(band) : MaterialFor(band);
            foreach (var stub in _stubs)
                if (stub != null && stub.TryGetComponent<Renderer>(out var r) && r.sharedMaterial != material)
                    r.sharedMaterial = material;
        }

        private Material MaterialFor(int band)
        {
            if (Shared.TryGetValue((_source, band), out var made) && made != null) return made;
            Color colour = band == OpenBand ? _open
                : band == WastingBand ? _wasting
                : band == DataBand ? _data
                : Color.Lerp(_idle, _loaded, band / (float)(_bands - 1));
            made = new Material(_source) { name = $"{_source.name} ({band})" };
            made.SetColor("_BaseColor", colour);
            made.SetColor("_EmissionColor", colour);
            Shared[(_source, band)] = made;
            return made;
        }

        /// A band's pulsing twin: its own colour to Data blue and back, animated by AnimatePulses.
        private Material PulsingFor(int band)
        {
            if (Pulsing.TryGetValue((_source, band), out var made) && made != null) return made;
            var own = MaterialFor(band);
            made = new Material(own) { name = $"{own.name} (data pulse)" };
            Pulsing[(_source, band)] = made;
            PulseColours.Add((made, own.GetColor("_BaseColor")));
            return made;
        }

        /// Sets every pulsing material's colour for this moment, once a frame however many conduits pulse:
        /// up to Data blue over Pulse Up Seconds, back over Pulse Down Seconds, then a rest.
        private void AnimatePulses()
        {
            if (_pulsedFrame == Time.frameCount) return;
            _pulsedFrame = Time.frameCount;
            float up = Mathf.Max(0.01f, _pulseUpSeconds), down = Mathf.Max(0.01f, _pulseDownSeconds);
            float t = Time.time % (up + down + Mathf.Max(0f, _pulseRestSeconds));
            float k = t < up ? t / up : t < up + down ? 1f - (t - up) / down : 0f;
            k = Mathf.SmoothStep(0f, 1f, k);
            foreach (var (material, own) in PulseColours)
            {
                if (material == null) continue;
                var colour = Color.Lerp(own, _data, k);
                material.SetColor("_BaseColor", colour);
                material.SetColor("_EmissionColor", colour);
            }
        }

        /// Shows or hides the thin data lines along each segment, when that changes.
        private void ShowDataLines(bool show)
        {
            if (show == _linesShown) return;
            _linesShown = show;
            if (show) PlaceDataLines();
            else foreach (var line in _lines) if (line != null) line.gameObject.SetActive(false);
        }

        /// Lays four thin lines along every showing segment, just off its surface, made the first time they are
        /// needed. They hang from the conduit, not the stub, so the stub's stretched scale does not bend them.
        private void PlaceDataLines()
        {
            if (_source == null) return;
            int wanted = _stubs.Length * LinesPerStub;
            if (_lines.Length < wanted)
            {
                int had = _lines.Length;
                System.Array.Resize(ref _lines, wanted);
                for (int i = had; i < wanted; i++) _lines[i] = MakeDataLine(i);
            }

            var material = MaterialFor(DataBand);
            for (int s = 0; s < _stubs.Length; s++)
            {
                var stub = _stubs[s];
                bool showing = stub != null && stub.gameObject.activeSelf;
                Vector3 along = showing ? stub.up : Vector3.up;
                Vector3 side = Mathf.Abs(along.y) > 0.5f ? Vector3.forward : Vector3.up;
                float radius = showing ? Mathf.Max(stub.lossyScale.x, stub.lossyScale.z) * 0.5f : 0f;
                float length = showing ? stub.lossyScale.y * _bodyLengthAtScaleOne : 0f;
                for (int k = 0; k < LinesPerStub; k++)
                {
                    var line = _lines[s * LinesPerStub + k];
                    if (line == null) continue;
                    line.gameObject.SetActive(showing);
                    if (!showing) continue;
                    Vector3 off = Quaternion.AngleAxis(k * 360f / LinesPerStub, along) * side;
                    line.SetPositionAndRotation(stub.position + off * (radius + _dataLineWidth * 0.5f),
                        Quaternion.LookRotation(off, along));
                    line.localScale = new Vector3(_dataLineWidth, length, _dataLineWidth);
                    if (line.TryGetComponent<Renderer>(out var r) && r.sharedMaterial != material) r.sharedMaterial = material;
                }
            }
        }

        private Transform MakeDataLine(int index)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(line.GetComponent<Collider>());
            line.name = $"Data Line {index}";
            line.transform.SetParent(transform, false);
            if (line.TryGetComponent<Renderer>(out var r))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            line.SetActive(false);
            return line.transform;
        }

        private void OnValidate()
        {
            _tile = GetComponent<GridNode>();
            if (_bodyRenderer == null) _bodyRenderer = GetComponentInChildren<Renderer>(true);
            Fit();
        }
    }
}
