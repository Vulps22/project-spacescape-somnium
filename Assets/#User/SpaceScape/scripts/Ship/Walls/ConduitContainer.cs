using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A wall, floor or ceiling with a conduit channel through its middle. Keeps itself on the grid while
    /// it is edited: width and height snap to whole cells, depth stays one cell (its skins' thickness is
    /// built into the prefab), it turns only in quarter turns, and it sits so its edges land on cell faces
    /// and its channel on a line of cell centres. Its pivot is the channel's centre; +Z faces a room.
    /// Assumes an unscaled, unrotated parent (or none). Either side can be hidden, leaving the channel
    /// open from that side.
    [ExecuteAlways]
    public sealed class ConduitContainer : MonoBehaviour
    {
        [Header("Sides")]
        [Tooltip("Shows the +Z side's skin. Off leaves the channel open from that side: no skin to see or bump into.")]
        [SerializeField] private bool _plusSideVisible = true;
        [Tooltip("Shows the -Z side's skin. Off leaves the channel open from that side: no skin to see or bump into.")]
        [SerializeField] private bool _minusSideVisible = true;
        [Tooltip("The skin on the +Z side. Found automatically as the child named Front.")]
        [SerializeField] private GameObject _plusSide;
        [Tooltip("The skin on the -Z side. Found automatically as the child named Back.")]
        [SerializeField] private GameObject _minusSide;

        /// Whether the +Z side's skin is there. For a panel coming off, or damage, to set in the world.
        public bool PlusSideVisible
        {
            get => _plusSideVisible;
            set { _plusSideVisible = value; ShowSides(); }
        }

        /// Whether the -Z side's skin is there.
        public bool MinusSideVisible
        {
            get => _minusSideVisible;
            set { _minusSideVisible = value; ShowSides(); }
        }

        private void OnEnable()
        {
            Snap();
            ShowSides();
        }

        private void OnValidate()
        {
            if (_plusSide == null) _plusSide = transform.Find("Front")?.gameObject;
            if (_minusSide == null) _minusSide = transform.Find("Back")?.gameObject;
            ShowSides();
        }

        /// Shows or hides each side's skin and its collider. The objects stay active, so switching one back
        /// on needs nothing else.
        private void ShowSides()
        {
            Show(_plusSide, _plusSideVisible);
            Show(_minusSide, _minusSideVisible);
        }

        private static void Show(GameObject side, bool visible)
        {
            if (side == null) return;
            foreach (var r in side.GetComponentsInChildren<Renderer>(true)) if (r.enabled != visible) r.enabled = visible;
            foreach (var c in side.GetComponentsInChildren<Collider>(true)) if (c.enabled != visible) c.enabled = visible;
        }

        private void Update()
        {
            if (Application.isPlaying || !transform.hasChanged) return;
            Snap();
            transform.hasChanged = false;
        }

        /// Puts it back on the grid, writing only what is off, so it does not dirty the scene for nothing.
        private void Snap()
        {
            if (Application.isPlaying) return;

            Vector3 euler = transform.localEulerAngles;
            var rotation = Quaternion.Euler(Quarter(euler.x), Quarter(euler.y), Quarter(euler.z));
            if (Quaternion.Angle(rotation, transform.localRotation) > 0.01f) transform.localRotation = rotation;

            Vector3 scale = transform.localScale;
            var cells = new Vector3(Cells(scale.x), Cells(scale.y), 1f);
            var snappedScale = new Vector3(cells.x * GridCell.Size, cells.y * GridCell.Size, 1f);
            if ((snappedScale - scale).sqrMagnitude > 1e-10f) transform.localScale = snappedScale;

            // How many cells it spans along each world axis: an odd count centres on a cell, an even one on a face.
            Vector3 span = transform.rotation * cells;
            Vector3 position = transform.position;
            var snapped = new Vector3(Axis(position.x, span.x), Axis(position.y, span.y), Axis(position.z, span.z));
            if ((snapped - position).sqrMagnitude > 1e-10f) transform.position = snapped;
        }

        private static float Quarter(float degrees) => Mathf.Round(degrees / 90f) * 90f;

        private static int Cells(float metres) => Mathf.Max(1, Mathf.RoundToInt(metres / GridCell.Size));

        private static float Axis(float at, float span)
        {
            int count = Mathf.Abs(Mathf.RoundToInt(span));
            float offset = count % 2 == 0 ? GridCell.Half : 0f;
            return Mathf.Round((at - offset) / GridCell.Size) * GridCell.Size + offset;
        }
    }
}
