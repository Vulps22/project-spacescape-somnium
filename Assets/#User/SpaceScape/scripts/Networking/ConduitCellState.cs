using System.Collections.Generic;
using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// One chunk of the ship's conduit cells for every player, spawned by the master as they need more room
    /// (ConduitCellNetwork): a stamp the master bumps with every write (0 until the first), the chunk's index, how many
    /// ints of cells follow, how many chunks there are in all, then that many ints of the cells as ConduitCells writes
    /// them (three a cell).
    public sealed class ConduitCellState : MonoBehaviour
    {
        /// Ints before the cells: stamp, index, length, total chunks.
        public const int Header = 4;

        /// Ints of cells one chunk carries: twenty cells.
        public const int Room = 60;

        [Tooltip("Stamp, index, length, total chunks, then the cells. 64 ints.")]
        [SerializeField] private NetworkBridgeData _data;

        private static readonly List<ConduitCellState> Chunks = new List<ConduitCellState>();

        /// Every chunk in the world that has arrived here.
        public static IReadOnlyList<ConduitCellState> All => Chunks;

        public NetworkBridgeData Data => _data;

        public NetworkObject Object => _data != null ? _data.Object : null;

        public bool Live => Object != null && Object.IsValid;

        /// The stamp of its last write, 0 before the first.
        public int Stamp => Live ? _data.IntArray[0] : 0;

        /// Which chunk it is, once written.
        public int Index => Live ? _data.IntArray[1] : -1;

        /// How many chunks the master was writing when it wrote this one.
        public int Total => Live ? _data.IntArray[3] : 0;

        private void OnEnable() => Chunks.Add(this);

        private void OnDisable() => Chunks.Remove(this);

        private void OnValidate()
        {
            if (_data == null) _data = GetComponent<NetworkBridgeData>();
        }
    }
}
