using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The panel in front of a conduit container. A hand passed through any of its sockets (pulsing cubes,
    /// like a switch's lock; a pillar has one on each face) opens it: the panel smokes away to nothing, and
    /// the conduits behind it can be reached. While it is shut, the conduits it covers cannot be opened, but a
    /// switch on one, facing it, reaches out through it (Surface) and can be worked.
    ///
    /// It stays open until nobody is left in its trigger box (anything on the Occupant Layers: Somnium gives
    /// each player a PlayerTrigger on its Triggers layer), then smokes back in. The box is asked of physics
    /// directly (an overlap test, a few times a second while open), not left to trigger events, which only
    /// fire when one side has a Rigidbody.
    ///
    /// Whoever decides (the master, or anyone with no network) opens and shuts it; everyone else asks
    /// (OpenRequested) and follows (SetOpen). The smoke plays on every client from that one state, but only
    /// where a camera can see the panel as it starts to go or come back; anywhere else it is simply gone or
    /// back, with nothing to watch.
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
        [Tooltip("What counts as someone in the box. Somnium puts each player's PlayerTrigger on the Triggers layer; nothing of the avatar is on Player.")]
        [SerializeField] private LayerMask _occupantLayers = 1 << 15;   // Triggers
        [Tooltip("Seconds between checks of who is in the box while it is open.")]
        [SerializeField] private float _checkSeconds = 0.25f;

        private static readonly List<AccessPanel> Panels = new List<AccessPanel>();
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        private static readonly Collider[] Found = new Collider[32];
        private static readonly Plane[] Frustum = new Plane[6];
        private static Camera[] _cameras = new Camera[4];
        private readonly HashSet<Vector3Int> _covers = new HashSet<Vector3Int>();
        private readonly Vector3[] _hands = new Vector3[MaxHands];
        private Renderer[] _renderers = new Renderer[0];
        private Material[][] _ownMaterials = new Material[0][];
        private Material[][] _smokeMaterials;
        private Collider[] _colliders = new Collider[0];
        private readonly List<Transform> _cubes = new List<Transform>();
        private Vector3 _cubeScale;
        private BoxCollider _box;
        private float _nextCheck;
        private bool _occupied;
        private float _openedAt;
        private float _gone;
        private bool _wearingSmoke;
        private bool _watched;
        private Bounds _bounds;
        private bool _hasBounds;
        private bool _measured;
        private GridDirection _side = GridDirection.None;
        private readonly Dictionary<Vector3Int, float> _surfaceAt = new Dictionary<Vector3Int, float>();

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
            {
                if (panel == null || panel.Open) continue;
                panel.Measure();
                if (panel._covers.Contains(cell)) return true;
            }
            return false;
        }

        /// Where the panel's mesh is in front of a cell, as a distance along the given face (a world position
        /// along that axis), when the face is the one toward the panel covering the cell. False otherwise, or
        /// when the mesh has nothing in front of that cell. A switch on the cell sets its depth from this.
        public static bool Surface(Vector3Int cell, GridDirection face, out float surface)
        {
            surface = 0f;
            foreach (var panel in Panels)
            {
                if (panel == null) continue;
                panel.Measure();
                if (panel._side != face || !panel._covers.Contains(cell)) continue;
                return panel.SurfaceAt(cell, out surface);
            }
            return false;
        }

        /// The mesh in front of one cell: one ray from the room side back toward the cell, against the panel's
        /// own colliders, asked once per cell and kept. A panel that is open has its colliders off, so they are
        /// switched on for the ray and back off after.
        private bool SurfaceAt(Vector3Int cell, out float surface)
        {
            if (_surfaceAt.TryGetValue(cell, out surface)) return !float.IsNaN(surface);

            Vector3 along = _side.Vector();
            Vector3 centre = GridCell.ToWorld(cell);
            float reach = GridCell.Size * 8f;
            var ray = new Ray(centre + along * reach, -along);
            float nearest = float.MaxValue;
            foreach (var c in _panel.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c.isTrigger) continue;
                bool was = c.enabled;
                if (!was) c.enabled = true;
                if (c.Raycast(ray, out var hit, reach) && hit.distance < nearest) nearest = hit.distance;
                if (!was) c.enabled = false;
            }
            surface = nearest < float.MaxValue ? Vector3.Dot(ray.origin, along) - nearest : float.NaN;
            _surfaceAt[cell] = surface;
            return !float.IsNaN(surface);
        }

        /// Works out, once, which cells it covers and which side of them it stands on. Asked for, not done at
        /// Start, because conduits place their switches while the world is still waking.
        private void Measure()
        {
            if (_measured) return;
            _measured = true;
            if (!TryGetComponent<ConduitLayoutDefinition>(out var definition)) return;
            foreach (var cell in definition.Cells) _covers.Add(cell);
            if (_panel == null) return;

            // Which side of its cells the panel stands on: the axis its middle is furthest off them along.
            bool any = false;
            var bounds = new Bounds();
            foreach (var r in _panel.GetComponentsInChildren<Renderer>(true))
            {
                if (any) bounds.Encapsulate(r.bounds);
                else { bounds = r.bounds; any = true; }
            }
            if (!any) return;
            Vector3 offset = bounds.center - definition.Origin.position;
            Vector3 abs = new Vector3(Mathf.Abs(offset.x), Mathf.Abs(offset.y), Mathf.Abs(offset.z));
            if (abs.x >= abs.y && abs.x >= abs.z) _side = offset.x >= 0f ? GridDirection.XPlus : GridDirection.XMinus;
            else if (abs.y >= abs.z) _side = offset.y >= 0f ? GridDirection.YPlus : GridDirection.YMinus;
            else _side = offset.z >= 0f ? GridDirection.ZPlus : GridDirection.ZMinus;
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            _box = GetComponent<BoxCollider>();
            if (!_box.isTrigger) _box.isTrigger = true;

            if (_panel != null)
            {
                _renderers = _panel.GetComponentsInChildren<Renderer>(true);
                _colliders = _panel.GetComponentsInChildren<Collider>(true);
                _ownMaterials = new Material[_renderers.Length][];
                for (int i = 0; i < _renderers.Length; i++)
                {
                    _ownMaterials[i] = _renderers[i].sharedMaterials;
                    if (_hasBounds) _bounds.Encapsulate(_renderers[i].bounds);
                    else { _bounds = _renderers[i].bounds; _hasBounds = true; }
                }
            }
            MakeCubes(HideFlags.None);
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            Measure();
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

        /// True when anything on the Occupant Layers is inside the box now.
        public bool Occupied => Overlapping(_occupantLayers) > 0;

        /// How many colliders in the given layers are inside the box, into Found.
        private int Overlapping(int mask)
        {
            if (_box == null || mask == 0) return 0;
            var t = _box.transform;
            Vector3 centre = t.TransformPoint(_box.center);
            Vector3 half = Vector3.Scale(_box.size * 0.5f, t.lossyScale);
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            return Physics.OverlapBoxNonAlloc(centre, half, Found, t.rotation, mask, QueryTriggerInteraction.Collide);
        }

        /// Logs everything in the box, every layer, once when it opens: if a player is not on the Occupant
        /// Layers, this says what they are on.
        private void LogWhoIsInside()
        {
            int count = Overlapping(~0);
            var said = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                var c = Found[i];
                if (c == null || c == _box || c.transform.IsChildOf(transform)) continue;
                said.Append($"{c.name} ({LayerMask.LayerToName(c.gameObject.layer)}) ");
            }
            Debug.Log($"[SpaceScape] AccessPanel '{name}' opened; in its box: {(said.Length > 0 ? said.ToString() : "nothing")}", this);
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
            if (Decides && Open && Time.time >= _openedAt + _minimumOpenSeconds && Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + _checkSeconds;
                if (!Occupied) SetOpen(false);
            }
            Animate();
        }

        /// Opens or shuts it. Where this client decides; everyone else is told what was decided.
        public void SetOpen(bool open)
        {
            if (open == Open) return;
            Open = open;
            _watched = Seen();
            if (open)
            {
                _openedAt = Time.time;
                if (Decides) LogWhoIsInside();
            }
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
            _gone = _watched ? Mathf.MoveTowards(_gone, target, Time.deltaTime / Mathf.Max(0.01f, _dissolveSeconds)) : target;

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

        /// True when the panel is inside the view of any camera rendering now. Asked of its bounds, not its
        /// renderers, since an open panel's renderers are off and a renderer that is off is never visible.
        /// Walls in the way do not count: a panel behind one still smokes.
        private bool Seen()
        {
            if (!_hasBounds) return true;
            if (_cameras.Length < Camera.allCamerasCount) _cameras = new Camera[Camera.allCamerasCount];
            int count = Camera.GetAllCameras(_cameras);
            for (int i = 0; i < count; i++)
            {
                var cam = _cameras[i];
                if (cam == null || cam.targetTexture != null) continue;   // a camera drawing to a texture is not an eye
                GeometryUtility.CalculateFrustumPlanes(cam, Frustum);
                if (GeometryUtility.TestPlanesAABB(Frustum, _bounds)) return true;
            }
            return false;
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
