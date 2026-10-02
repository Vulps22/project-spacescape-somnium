using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The panel in front of a conduit container. A hand passed through any of its sockets (pulsing cubes,
    /// like a switch's lock; a pillar has one on each face) opens it: the panel smokes away to nothing, and the conduits behind it can be reached.
    /// It stays open until nobody is left in its trigger box (anything on the Player layer), then smokes back
    /// in. While it is shut, the conduits it covers cannot be opened.
    ///
    /// Whoever decides (the master, or anyone with no network) opens and shuts it; everyone else asks
    /// (OpenRequested) and follows (SetOpen). The smoke plays on every client from that one state.
    ///
    /// While editing, each socket shows a copy of the socket prefab, so they can be placed by eye. The copies
    /// are never saved.
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AccessPanel : MonoBehaviour
    {
        private const int MaxHands = 8;

        [Header("Parts")]
        [Tooltip("The panel that smokes away: every renderer and collider under it goes.")]
        [SerializeField] private GameObject _panel;
        [Tooltip("Where the sockets sit, one on each face it can be opened from. Any of them opens it.")]
        [SerializeField] private Transform[] _sockets = new Transform[0];
        [Tooltip("The pulsing cube shown at the socket while the panel is shut: the same prefab as a switch's unlock cube. Should have no collider.")]
        [SerializeField] private GameObject _socketPrefab;

        [Header("Socket")]
        [Tooltip("How close a hand must come to the socket's centre to pass through it, in metres.")]
        [SerializeField] private float _socketRadius = 0.06f;
        [Tooltip("Seconds for one pulse of the socket, from small to large and back.")]
        [SerializeField] private float _pulseSeconds = 1.2f;
        [Tooltip("How much the socket grows at the top of its pulse: 0.2 is 20%.")]
        [SerializeField] private float _pulse = 0.2f;

        [Header("Smoke")]
        [Tooltip("The SpaceScape/Dissolve material. The panel's own textures are copied onto it while it smokes away or back.")]
        [SerializeField] private Material _dissolveMaterial;
        [Tooltip("Seconds the panel takes to smoke away, and to come back.")]
        [SerializeField] private float _dissolveSeconds = 1.2f;
        [Tooltip("Played as the panel starts to smoke away or back. Optional.")]
        [SerializeField] private ParticleSystem _smoke;

        [Header("Closing")]
        [Tooltip("Seconds it stays open at least, so it does not shut on someone who opened it from just outside the box.")]
        [SerializeField] private float _minimumOpenSeconds = 3f;

        private static readonly List<AccessPanel> Panels = new List<AccessPanel>();
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        private readonly HashSet<Collider> _inside = new HashSet<Collider>();
        private readonly HashSet<Vector3Int> _covers = new HashSet<Vector3Int>();
        private readonly Vector3[] _hands = new Vector3[MaxHands];
        private Renderer[] _renderers = new Renderer[0];
        private Material[][] _ownMaterials = new Material[0][];
        private Material[][] _smokeMaterials;
        private Collider[] _colliders = new Collider[0];
        private readonly List<Transform> _cubes = new List<Transform>();
        private Vector3 _cubeScale;
        private int _playerLayer;
        private float _openedAt;
        private float _gone;
        private bool _wearingSmoke;

        /// Every panel in the world.
        public static IReadOnlyList<AccessPanel> All => Panels;

        /// True while the panel is open: everyone sees it smoked away, and the conduits behind it can be reached.
        public bool Open { get; private set; }

        /// True where this client opens and shuts it: the master, or anyone with no network. Set by the network.
        public bool Decides { get; set; } = true;

        /// Raised on a client that does not decide when its hand passes through the socket, for the network to ask.
        public event Action OpenRequested;

        /// True when a shut panel stands in front of this cell, so the conduit in it cannot be opened.
        public static bool Blocks(Vector3Int cell)
        {
            foreach (var panel in Panels)
                if (panel != null && !panel.Open && panel._covers.Contains(cell)) return true;
            return false;
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            _playerLayer = LayerMask.NameToLayer("Player");
            var box = GetComponent<BoxCollider>();
            if (!box.isTrigger) box.isTrigger = true;

            if (_panel != null)
            {
                _renderers = _panel.GetComponentsInChildren<Renderer>(true);
                _colliders = _panel.GetComponentsInChildren<Collider>(true);
                _ownMaterials = new Material[_renderers.Length][];
                for (int i = 0; i < _renderers.Length; i++) _ownMaterials[i] = _renderers[i].sharedMaterials;
            }
            MakeCubes(HideFlags.None);
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            if (TryGetComponent<ConduitLayoutDefinition>(out var definition))
                foreach (var cell in definition.WorldCells) _covers.Add(cell);
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Panels.Add(this);
        }

        private void OnDisable()
        {
            if (Application.isPlaying) Panels.Remove(this);
            else ClearCubes();
        }

        /// A copy of the socket prefab on each socket. Hidden and never saved while editing.
        private void MakeCubes(HideFlags flags)
        {
            ClearCubes();
            if (_socketPrefab == null) return;
            foreach (var socket in _sockets)
            {
                if (socket == null) continue;
                // A preview left over from editing (never saved, but it can outlive a reload) goes first.
                for (int c = socket.childCount - 1; c >= 0; c--)
                {
                    var old = socket.GetChild(c).gameObject;
                    if ((old.hideFlags & HideFlags.DontSave) == 0) continue;
                    if (Application.isPlaying) Destroy(old);
                    else DestroyImmediate(old);
                }
                var cube = Instantiate(_socketPrefab, socket, false);
                cube.hideFlags = flags;
                foreach (var child in cube.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = flags;
                _cubes.Add(cube.transform);
                _cubeScale = cube.transform.localScale;
            }
        }

        private void ClearCubes()
        {
            foreach (var cube in _cubes)
                if (cube != null)
                {
                    if (Application.isPlaying) Destroy(cube.gameObject);
                    else DestroyImmediate(cube.gameObject);
                }
            _cubes.Clear();
        }

        /// Keeps one preview copy on each socket while editing, remade when the sockets or prefab change.
        private void Preview()
        {
            int wanted = 0;
            foreach (var socket in _sockets) if (socket != null) wanted++;
            if (_socketPrefab == null) wanted = 0;
            bool stale = _cubes.Count != wanted;
            int i = 0;
            foreach (var socket in _sockets)
            {
                if (stale || socket == null || _socketPrefab == null) continue;
                if (_cubes[i] == null || _cubes[i].parent != socket) stale = true;
                i++;
            }
            if (stale) MakeCubes(HideFlags.HideAndDontSave);
        }

        private void OnDestroy()
        {
            if (_smokeMaterials == null) return;
            foreach (var set in _smokeMaterials)
                foreach (var material in set) Destroy(material);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == _playerLayer) _inside.Add(other);
        }

        private void OnTriggerExit(Collider other) => _inside.Remove(other);

        /// True while anything on the Player layer is inside the box. A collider switched off or destroyed
        /// inside it never says it left, so those are dropped here.
        public bool Occupied
        {
            get
            {
                _inside.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
                return _inside.Count > 0;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                Preview();
                return;
            }
            PulseSocket();
            if (!Open && HandInSocket())
            {
                if (Decides) SetOpen(true);
                else OpenRequested?.Invoke();
            }
            if (Decides && Open && Time.time >= _openedAt + _minimumOpenSeconds && !Occupied) SetOpen(false);
            Animate();
        }

        /// Opens or shuts it. Where this client decides; everyone else is told what was decided.
        public void SetOpen(bool open)
        {
            if (open == Open) return;
            Open = open;
            if (open) _openedAt = Time.time;
            if (_smoke != null) _smoke.Play();
            // Hands can reach in as soon as it starts to go; it is solid again only once it is all back.
            if (open) SetColliders(false);
        }

        private void PulseSocket()
        {
            bool show = !Open && _gone <= 0f;
            float pulse = 1f + _pulse * 0.5f * (1f - Mathf.Cos(Time.time * 2f * Mathf.PI / Mathf.Max(0.01f, _pulseSeconds)));
            foreach (var cube in _cubes)
            {
                if (cube == null) continue;
                if (cube.gameObject.activeSelf != show) cube.gameObject.SetActive(show);
                if (show) cube.localScale = _cubeScale * pulse;
            }
        }

        private bool HandInSocket()
        {
            if (_gone > 0f) return false;
            int count = PlayerHands.Positions(_hands);
            foreach (var socket in _sockets)
            {
                if (socket == null) continue;
                for (int i = 0; i < count; i++)
                    if ((_hands[i] - socket.position).sqrMagnitude <= _socketRadius * _socketRadius) return true;
            }
            return false;
        }

        /// Eases the panel toward gone or back, wearing the dissolve material while it is between the two.
        private void Animate()
        {
            float target = Open ? 1f : 0f;
            if (Mathf.Approximately(_gone, target)) return;
            _gone = Mathf.MoveTowards(_gone, target, Time.deltaTime / Mathf.Max(0.01f, _dissolveSeconds));

            if (_gone <= 0f)
            {
                Wear(_ownMaterials, false);
                ShowRenderers(true);
                SetColliders(true);
                return;
            }
            if (_gone >= 1f)
            {
                ShowRenderers(false);
                return;
            }

            ShowRenderers(true);
            if (_dissolveMaterial == null)
            {
                ShowRenderers(_gone < 0.5f);
                return;
            }
            if (_smokeMaterials == null) _smokeMaterials = MakeSmokeMaterials();
            Wear(_smokeMaterials, true);
            foreach (var set in _smokeMaterials)
                foreach (var material in set) material.SetFloat(DissolveId, _gone);
        }

        /// A dissolve material for each of the panel's own, carrying its textures and colour.
        private Material[][] MakeSmokeMaterials()
        {
            var made = new Material[_ownMaterials.Length][];
            for (int i = 0; i < _ownMaterials.Length; i++)
            {
                made[i] = new Material[_ownMaterials[i].Length];
                for (int j = 0; j < _ownMaterials[i].Length; j++)
                {
                    var own = _ownMaterials[i][j];
                    var smoke = new Material(_dissolveMaterial) { name = $"{_dissolveMaterial.name} ({(own != null ? own.name : "none")})" };
                    if (own != null)
                    {
                        Copy(own, smoke, "_BaseMap", "_MainTex");
                        Copy(own, smoke, "_BumpMap", null);
                        if (own.HasProperty("_BaseColor")) smoke.SetColor("_BaseColor", own.GetColor("_BaseColor"));
                        else if (own.HasProperty("_Color")) smoke.SetColor("_BaseColor", own.GetColor("_Color"));
                        if (own.HasProperty("_Metallic")) smoke.SetFloat("_Metallic", own.GetFloat("_Metallic"));
                        if (own.HasProperty("_Smoothness")) smoke.SetFloat("_Smoothness", own.GetFloat("_Smoothness"));
                    }
                    made[i][j] = smoke;
                }
            }
            return made;
        }

        private static void Copy(Material from, Material to, string name, string fallback)
        {
            string source = from.HasProperty(name) && from.GetTexture(name) != null ? name
                : fallback != null && from.HasProperty(fallback) ? fallback : null;
            if (source == null) return;
            to.SetTexture(name, from.GetTexture(source));
            to.SetTextureScale(name, from.GetTextureScale(source));
            to.SetTextureOffset(name, from.GetTextureOffset(source));
        }

        private void Wear(Material[][] sets, bool smoke)
        {
            if (_wearingSmoke == smoke) return;
            _wearingSmoke = smoke;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].sharedMaterials = sets[i];
        }

        private void ShowRenderers(bool show)
        {
            foreach (var r in _renderers)
                if (r != null && r.enabled != show) r.enabled = show;
        }

        private void SetColliders(bool on)
        {
            foreach (var c in _colliders)
                if (c != null && c.enabled != on) c.enabled = on;
        }

        private void OnValidate()
        {
            if (TryGetComponent<BoxCollider>(out var box) && !box.isTrigger) box.isTrigger = true;
        }
    }
}
