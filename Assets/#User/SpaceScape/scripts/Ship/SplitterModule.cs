using UnityEngine;

namespace SpaceScape.Ship
{
    /// Opens a second face on the tile it is slotted into, which is all a branch is: power already
    /// divides by itself once a tile has somewhere else to send it.
    public sealed class SplitterModule : ConduitModule
    {
        [SerializeField] private GridDirection _branch = GridDirection.ZMinus;

        private readonly GridDirection[] _outputs = new GridDirection[1];

        /// The one extra face this splitter opens.
        public GridDirection Branch
        {
            get => _branch;
            set => _branch = value;
        }

        public override GridDirection[] ExtraOutputs
        {
            get
            {
                _outputs[0] = _branch;
                return _outputs;
            }
        }

        private void OnDrawGizmos()
        {
            if (_branch == GridDirection.None) return;
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
            Gizmos.DrawLine(transform.position, transform.position + _branch.Vector());
        }
    }
}
