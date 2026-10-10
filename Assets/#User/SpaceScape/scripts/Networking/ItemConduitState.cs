using System.Collections.Generic;
using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// One chunk of the item conduits' cells for every player, spawned by the master as they need more room
    /// (ItemConduitNetwork): a stamp the master bumps with every write (0 until the first), the chunk's index, how
    /// many ints follow, then that many of the cells as ItemConduits writes them (two ints a cell).
    public sealed class ItemConduitState : MonoBehaviour
    {
        /// Ints of cells one chunk carries: thirty cells.
        public const int Room = 60;

        [Tooltip("Stamp, index, length, then the cells. 64 ints.")]
        [SerializeField] private NetworkBridgeData _data;

        private static readonly List<ItemConduitState> Chunks = new List<ItemConduitState>();

        /// Every chunk in the world that has arrived here.
        public static IReadOnlyList<ItemConduitState> All => Chunks;

        public NetworkBridgeData Data => _data;

        public NetworkObject Object => _data != null ? _data.Object : null;

        public bool Live => Object != null && Object.IsValid;

        /// The stamp of its last write, 0 before the first.
        public int Stamp => Live ? _data.IntArray[0] : 0;

        /// Which chunk it is, once written.
        public int Index => Live ? _data.IntArray[1] : -1;

        private void OnEnable() => Chunks.Add(this);

        private void OnDisable() => Chunks.Remove(this);

        private void OnValidate()
        {
            if (_data == null) _data = GetComponent<NetworkBridgeData>();
        }
    }
}
