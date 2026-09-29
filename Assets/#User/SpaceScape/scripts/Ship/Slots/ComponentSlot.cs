using System;
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
            if (_node == null) return true;
            Plug();
            _grid.Rewire(GetComponent<GridNode>());
            return true;
        }

        /// Takes the component out: it keeps its heat, lets go of the node, and the slot goes inert and is
        /// rewired. Returns the component, or null when the slot was empty.
        public GridNode Remove()
        {
            var component = _component;
            if (component == null) return null;
            _component = null;
            if (_node == null) return component;

            component.HeldCelsius = _node.Celsius;
            component.Unbind();
            Plug();
            _grid.Rewire(GetComponent<GridNode>());
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
            if (!Application.isPlaying) RenderPipelineManager.beginCameraRendering += DrawPreview;
        }

        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= DrawPreview;

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
                // TODO(grid.md, build order 4): relock only if the component is still in place; otherwise
                // listen for one arriving and check its alignment, position and size.
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
