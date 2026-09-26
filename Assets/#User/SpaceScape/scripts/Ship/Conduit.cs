using SomniumSpace.Worlds.SpaceScape.Power;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Cable occupying a single tile. It carries no topology of its own — the GridNode beside it
    /// declares what it feeds — and it exists to show what is passing through it.
    ///
    /// It draws one stub per face the tile actually uses, running from the tile's centre out to that
    /// face. Two opposite faces meet in the middle and read as one straight run; two faces at right
    /// angles read as a corner; three read as a tee. No case is special.
    [ExecuteAlways]
    [RequireComponent(typeof(GridNode))]
    public sealed class Conduit : MonoBehaviour
    {
        [SerializeField] private Transform[] _stubs = new Transform[0];
        [SerializeField] private Renderer _bodyRenderer;
        [SerializeField] private float _bodyLengthAtScaleOne = 2f;   // Unity's cylinder primitive
        [SerializeField] private float _tileLength = 1f;

        [Header("Appearance")]
        [SerializeField] private double _referenceWatts = 100.0;
        [SerializeField] private Color _idle = new Color(0.16f, 0.18f, 0.2f);
        [SerializeField] private Color _loaded = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color _open = new Color(0.35f, 0.08f, 0.08f);
        [SerializeField] private Color _wasting = new Color(1f, 0.25f, 0.1f);

        [Header("Naming")]
        [SerializeField] private bool _autoName = true;
        [SerializeField] private string _namePrefix = "Conduit";

        [Header("Readout")]
        [SerializeField] private TMP_Text _readout;
        [SerializeField] private float _readoutInterval = 0.25f;

        private GridNode _tile;
        private MaterialPropertyBlock _block;
        private float _nextReadout;

        /// The tile this cable occupies.
        public GridNode Tile => _tile != null ? _tile : _tile = GetComponent<GridNode>();

        /// True while this tile is switched off and the graph is skipping it.
        public bool IsOpen => Tile != null && !Tile.On;

        private void Awake()
        {
            _tile = GetComponent<GridNode>();
        }

        private void LateUpdate()
        {
            Rename();
            Fit();
            Tint();
            Readout();
        }

        /// Puts a stub on every face the tile uses, each running from the centre out to its face.
        private void Fit()
        {
            if (_stubs == null || _stubs.Length == 0) return;

            int used = 0;
            float half = _tileLength * 0.5f;

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

        private static readonly GridDirection[] NoFaces = new GridDirection[0];

        /// Keeps the name matching where the tile actually sits, so a line in the log says where to
        /// look. Named for the grid coordinate rather than the transform, because that is what the
        /// graph reports and what a neighbour is worked out from.
        private void Rename()
        {
            if (!_autoName || Tile == null) return;

            var at = Tile.Coordinate;
            string want = $"{_namePrefix}_{at.x}_{at.y}_{at.z}";
            if (name != want) name = want;
        }

        /// Colours the cable by what passes through it, and flags a tile that is wasting power.
        private void Tint()
        {
            if (_stubs == null || _stubs.Length == 0) return;

            var node = Tile != null ? Tile.Node : null;
            Color colour;

            if (IsOpen) colour = _open;
            else if (node == null) colour = _idle;
            else if (node.Dumped > 0.01) colour = _wasting;
            else
            {
                float load = _referenceWatts > 0.0 ? Mathf.Clamp01((float)(node.Inflow / _referenceWatts)) : 0f;
                colour = Color.Lerp(_idle, _loaded, load);
            }

            if (_block == null) _block = new MaterialPropertyBlock();
            _block.SetColor("_BaseColor", colour);
            _block.SetColor("_EmissionColor", colour);
            foreach (var stub in _stubs)
            {
                if (stub == null) continue;
                var r = stub.GetComponent<Renderer>();
                if (r != null) r.SetPropertyBlock(_block);
            }
        }

        /// Writes what it is carrying, and does no string work at all while the label is switched off.
        private void Readout()
        {
            if (!Application.isPlaying) return;          // no live numbers to show, and it would dirty the scene
            if (_readout == null || !_readout.enabled) return;
            if (Time.time < _nextReadout) return;
            _nextReadout = Time.time + _readoutInterval;

            var node = Tile != null ? Tile.Node : null;
            if (node == null) { _readout.SetText("unbound"); return; }
            if (IsOpen) { _readout.SetText($"open\n{node.Celsius:0.0} C"); return; }
            _readout.SetText(node.Dumped > 0.01
                ? $"{node.Inflow:0} W\nWASTE {node.Dumped:0} W\n{node.Celsius:0.0} C"
                : $"{node.Inflow:0} W\n{node.Celsius:0.0} C");
        }

        private void OnValidate()
        {
            _tile = GetComponent<GridNode>();
            if (_bodyRenderer == null) _bodyRenderer = GetComponentInChildren<Renderer>(true);
            Fit();
        }
    }
}
