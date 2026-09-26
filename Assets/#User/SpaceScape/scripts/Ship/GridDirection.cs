using UnityEngine;

namespace SpaceScape.Ship
{
    /// A face of a tile. Tiles sit one unit apart on integers, so a face is also its neighbour's offset.
    public enum GridDirection
    {
        None = 0,
        XPlus,
        XMinus,
        YPlus,
        YMinus,
        ZPlus,
        ZMinus,
    }

    /// Turns a face into the step that reaches the tile on the other side of it.
    public static class GridDirections
    {
        public static Vector3Int Offset(this GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.XPlus:  return new Vector3Int(1, 0, 0);
                case GridDirection.XMinus: return new Vector3Int(-1, 0, 0);
                case GridDirection.YPlus:  return new Vector3Int(0, 1, 0);
                case GridDirection.YMinus: return new Vector3Int(0, -1, 0);
                case GridDirection.ZPlus:  return new Vector3Int(0, 0, 1);
                case GridDirection.ZMinus: return new Vector3Int(0, 0, -1);
                default: return Vector3Int.zero;
            }
        }

        /// The world direction of a face, for pointing a mesh down it.
        public static Vector3 Vector(this GridDirection direction)
        {
            Vector3Int o = direction.Offset();
            return new Vector3(o.x, o.y, o.z);
        }
    }
}
