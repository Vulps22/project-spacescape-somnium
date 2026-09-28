using System.Collections.Generic;
using Fusion;
using SomniumSpace.Worlds.SpaceScape.Player;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// The conduits' state for every player (docs/networking.md, The ship's conduits).
    ///
    /// Every client lists the conduits in the same order, by cell, so a conduit's place in that list is its
    /// slot and nothing else identifies it. The master spawns ConduitChunks and writes every slot into them
    /// a few times a second; every other client compares each of its conduits with its slot and corrects
    /// whatever differs, its own pops included. Installed addons are their own objects (AddonNetwork): the
    /// master spawns one for each addon on a conduit and despawns it when the addon comes off.
    ///
    /// A client that has joined but not yet received every chunk holds the grid still: it cannot simulate
    /// any of the ship until it has all of it. With no network at all, the grid simply runs.
    [RequireComponent(typeof(PowerGrid))]
    public sealed class ConduitNetwork : MonoBehaviour
    {
        [Tooltip("The ConduitChunk prefab, registered on SceneNetworking.")]
        [SerializeField] private NetworkObject _chunkPrefab;
        [Tooltip("The AddonNetwork prefab, registered on SceneNetworking.")]
        [SerializeField] private NetworkObject _addonPrefab;
        [Tooltip("How many times a second the master writes the conduits out and everyone else checks theirs.")]
        [SerializeField] private float _syncsPerSecond = 4f;

        private const int PoppedBit = 12;
        private const int AddonBit = 13;
        private const float HeatStep = 0.05f;   // degrees per unit: 16 bits cover -100 to about 3177 C
        private const float HeatFloor = -100f;
        private const float SpawnRetrySeconds = 5f;
        private const float EditHoldSeconds = 2f;

        private static readonly GridDirection[] Faces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        private static ConduitNetwork _current;

        private PowerGrid _grid;
        private ConduitLayout _layout;
        private GridNode[] _slots;
        private readonly Dictionary<GridNode, int> _slotOf = new Dictionary<GridNode, int>();
        private readonly Dictionary<int, float> _chunkAsked = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _addonAsked = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _heldUntil = new Dictionary<int, float>();
        private float _nextSync;
        private bool _joined;

        /// The conduit in a slot, or null.
        public static GridNode TileAt(int slot) =>
            _current != null && _current._slots != null && slot >= 0 && slot < _current._slots.Length ? _current._slots[slot] : null;

        /// A conduit's slot, or -1.
        public static int SlotOf(GridNode tile) =>
            _current != null && tile != null && _current._slotOf.TryGetValue(tile, out int slot) ? slot : -1;

        /// True while a conduit changed by hand on this client is kept against corrections, so the master has
        /// time to agree before an older value undoes the change.
        public static bool IsHeld(int slot)
        {
            if (_current == null || !_current._heldUntil.TryGetValue(slot, out float until)) return false;
            if (Time.time < until) return true;
            _current._heldUntil.Remove(slot);
            return false;
        }

        /// A hand on this client changed a conduit. Holds it against corrections for a moment and returns what
        /// to ask the master for, as it stands now: slot, segments, addon id, addon pose and addon state.
        /// Null when the conduit has no slot.
        public static int[] DescribeEdit(GridNode tile)
        {
            int slot = SlotOf(tile);
            if (slot < 0) return null;
            _current._heldUntil[slot] = Time.time + EditHoldSeconds;

            int id = 0, pose = 0, state = 0;
            if (tile.TryGetComponent<AddonModule>(out var module) && module.Addon != null)
            {
                id = AddonId(module.Installed);
                pose = AddonPose(module);
                state = module.Addon.NetworkState;
            }
            return new[] { slot, Pack(tile), id, pose, state };
        }

        /// Makes a conduit match what a player asked for. Master only. The chunks and addon objects carry the
        /// result to everyone on the next sync.
        public static void ApplyEdit(int[] edit)
        {
            if (_current == null || edit == null || edit.Length < 5 || !PlayerManager.IsMaster) return;
            var tile = TileAt(edit[0]);
            if (tile == null) return;
            var grid = _current._grid;

            // Faces, as the hologram commits them: a blown conduit given an output again is mended.
            var edges = tile.Edges;
            if (edges != null)
            {
                bool hasOutput = false, changed = false;
                for (int i = 0; i < Faces.Length; i++)
                {
                    int two = (edit[1] >> (i * 2)) & 3;
                    var flow = two == 1 ? FlowDirection.In : two == 2 ? FlowDirection.Out : FlowDirection.None;
                    hasOutput |= flow == FlowDirection.Out;
                    if (edges.GetFace(Faces[i]) == flow) continue;
                    edges.SetFace(Faces[i], flow);
                    changed = true;
                }
                if (hasOutput && tile.Node != null && tile.Node.IsPopped) { grid.Repair(tile); changed = true; }
                if (changed) grid.Rewire(tile);
            }

            if (!tile.TryGetComponent<AddonModule>(out var module)) return;
            var addons = Addons;
            var prefab = addons != null && edit[2] > 0 && edit[2] <= addons.Count ? addons[edit[2] - 1] : null;
            if (prefab == null)
            {
                if (module.Addon != null) module.Remove();
                return;
            }

            var facing = (GridDirection)(edit[3] & 7);
            if (module.Installed != prefab && module.Addon != null) module.Remove();
            if (module.Addon == null) module.Install(prefab, facing);
            module.Arrange(facing, (edit[3] >> 3) & 3, (edit[3] & (1 << 5)) != 0);
            if (module.Addon != null) module.Addon.NetworkState = edit[4];
        }

        /// An addon prefab's id: its place in the layout's list, plus one. 0 for none.
        public static int AddonId(ConduitAddon prefab)
        {
            var addons = Addons;
            if (addons == null || prefab == null) return 0;
            for (int i = 0; i < addons.Count; i++)
                if (addons[i] == prefab) return i + 1;
            return 0;
        }

        /// An addon's facing, quarter turns and parked, in one int: facing | turns << 3 | parked << 5.
        public static int AddonPose(AddonModule module) =>
            (int)module.Facing | (module.Turns << 3) | (module.Parked ? 1 << 5 : 0);

        /// The addons a conduit can have, in the order their ids count.
        public static IReadOnlyList<ConduitAddon> Addons => _current != null && _current._layout != null ? _current._layout.Addons : null;

        private void Awake()
        {
            _current = this;
            _grid = GetComponent<PowerGrid>();
            _layout = FindFirstObjectByType<ConduitLayout>();
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        // After every Awake, so the layout has built its conduits and the grid has bound them.
        private void Start()
        {
            var conduits = new List<GridNode>();
            foreach (var module in FindObjectsByType<ConduitBehaviourModule>(FindObjectsSortMode.None))
                conduits.Add(module.Tile);
            conduits.Sort((a, b) => Compare(a.Coordinate, b.Coordinate));
            _slots = conduits.ToArray();
            for (int i = 0; i < _slots.Length; i++) _slotOf[_slots[i]] = i;

            // Every client must list the same cells in the same order, or every edit lands on the wrong cable.
            uint hash = 2166136261;
            foreach (var tile in _slots)
            {
                var c = tile.Coordinate;
                hash = (hash ^ (uint)c.x) * 16777619;
                hash = (hash ^ (uint)c.y) * 16777619;
                hash = (hash ^ (uint)c.z) * 16777619;
            }
            Debug.Log($"ConduitNetwork: {_slots.Length} conduit slots in {ChunkCount} chunk(s), order hash {hash:x8}");
        }

        private int ChunkCount => (_slots.Length + ConduitChunk.Size - 1) / ConduitChunk.Size;

        private static int Compare(Vector3Int a, Vector3Int b) =>
            a.x != b.x ? a.x.CompareTo(b.x) : a.y != b.y ? a.y.CompareTo(b.y) : a.z.CompareTo(b.z);

        private void Update()
        {
            if (_slots == null || _grid.Graph == null) return;

            // Only one grid rolls for failures; everyone else is told by the corrections.
            _grid.Graph.DecidesFailures = !WorldManager.IsNetworkReady || PlayerManager.IsMaster;

            if (!WorldManager.IsNetworkReady)
            {
                _grid.Held = false;
                return;
            }

            if (PlayerManager.IsMaster)
            {
                _grid.Held = false;
                _joined = true;
                SpawnMissingChunks();
                if (Time.time < _nextSync) return;
                _nextSync = Time.time + 1f / Mathf.Max(0.1f, _syncsPerSecond);
                Write();
                SyncAddonObjects();
                return;
            }

            if (Time.time < _nextSync && _joined) return;
            _nextSync = Time.time + 1f / Mathf.Max(0.1f, _syncsPerSecond);
            bool complete = Read();
            if (complete) _joined = true;
            _grid.Held = !_joined;
        }

        private void SpawnMissingChunks()
        {
            if (!WorldManager.CanSpawn || _chunkPrefab == null) return;
            for (int i = 0; i < ChunkCount; i++)
            {
                if (ConduitChunk.Find(i) != null) continue;
                if (_chunkAsked.TryGetValue(i, out float at) && Time.time - at < SpawnRetrySeconds) continue;
                _chunkAsked[i] = Time.time;

                var spawned = WorldManager.Spawn(_chunkPrefab, transform.position, Quaternion.identity, $"conduit chunk {i}");
                if (spawned != null && spawned.TryGetComponent<ConduitChunk>(out var chunk)) chunk.Claim(i);
            }
        }

        private void Write()
        {
            for (int c = 0; c < ChunkCount; c++)
            {
                var chunk = ConduitChunk.Find(c);
                if (chunk == null || !chunk.HasStateAuthority) continue;

                int first = c * ConduitChunk.Size;
                int last = Mathf.Min(_slots.Length, first + ConduitChunk.Size);
                for (int slot = first; slot < last; slot++)
                {
                    var tile = _slots[slot];
                    chunk.SetSegments(slot - first, Pack(tile));
                    chunk.SetHeat(slot - first, Quantise(tile.Node != null ? tile.Node.Celsius : 0.0));
                }
                chunk.MarkWritten();
            }
        }

        /// Brings every conduit into line with its slot. True once every chunk has been read.
        private bool Read()
        {
            bool complete = true;
            for (int c = 0; c < ChunkCount; c++)
            {
                var chunk = ConduitChunk.Find(c);
                if (chunk == null || chunk.Index != c) { complete = false; continue; }

                int first = c * ConduitChunk.Size;
                int last = Mathf.Min(_slots.Length, first + ConduitChunk.Size);
                for (int slot = first; slot < last; slot++)
                {
                    var tile = _slots[slot];
                    if (!IsHeld(slot)) Apply(tile, chunk.GetSegments(slot - first));
                    if (tile.Node != null) tile.Node.Celsius = HeatFrom(chunk.GetHeat(slot - first));
                }
            }
            return complete;
        }

        /// A conduit's faces (2 bits each, In 1 and Out 2, as ConduitCode packs them), severed and has-addon.
        private static ushort Pack(GridNode tile)
        {
            int bits = 0;
            var edges = tile.Edges;
            if (edges != null)
                for (int i = 0; i < Faces.Length; i++)
                {
                    var flow = edges.GetFace(Faces[i]);
                    if (flow == FlowDirection.In) bits |= 1 << (i * 2);
                    else if (flow == FlowDirection.Out) bits |= 2 << (i * 2);
                }
            if (tile.Node != null && tile.Node.IsPopped) bits |= 1 << PoppedBit;
            if (tile.TryGetComponent<AddonModule>(out var slot) && slot.Addon != null) bits |= 1 << AddonBit;
            return (ushort)bits;
        }

        private void Apply(GridNode tile, ushort bits)
        {
            if (Pack(tile) == bits) return;

            var edges = tile.Edges;
            bool rewire = false;
            if (edges != null)
                for (int i = 0; i < Faces.Length; i++)
                {
                    int two = (bits >> (i * 2)) & 3;
                    var flow = two == 1 ? FlowDirection.In : two == 2 ? FlowDirection.Out : FlowDirection.None;
                    if (edges.GetFace(Faces[i]) == flow) continue;
                    edges.SetFace(Faces[i], flow);
                    rewire = true;
                }

            _grid.Graph.CorrectPopped(tile.Node, (bits & (1 << PoppedBit)) != 0);
            if (rewire) _grid.Rewire(tile);

            // An addon that came off elsewhere comes off here. One that went on arrives as its own object.
            if ((bits & (1 << AddonBit)) == 0 && tile.TryGetComponent<AddonModule>(out var slot) && slot.Addon != null)
                slot.Remove();
        }

        /// Spawns an addon object for every addon on a conduit that has none, and despawns any whose conduit's
        /// addon has come off.
        private void SyncAddonObjects()
        {
            if (_addonPrefab == null) return;

            for (int slot = 0; slot < _slots.Length; slot++)
            {
                if (!_slots[slot].TryGetComponent<AddonModule>(out var module) || module.Addon == null) continue;
                if (AddonNetwork.Find(slot) != null || !WorldManager.CanSpawn) continue;
                if (_addonAsked.TryGetValue(slot, out float at) && Time.time - at < SpawnRetrySeconds) continue;
                _addonAsked[slot] = Time.time;

                var spawned = WorldManager.Spawn(_addonPrefab, _slots[slot].transform.position, Quaternion.identity, $"addon on slot {slot}");
                if (spawned != null && spawned.TryGetComponent<AddonNetwork>(out var addon)) addon.Claim(slot);
            }

            foreach (var addon in AddonNetwork.Owned())
            {
                var tile = TileAt(addon.Slot);
                if (tile == null || !tile.TryGetComponent<AddonModule>(out var module) || module.Addon == null)
                    WorldManager.Despawn(addon.Object, $"addon on slot {addon.Slot} came off");
            }
        }

        private static ushort Quantise(double celsius) =>
            (ushort)Mathf.Clamp(Mathf.RoundToInt(((float)celsius - HeatFloor) / HeatStep), 0, ushort.MaxValue);

        private static double HeatFrom(ushort units) => HeatFloor + units * HeatStep;
    }
}
