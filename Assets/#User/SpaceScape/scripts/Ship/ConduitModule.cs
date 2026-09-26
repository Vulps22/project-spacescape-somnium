using UnityEngine;

namespace SpaceScape.Ship
{
    /// A part slotted into a tile, sitting at its centre. A tile can only do more than pass power
    /// straight through once one of these is installed.
    public abstract class ConduitModule : MonoBehaviour
    {
        /// Faces this part opens up, on top of the one the tile already has.
        public abstract GridDirection[] ExtraOutputs { get; }

        /// The tile this part is slotted into.
        public GridNode Tile => GetComponentInParent<GridNode>();

        protected virtual void OnValidate()
        {
            // The origin of a module is always the centre of its tile.
            if (transform.parent != null) transform.localPosition = Vector3.zero;
        }
    }
}
