using UnityEngine;

namespace SpaceScape.Ship
{
    /// A script added to a tile to give it a feature or a behaviour. Knows only the tile it sits on.
    [RequireComponent(typeof(GridNode))]
    public abstract class Module : MonoBehaviour
    {
        private GridNode _tile;

        /// The tile this module is on.
        public GridNode Tile => _tile != null ? _tile : _tile = GetComponent<GridNode>();
    }
}
