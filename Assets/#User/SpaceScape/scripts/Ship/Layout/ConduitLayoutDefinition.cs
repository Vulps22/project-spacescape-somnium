using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A block of cells that holds conduits: x by y by z cells, spread evenly out from its Origin, never fewer
    /// than one each way. Goes on a conduit container's root; the Origin is an empty child placed where the
    /// conduits run. When the world starts, every cell in it gets a conduit with both segments parked, unless
    /// a drawn conduit or a placed tile is already there (ConduitLayout.Spawn); while editing they show as
    /// gizmos. Cells inside a component slot's volume are left out: a slot's volume is reserved (grid.md), so
    /// a floor's layer of conduits routes round the slots standing on it.
    ///
    /// Keeps the container on the grid while edited: the root turns only in quarter turns and sits on a cell
    /// centre, and the Origin stays square to the root, on a cell centre along an axis with an odd count and on
    /// a cell face along an even one. So the art and the cells move together, and a prefab says 8 x 8 x 1
    /// once and is right however it is placed. The counts are along the root's axes. Cells are always
    /// GridCell.Size: keep the root and Origin unscaled.
    [ExecuteAlways]
    public sealed class ConduitLayoutDefinition : MonoBehaviour
    {
        [Tooltip("How many cells it holds along its own x, y and z. At least 1 each way.")]
        [SerializeField] private Vector3Int _cells = Vector3Int.one;
        [Tooltip("Where the block is centred: an empty child placed where the conduits run. None centres it on this object.")]
        [SerializeField] private Transform _origin;
        [Tooltip("Ticked on walls and corners: its cells at the layout's Ring Heights are wired as the ship's rings, runs that go all the way round from one open end to the other (ConduitLayout).")]
        [SerializeField] private bool _carriesRings;

        /// True when its cells at the ring heights are part of the ship's rings.
        public bool CarriesRings => _carriesRings;

        /// Where the block is centred: the Origin, or this object when none is set.
        public Transform Origin => _origin != null ? _origin : transform;

        /// How many cells it holds along its own axes.
        public Vector3Int Size => _cells;

        /// True when it sits square to the world grid, so its cells land on whole cells.
        public bool IsSquare
        {
            get
            {
                var r = transform.rotation;
                return IsAxis(r * Vector3.right) && IsAxis(r * Vector3.up);
            }
        }

        /// Every world cell it holds. None when it is not square to the grid.
        public IEnumerable<Vector3Int> WorldCells
        {
            get
            {
                if (!IsSquare) yield break;
                var rotation = transform.rotation;
                Vector3 centre = Origin.position;
                for (int x = 0; x < _cells.x; x++)
                    for (int y = 0; y < _cells.y; y++)
                        for (int z = 0; z < _cells.z; z++)
                        {
                            var local = new Vector3(x - (_cells.x - 1) * 0.5f, y - (_cells.y - 1) * 0.5f, z - (_cells.z - 1) * 0.5f) * GridCell.Size;
                            yield return GridCell.ToCell(centre + rotation * local);
                        }
            }
        }

        /// The cells that get conduits: every cell it holds but those inside a component slot.
        public IEnumerable<Vector3Int> Cells
        {
            get
            {
                foreach (var cell in WorldCells)
                    if (!InSlot(cell)) yield return cell;
            }
        }

        /// True when a cell is inside a component slot's volume.
        public static bool InSlot(Vector3Int cell)
        {
            Vector3 centre = GridCell.ToWorld(cell);
            foreach (var slot in ComponentSlot.All)
                if (slot != null && slot.gameObject.activeInHierarchy && slot.Contains(centre)) return true;
            return false;
        }

        private void OnValidate()
        {
            _cells = Vector3Int.Max(_cells, Vector3Int.one);
            transform.hasChanged = true;
        }

        private void Update()
        {
            if (Application.isPlaying || !(transform.hasChanged || Origin.hasChanged)) return;
            Snap();
            transform.hasChanged = false;
            Origin.hasChanged = false;
        }

        /// Puts it back on the grid, writing only what is off, so it does not dirty the scene for nothing.
        private void Snap()
        {
            Vector3 euler = transform.localEulerAngles;
            var rotation = Quaternion.Euler(Quarter(euler.x), Quarter(euler.y), Quarter(euler.z));
            if (Quaternion.Angle(rotation, transform.localRotation) > 0.01f) transform.localRotation = rotation;

            if (!IsSquare)
            {
                Debug.LogWarning($"ConduitLayoutDefinition: '{name}' is not square to the grid (its parent is turned by something other than quarter turns); it holds no cells", this);
                return;
            }

            // On a slot, the slot places it: a slot's centre is a grid corner, which the slot keeps it on, and in
            // the slot's prefab (centred on the origin, which is not a corner) snapping would save it half a cell off.
            if (transform.parent != null && GetComponentInParent<ComponentSlot>() != null) return;

            if (Origin == transform)
            {
                // No Origin: the root itself is the centre. An odd count centres on a cell, an even one on a face.
                Vector3 span = transform.rotation * (Vector3)_cells;
                Vector3 at = transform.position;
                var centred = new Vector3(Axis(at.x, span.x), Axis(at.y, span.y), Axis(at.z, span.z));
                if ((centred - at).sqrMagnitude > 1e-10f) transform.position = centred;
                return;
            }

            // The root on a cell centre, and the Origin offset from it by whole cells, or half a cell more along an
            // even count: so wherever the root is put, the Origin lands where the block needs it.
            Vector3 position = transform.position;
            var snapped = new Vector3(Axis(position.x, 1), Axis(position.y, 1), Axis(position.z, 1));
            if ((snapped - position).sqrMagnitude > 1e-10f) transform.position = snapped;

            if (Quaternion.Angle(Origin.localRotation, Quaternion.identity) > 0.01f) Origin.localRotation = Quaternion.identity;
            Vector3 local = Origin.localPosition;
            var offset = new Vector3(Axis(local.x, _cells.x), Axis(local.y, _cells.y), Axis(local.z, _cells.z));
            if ((offset - local).sqrMagnitude > 1e-10f) Origin.localPosition = offset;
        }

        private static bool IsAxis(Vector3 v) =>
            Mathf.Abs(Mathf.Abs(v.x) + Mathf.Abs(v.y) + Mathf.Abs(v.z) - 1f) < 1e-3f;

        private static float Quarter(float degrees) => Mathf.Round(degrees / 90f) * 90f;

        private static float Axis(float at, float span)
        {
            int count = Mathf.Abs(Mathf.RoundToInt(span));
            float offset = count % 2 == 0 ? GridCell.Half : 0f;
            return Mathf.Round((at - offset) / GridCell.Size) * GridCell.Size + offset;
        }

        /// Shows its block, and every conduit it will hold as the drawn ones show (ConduitLayout): a small box
        /// per cell, nothing wired. A cell with a drawn conduit shows that one instead.
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            bool square = IsSquare;
            Gizmos.color = square ? new Color(0.4f, 0.8f, 1f, 0.6f) : Color.red;
            Gizmos.matrix = Matrix4x4.TRS(Origin.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, (Vector3)_cells * GridCell.Size);
            Gizmos.matrix = Matrix4x4.identity;
            if (!square) return;

            if (_layout == null) _layout = FindFirstObjectByType<ConduitLayout>();
            Gizmos.color = new Color(0.7f, 0.7f, 0.75f, 0.8f);
            foreach (var cell in Cells)
                if (_layout == null || !_layout.TryGet(cell, out _))
                    Gizmos.DrawWireCube(GridCell.ToWorld(cell), Vector3.one * (GridCell.Size * 0.15f));
        }

        private ConduitLayout _layout;
    }
}
