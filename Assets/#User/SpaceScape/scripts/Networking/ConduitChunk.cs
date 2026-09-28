using System.Collections.Generic;
using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// 128 conduits' state, spawned by the master: each conduit's segments, severed and has-addon bits in
    /// one 16-bit value, and its heat in another, two to an int. Which 128 is written into its own index
    /// once the master has filled it, so a client never reads a chunk before it holds anything.
    public sealed class ConduitChunk : MonoBehaviour
    {
        public const int Size = 128;

        [Tooltip("Which chunk this is, plus one: 0 until the master has written it. One int.")]
        [SerializeField] private NetworkBridgeData _index;
        [Tooltip("Each conduit's segments, severed and has-addon bits, 16 bits each. 64 ints.")]
        [SerializeField] private NetworkBridgeData _segments;
        [Tooltip("Each conduit's heat, 16 bits each. 64 ints.")]
        [SerializeField] private NetworkBridgeData _heat;

        private static readonly List<ConduitChunk> All = new List<ConduitChunk>();
        private int _claimed = -1;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public NetworkObject Object => _index != null ? _index.Object : null;

        private bool Live => Object != null && Object.IsValid;

        public bool HasStateAuthority => Live && Object.HasStateAuthority;

        /// Which chunk this is, or -1 until the master has written it.
        public int Index => Live && _index.IntArray[0] > 0 ? _index.IntArray[0] - 1 : -1;

        /// The chunk with this index, or null. The master finds one it has just spawned by its claim.
        public static ConduitChunk Find(int index)
        {
            foreach (var chunk in All)
                if (chunk.Index == index || chunk._claimed == index) return chunk;
            return null;
        }

        /// Marks a freshly spawned chunk as the master's for this index.
        public void Claim(int index) => _claimed = index;

        /// Tells every client this chunk holds real values now.
        public void MarkWritten()
        {
            if (HasStateAuthority && _claimed >= 0 && Index != _claimed) _index.IntArray.Set(0, _claimed + 1);
        }

        public ushort GetSegments(int i) => Get(_segments, i);
        public void SetSegments(int i, ushort value) => Set(_segments, i, value);
        public ushort GetHeat(int i) => Get(_heat, i);
        public void SetHeat(int i, ushort value) => Set(_heat, i, value);

        private static ushort Get(NetworkBridgeData data, int i)
        {
            int word = data.IntArray[i >> 1];
            return (ushort)((word >> ((i & 1) * 16)) & 0xFFFF);
        }

        private static void Set(NetworkBridgeData data, int i, ushort value)
        {
            var ints = data.IntArray;
            int shift = (i & 1) * 16;
            int word = ints[i >> 1];
            int next = (word & ~(0xFFFF << shift)) | (value << shift);
            if (next != word) ints.Set(i >> 1, next);
        }
    }
}
