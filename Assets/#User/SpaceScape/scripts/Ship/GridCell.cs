using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The size of one grid cell, and the only place a cell becomes metres or metres become a cell.
    /// Everything else counts in cells.
    public static class GridCell
    {
        /// Length of a cell's side, in metres.
        public const float Size = 0.5f;

        /// From a cell's centre to any of its faces, in metres.
        public const float Half = Size * 0.5f;

        /// The cell a point in the world is in.
        public static Vector3Int ToCell(Vector3 world) => new Vector3Int(
            Mathf.RoundToInt(world.x / Size),
            Mathf.RoundToInt(world.y / Size),
            Mathf.RoundToInt(world.z / Size));

        /// The centre of a cell, in the world.
        public static Vector3 ToWorld(Vector3Int cell) => (Vector3)cell * Size;

        /// The centre of a block of cells, in the world.
        public static Vector3 Centre(Vector3Int min, Vector3Int max) => (Vector3)(min + max) * (Size * 0.5f);
    }
}
