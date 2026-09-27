using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// One drawn conduit, packed into a single 64-bit number so its cell can never fall out of step with
    /// its faces. Lowest bit first:
    ///
    ///   0-11   faces +X -X +Y -Y +Z -Z, two bits each: In, then Out
    ///   12-18  addon id: 0 none, else an index into the layout's addon list, plus one
    ///   19-21  addon facing (a GridDirection)
    ///   22-23  addon quarter turns
    ///   24     addon parked
    ///   25-63  x, y, z: 13 bits each, offset by 4096, so each runs from -4096 to 4095 cells
    public struct ConduitCode
    {
        public const int MinCell = -4096;
        public const int MaxCell = 4095;
        public const int MaxAddon = 127;

        private const int AddonShift = 12;
        private const int FacingShift = 19;
        private const int TurnsShift = 22;
        private const int ParkedShift = 24;
        private const int XShift = 25;
        private const int YShift = 38;
        private const int ZShift = 51;
        private const ulong CoordMask = 0x1FFF;
        private const int CoordOffset = 4096;

        public Vector3Int Cell;
        public int Faces;
        public int Addon;
        public GridDirection AddonFacing;
        public int AddonTurns;
        public bool AddonParked;

        /// Which way power may cross a face. A face packed as both In and Out is read as None.
        public FlowDirection GetFace(GridDirection face)
        {
            int bits = (Faces >> Shift(face)) & 3;
            return bits == 1 ? FlowDirection.In : bits == 2 ? FlowDirection.Out : FlowDirection.None;
        }

        /// Sets which way power may cross a face.
        public void SetFace(GridDirection face, FlowDirection flow)
        {
            if (face == GridDirection.None) return;
            int shift = Shift(face);
            Faces &= ~(3 << shift);
            if (flow == FlowDirection.In) Faces |= 1 << shift;
            else if (flow == FlowDirection.Out) Faces |= 2 << shift;
        }

        /// True when some face is packed as both In and Out, which no conduit can be.
        public bool HasContradiction
        {
            get
            {
                for (int i = 0; i < 6; i++)
                    if (((Faces >> (i * 2)) & 3) == 3) return true;
                return false;
            }
        }

        /// True when the cell fits in the thirteen bits each axis has.
        public bool InRange => Fits(Cell.x) && Fits(Cell.y) && Fits(Cell.z);

        private static bool Fits(int v) => v >= MinCell && v <= MaxCell;

        private static int Shift(GridDirection face) => ((int)face - 1) * 2;

        /// Packs it. Out-of-range values are masked, so check InRange first.
        public long Pack()
        {
            ulong bits = (ulong)(uint)(Faces & 0xFFF)
                | (ulong)(uint)(Mathf.Clamp(Addon, 0, MaxAddon) & 0x7F) << AddonShift
                | (ulong)(uint)((int)AddonFacing & 0x7) << FacingShift
                | (ulong)(uint)(AddonTurns & 0x3) << TurnsShift
                | (AddonParked ? 1UL : 0UL) << ParkedShift
                | ((ulong)(uint)(Cell.x + CoordOffset) & CoordMask) << XShift
                | ((ulong)(uint)(Cell.y + CoordOffset) & CoordMask) << YShift
                | ((ulong)(uint)(Cell.z + CoordOffset) & CoordMask) << ZShift;
            return (long)bits;
        }

        /// Unpacks one.
        public static ConduitCode Unpack(long packed)
        {
            ulong bits = (ulong)packed;
            return new ConduitCode
            {
                Faces = (int)(bits & 0xFFF),
                Addon = (int)((bits >> AddonShift) & 0x7F),
                AddonFacing = (GridDirection)((bits >> FacingShift) & 0x7),
                AddonTurns = (int)((bits >> TurnsShift) & 0x3),
                AddonParked = ((bits >> ParkedShift) & 1) != 0,
                Cell = new Vector3Int(
                    (int)((bits >> XShift) & CoordMask) - CoordOffset,
                    (int)((bits >> YShift) & CoordMask) - CoordOffset,
                    (int)((bits >> ZShift) & CoordMask) - CoordOffset),
            };
        }
    }
}
