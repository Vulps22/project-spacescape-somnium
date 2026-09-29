using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// What a component is to a slot: its size, a constant (docs/grid.md), and which of its own sides is its
    /// front and which its top. A slot turns it so its front faces the slot's front and its top is the slot's
    /// up. The transform is the component's centre.
    public sealed class SlottedComponent : MonoBehaviour
    {
        [Tooltip("The size of slot this component fits. Only a slot of the same size takes it.")]
        [SerializeField] private SlotSize _size = SlotSize.S1;
        [Tooltip("Which of the component's own sides is its front: it faces out of a slot's front.")]
        [SerializeField] private GridDirection _front = GridDirection.ZPlus;
        [Tooltip("Which of the component's own sides is its top: it is a slot's up. Must not lie along the front.")]
        [SerializeField] private GridDirection _up = GridDirection.YPlus;

        /// The size of slot this component fits.
        public SlotSize Size => _size;

        /// The component's front, in its own axes.
        public GridDirection Front => _front;

        /// The component's top, in its own axes.
        public GridDirection Up => _up;

        /// Turns the slot's frame (+Z front, +Y up) into the component's own axes.
        public Quaternion Frame =>
            Mathf.Abs(Vector3.Dot(_front.Vector(), _up.Vector())) > 0.5f
                ? Quaternion.identity
                : Quaternion.LookRotation(_front.Vector(), _up.Vector());

        /// The component's size in cells along its own axes.
        public Vector3Int LocalCells
        {
            get
            {
                var turned = Frame * (Vector3)ComponentSlot.CellsOf(_size);
                return new Vector3Int(Mathf.RoundToInt(Mathf.Abs(turned.x)), Mathf.RoundToInt(Mathf.Abs(turned.y)),
                    Mathf.RoundToInt(Mathf.Abs(turned.z)));
            }
        }

        /// The rotation that seats this component in a slot facing the given way.
        public Quaternion SeatedIn(Quaternion slotFacing) => slotFacing * Quaternion.Inverse(Frame);

        private void OnValidate()
        {
            if (Mathf.Abs(Vector3.Dot(_front.Vector(), _up.Vector())) > 0.5f)
                Debug.LogWarning($"'{name}': a component's top cannot lie along its front", this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation * Frame, Vector3.one);
            var extent = (Vector3)ComponentSlot.CellsOf(_size) * GridCell.Size;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.7f);
            Gizmos.DrawWireCube(Vector3.zero, extent);
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Vector3 front = new Vector3(0f, 0f, extent.z * 0.5f);
            Gizmos.DrawLine(front, front + Vector3.forward * 0.75f);
            Gizmos.DrawSphere(front + Vector3.forward * 0.75f, 0.05f);
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
            Gizmos.DrawLine(Vector3.zero, Vector3.up * (extent.y * 0.5f + 0.5f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
