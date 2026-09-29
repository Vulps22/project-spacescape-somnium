using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The five slot sizes (docs/grid.md). Only an Sx component fits an Sx slot.
    public enum SlotSize { S1 = 1, S2 = 2, S3 = 3, S4 = 4, S5 = 5 }

    /// A place on the ship a component of one size sits in. Its volume is reserved: no conduits inside it.
    /// It shows nothing in-world but its grab handle; the volume, front and up are gizmos.
    ///
    /// A thin wrapper (docs/grid.md). To the grid it is the component: it is the tile, its node never goes
    /// away, and every question is answered by whatever is installed. To the component it is the grid: the
    /// component is bound to the slot's node and never knows the slot is there. Empty, it is inert.
    ///
    /// Its anchor is its centre, which is always a grid corner, since every size is an even number of
    /// cells each way. The front and up are picked like a conduit's faces, and the slot turns itself to
    /// match, so the handle on its front bottom edge follows.
    [ExecuteAlways]
    [DefaultExecutionOrder(-200)]   // makes its starting component before the grid builds
    public sealed class ComponentSlot : MonoBehaviour
    {
        [Tooltip("Which size of component this slot takes.")]
        [SerializeField] private SlotSize _size = SlotSize.S1;
        [Tooltip("The side a slotted component faces, and where the grab handle is.")]
        [SerializeField] private GridDirection _front = GridDirection.ZPlus;
        [Tooltip("Up for a slotted component. Must not be along the front.")]
        [SerializeField] private GridDirection _up = GridDirection.YPlus;
        [Tooltip("The grab handle, kept on the middle of the front bottom edge.")]
        [SerializeField] private Transform _handle;
        [Tooltip("The component prefab in this slot when the world starts, or none. Drawn in place while editing; made when the world starts, seated at the slot's centre facing its front.")]
        [FormerlySerializedAs("_component")]
        [SerializeField] private GridNode _startsWith;
        [Tooltip("Seconds a pulled handle leaves the component free to take out, before the slot tries to lock again.")]
        [SerializeField] private float _unlockSeconds = 10f;
        [Tooltip("How far a let-go component's centre may be from the slot's centre and still go in, in metres.")]
        [SerializeField] private float _seatDistance = 0.5f;
        [Tooltip("How far a let-go component may be turned from sitting square in the slot and still go in, in degrees.")]
        [SerializeField] private float _seatAngle = 30f;
        [Tooltip("Share of its maximum integrity a component loses when a hand pulls it out while power is flowing through it, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float _liveRemovalDamage = 0.1f;

        [Header("Placement guide")]
        [Tooltip("The guide box while a held component inside the slot is neither positioned nor aligned.")]
        [SerializeField] private Material _guideNeither;
        [Tooltip("The guide box while it is positioned or aligned, but not both.")]
        [SerializeField] private Material _guideOneOf;
        [Tooltip("The guide box while it is both, so letting go snaps it in.")]
        [SerializeField] private Material _guideBoth;

        private static readonly List<ComponentSlot> Slots = new List<ComponentSlot>();

        /// Every slot in the world.
        public static IReadOnlyList<ComponentSlot> All => Slots;

        private float _relockAt;
        private PowerNode _node;
        private PowerGrid _grid;
        private GridNode _component;

        /// The component in this slot, or null. Made from Starts With when the world starts.
        public GridNode Component => _component;

        /// The component prefab this slot starts with, or null.
        public GridNode StartsWith => _startsWith;

        /// The low corner of the slot's volume, in world cells.
        public Vector3Int WorldMin => GridCell.ToCell(transform.position - HalfWorld + Vector3.one * GridCell.Half);

        /// The high corner of the slot's volume, in world cells.
        public Vector3Int WorldMax => GridCell.ToCell(transform.position + HalfWorld - Vector3.one * GridCell.Half);

        private Vector3 HalfWorld
        {
            get
            {
                var turned = Facing * Extent;
                return new Vector3(Mathf.Abs(turned.x), Mathf.Abs(turned.y), Mathf.Abs(turned.z)) * 0.5f;
            }
        }

        /// Called by the grid once it has built this slot's node: puts the installed component on it.
        public void Attach(PowerNode node, PowerGrid grid)
        {
            _node = node;
            _grid = grid;
            Plug();
        }

        /// Puts a component in this empty slot, on its node, and rewires the slot. False when it is taken.
        public bool Install(GridNode component)
        {
            if (component == null || _component != null || !Fits(component)) return false;
            _component = component;
            if (component.TryGetComponent<SlottedComponent>(out var slotted)) slotted.Slot = this;
            if (component.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            if (_node == null) { Seat(component); return true; }
            _grid.Unloosen(component);   // its own node, if it was loose, goes; it keeps the heat
            Plug();
            _grid.Rewire(GetComponent<GridNode>());
            return true;
        }

        /// Takes the component out: it keeps its heat, lets go of the node, and goes on running on a node of its
        /// own with nothing connected (docs/polish.md, a loose component keeps running). The slot goes inert
        /// and is rewired. Returns the component, or null when the slot was empty.
        public GridNode Remove()
        {
            var component = _component;
            if (component == null) return null;
            _component = null;
            if (component.TryGetComponent<SlottedComponent>(out var slotted) && slotted.Slot == this) slotted.Slot = null;
            if (_node == null) return component;

            component.HeldCelsius = _node.Celsius;
            component.Unbind();
            Plug();
            _grid.Rewire(GetComponent<GridNode>());
            _grid.Loosen(component);
            return component;
        }

        /// Hands the node whatever is installed, or leaves it inert.
        private void Plug()
        {
            var tile = GetComponent<GridNode>();
            if (tile != null) tile.DrivesNode = _component == null;
            if (_node == null) return;

            if (_component == null)
            {
                _node.Source = null;
                _node.Sink = InertSink.Instance;
                _node.Integrity = null;
                _node.On = true;
                _node.Celsius = PowerGraph.AmbientCelsius;
                return;
            }

            Seat(_component);
            var behaviour = _component.GetComponent<BehaviourModule>();
            _node.Source = behaviour != null ? behaviour.Source : null;
            _node.Sink = behaviour != null && behaviour.Sink != null ? behaviour.Sink
                : _node.Source == null ? InertSink.Instance : null;
            _node.Integrity = _component.TryGetComponent<IntegrityModule>(out var integrity) ? integrity.Integrity : null;
            _component.DrivesNode = true;
            _component.Bind(_node);
            _node.Celsius = _component.HeldCelsius;
        }

        /// True when a component is this slot's size.
        public bool Fits(GridNode component) =>
            component != null && component.TryGetComponent<SlottedComponent>(out var slotted) && slotted.Size == _size;

        /// How a component sits here: its front out of the slot's front, its top the slot's up.
        private Quaternion SeatFor(GridNode component) =>
            component.TryGetComponent<SlottedComponent>(out var slotted) ? slotted.SeatedIn(Facing) : Facing;

        /// Puts a component at the slot's centre, facing out of its front with the slot's up.
        private void Seat(GridNode component)
        {
            var t = component.transform;
            var turn = SeatFor(component);
            if ((t.position - transform.position).sqrMagnitude > 1e-8f || Quaternion.Angle(t.rotation, turn) > 0.01f)
                t.SetPositionAndRotation(transform.position, turn);
        }

        /// True while the component in this slot can be taken out.
        public bool Unlocked { get; private set; }

        /// True where this client decides the lock: the master, or anyone with no network. Set by the
        /// network; a client that does not decide asks instead (PullRequested).
        public bool Decides { get; set; } = true;

        /// Raised on a client that does not decide when its hand pulls the handle, for the network to ask.
        public event Action PullRequested;

        /// The handle was pulled here. Unlocks, or asks whoever decides.
        public void Pull()
        {
            if (Decides) Unlock();
            else PullRequested?.Invoke();
        }

        /// Frees the component for the unlock time, then the slot tries to lock again. Where this client
        /// decides; everyone else is told (SetUnlocked).
        public void Unlock()
        {
            Unlocked = true;
            _relockAt = Time.time + _unlockSeconds;
        }

        /// Takes the lock as the one who decides has it.
        public void SetUnlocked(bool unlocked) => Unlocked = unlocked;

        /// Raised when a hand here takes the component out, for the network to tell whoever decides.
        public event Action<GridNode> TakenOut;

        /// Raised when a hand here puts a component in, for the network to tell whoever decides.
        public event Action<GridNode> PutIn;

        /// A hand here lifted the component out of the unlocked slot. Takes it out now; the network tells
        /// whoever decides.
        public void TakeOut(GridNode component)
        {
            if (component == null || component != _component || !Unlocked) return;
            if (Decides) DamageIfLive(component);
            Remove();
            TakenOut?.Invoke(component);
        }

        /// A hand on another client took the component out, and this client decides: it loses integrity if
        /// it was live, as it would for a hand here, and comes out.
        public void TakenOutElsewhere(GridNode component)
        {
            if (component == null || component != _component) return;
            DamageIfLive(component);
            Remove();
        }

        /// Pulled out while power was flowing through it, a component loses a share of its maximum integrity.
        /// Only where the lock is decided, so it is taken once; the component's own data carries the new
        /// condition to everyone.
        private void DamageIfLive(GridNode component)
        {
            if (_node == null || !component.TryGetComponent<IntegrityModule>(out var module)) return;
            const double Flowing = 1e-6;
            bool live = _node.Inflow > Flowing || _node.Offered > Flowing || _node.Drawn > Flowing;
            if (live) module.Integrity.TakeDamage(module.Integrity.Max * _liveRemovalDamage);
        }

        /// True when a let-go component can go in here: the slot is empty, it is the slot's size, its centre
        /// is near the slot's, and it is turned near enough to sit square.
        public bool Accepts(GridNode component) =>
            _component == null && Fits(component) && IsPositioned(component) && IsAligned(component);

        /// True when a component's centre is near enough the slot's to go in.
        public bool IsPositioned(GridNode component) =>
            (component.transform.position - transform.position).sqrMagnitude <= _seatDistance * _seatDistance;

        /// True when a component is turned near enough square to go in.
        public bool IsAligned(GridNode component) =>
            Quaternion.Angle(component.transform.rotation, SeatFor(component)) <= _seatAngle;

        /// True when a point in the world is inside the slot's volume.
        public bool Contains(Vector3 world)
        {
            var local = Quaternion.Inverse(Facing) * (world - transform.position);
            var half = Extent * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// What the placement guide shows.
        public enum Guide { Hidden, Neither, OneOf, Both }

        private MeshRenderer _guide;

        /// Shows the slot's volume as a transparent box coloured by how a held component sits in it, or hides
        /// it. For the hand doing the placing only: nothing about it is networked.
        public void ShowGuide(Guide guide)
        {
            if (guide == Guide.Hidden)
            {
                if (_guide != null && _guide.enabled) _guide.enabled = false;
                return;
            }
            if (_guide == null) _guide = MakeGuide();
            _guide.enabled = true;
            var material = guide == Guide.Both ? _guideBoth : guide == Guide.OneOf ? _guideOneOf : _guideNeither;
            if (material != null && _guide.sharedMaterial != material) _guide.sharedMaterial = material;
        }

        private MeshRenderer MakeGuide()
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(box.GetComponent<Collider>());   // shows the volume, never blocks anything
            box.name = "Placement Guide";
            box.transform.SetParent(transform, false);
            box.transform.localPosition = Vector3.zero;
            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = Extent;
            var renderer = box.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        /// How far a component's centre is from this slot's, for picking the nearest of several.
        public float DistanceTo(GridNode component) => Vector3.Distance(component.transform.position, transform.position);

        /// A hand here let a component go where this slot accepts it. Puts it in and locks it; the network
        /// tells whoever decides.
        public bool PutInFromHand(GridNode component)
        {
            if (!Accepts(component) || !Install(component)) return false;
            if (Decides) Unlocked = false;
            PutIn?.Invoke(component);
            return true;
        }

        /// Which size of component this slot takes.
        public SlotSize Size => _size;

        /// The side a slotted component faces.
        public GridDirection Front => _front;

        /// Up for a slotted component.
        public GridDirection Up => _up;

        /// The slot's size in cells along its own axes: x across the front, y up, z front to back.
        public static Vector3Int CellsOf(SlotSize size)
        {
            switch (size)
            {
                case SlotSize.S1: return new Vector3Int(4, 4, 4);
                case SlotSize.S2: return new Vector3Int(6, 4, 4);
                case SlotSize.S3: return new Vector3Int(8, 8, 8);
                case SlotSize.S4: return new Vector3Int(10, 8, 8);
                default:          return new Vector3Int(10, 10, 10);
            }
        }

        /// The slot's size in cells along its own axes.
        public Vector3Int Cells => CellsOf(_size);

        /// The slot's size in metres along its own axes.
        public Vector3 Extent => (Vector3)Cells * GridCell.Size;

        /// The rotation its front and up give it; identity when up lies along the front.
        public Quaternion Facing =>
            Mathf.Abs(Vector3.Dot(_front.Vector(), _up.Vector())) > 0.5f
                ? Quaternion.identity
                : Quaternion.LookRotation(_front.Vector(), _up.Vector());

        /// The nearest grid corner: where a slot's centre goes. Corners sit half a cell off the cell centres.
        public static Vector3 NearestCorner(Vector3 world)
        {
            float half = GridCell.Half;
            return new Vector3(Snap(world.x), Snap(world.y), Snap(world.z));

            float Snap(float v) => Mathf.Round((v - half) / GridCell.Size) * GridCell.Size + half;
        }

        /// Every world cell inside the slot's volume.
        public System.Collections.Generic.IEnumerable<Vector3Int> WorldCells
        {
            get
            {
                var cells = Cells;
                var rotation = Facing;
                Vector3 centre = transform.position;
                for (int x = 0; x < cells.x; x++)
                    for (int y = 0; y < cells.y; y++)
                        for (int z = 0; z < cells.z; z++)
                        {
                            var local = new Vector3(x - (cells.x - 1) * 0.5f, y - (cells.y - 1) * 0.5f, z - (cells.z - 1) * 0.5f) * GridCell.Size;
                            yield return GridCell.ToCell(centre + rotation * local);
                        }
            }
        }

        private void Awake()
        {
            PlaceHandle();
        }

        /// Makes the starting component here and installs it, for a world with no network. Networked, the
        /// master spawns it instead (SlotNetwork).
        public void StartLocally()
        {
            if (_startsWith == null || _component != null) return;
            if (!Fits(_startsWith))
            {
                Debug.LogWarning($"'{name}' ({_size}) starts with '{_startsWith.name}', which is not a {_size} component; left empty", this);
                return;
            }
            var made = Instantiate(_startsWith, transform.position, SeatFor(_startsWith));
            made.name = _startsWith.name;
            Install(made);
        }

        /// Where a component made for this slot should appear: its centre, turned to sit here.
        public Pose SeatPose(GridNode component) => new Pose(transform.position, SeatFor(component));

        private void OnEnable()
        {
            if (!Slots.Contains(this)) Slots.Add(this);
            if (!Application.isPlaying) RenderPipelineManager.beginCameraRendering += DrawPreview;
        }

        private void OnDisable()
        {
            Slots.Remove(this);
            RenderPipelineManager.beginCameraRendering -= DrawPreview;
        }

        /// While editing, draws the starting component's meshes where it will sit. Drawn only: nothing is
        /// made in the scene.
        private void DrawPreview(ScriptableRenderContext context, Camera camera)
        {
            if (Application.isPlaying || _startsWith == null || this == null) return;
            var root = _startsWith.transform;
            var place = Matrix4x4.TRS(transform.position, SeatFor(_startsWith), Vector3.one) * root.worldToLocalMatrix;
            foreach (var filter in _startsWith.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled) continue;
                var matrix = place * filter.transform.localToWorldMatrix;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < mesh.subMeshCount && i < materials.Length; i++)
                    if (materials[i] != null) Graphics.DrawMesh(mesh, matrix, materials[i], gameObject.layer, camera, i);
            }

            // Its ports, as Play mode will place them: the Port model on each cell face, ringed by direction.
            if (!_startsWith.TryGetComponent<ComponentEdgeModule>(out var edges) || edges.PortPrefab == null) return;
            var port = edges.PortPrefab;
            foreach (var (face, cell, flow) in edges.PortsAt(transform.position, SeatFor(_startsWith)))
            {
                Vector3 outward = face.Vector();
                Vector3 up = Mathf.Abs(outward.y) > 0.5f ? Vector3.forward : Vector3.up;
                var at = Matrix4x4.TRS(GridCell.ToWorld(cell) + outward * GridCell.Half, Quaternion.LookRotation(outward, up), Vector3.one)
                    * port.transform.worldToLocalMatrix;
                foreach (var filter in port.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !filter.TryGetComponent<MeshRenderer>(out var renderer)) continue;
                    var matrix = at * filter.transform.localToWorldMatrix;
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < mesh.subMeshCount && i < materials.Length; i++)
                    {
                        var material = renderer == port.Ring ? port.MaterialFor(flow) : materials[i];
                        if (material != null) Graphics.DrawMesh(mesh, matrix, material, gameObject.layer, camera, i);
                    }
                }
            }
        }

        /// Puts the handle on the middle of the front bottom edge, facing out of the front, its back flush with
        /// the front so none of it is buried in the component.
        private void PlaceHandle()
        {
            if (_handle == null) return;
            var extent = Extent;
            var local = new Vector3(0f, -extent.y * 0.5f, extent.z * 0.5f + HandleBackDepth());
            if ((_handle.localPosition - local).sqrMagnitude > 1e-8f) _handle.localPosition = local;
            if (_handle.localRotation != Quaternion.identity) _handle.localRotation = Quaternion.identity;
        }

        private float? _handleBackDepth;

        /// How far the handle's models reach behind its origin, measured once from their meshes at rest.
        private float HandleBackDepth()
        {
            if (_handleBackDepth.HasValue) return _handleBackDepth.Value;
            float back = 0f;
            foreach (var filter in _handle.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                var toHandle = _handle.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    back = Mathf.Max(back, -toHandle.MultiplyPoint3x4(corner).z);
                }
            }
            _handleBackDepth = back;
            return back;
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                // A component still in place locks back in; a slot emptied by a hand just locks. A component
                // let go here later is checked and locked in by PutInFromHand.
                if (Decides && Unlocked && Time.time >= _relockAt) Unlocked = false;
                return;
            }
            PlaceHandle();
            // While editing: sit on a grid corner and turn to face the front.
            var corner = NearestCorner(transform.position);
            if ((transform.position - corner).sqrMagnitude > 1e-8f) transform.position = corner;
            if (Quaternion.Angle(transform.rotation, Facing) > 0.01f) transform.rotation = Facing;
            if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
        }

        private void OnValidate()
        {
            _handleBackDepth = null;
            if (_startsWith != null && !Fits(_startsWith))
                Debug.LogWarning($"'{name}' ({_size}) starts with '{_startsWith.name}', which is not a {_size} component", this);
            if (Mathf.Abs(Vector3.Dot(_front.Vector(), _up.Vector())) > 0.5f)
                Debug.LogWarning($"'{name}': a slot's up cannot lie along its front", this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Facing, Vector3.one);
            var extent = Extent;
            Gizmos.color = _size == SlotSize.S4 ? new Color(1f, 0.3f, 0.9f, 0.8f) : new Color(0.3f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, extent);

            // The front: a filled panel just inside it, and an arrow out of it.
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.12f);
            Gizmos.DrawCube(new Vector3(0f, 0f, extent.z * 0.5f - 0.01f), new Vector3(extent.x, extent.y, 0.02f));
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Vector3 front = new Vector3(0f, 0f, extent.z * 0.5f);
            Gizmos.DrawLine(front, front + Vector3.forward * 0.5f);
            Gizmos.DrawLine(Vector3.zero, Vector3.up * (extent.y * 0.5f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
