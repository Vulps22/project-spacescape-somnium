using System.Collections.Generic;
using System.Linq;
using SomniumSpace.Worlds.SpaceScape.Player;
using SomniumSpace.Worlds.SpaceScape.Ship;
using Fusion;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// The ship's conduit cells for every player (conduits.md → Networking): power and item conduits alike, by their
    /// cell. The master owns them: it spawns as many ConduitCellState chunks as the cells need (twenty cells each, never
    /// fewer than one) and writes the cells into them whenever they change, and a few times a second for the power
    /// conduits' heat. Every other client puts the chunks together in order and takes the cells from them.
    ///
    /// A hand on a non-master changes its own cell at once (ConduitCells.EditByHand) and ShipNetwork sends the edit to
    /// the master; until the master's cells catch up, that cell is kept as this client has it.
    ///
    /// A client that has joined but not yet read every chunk holds the grid still: it cannot simulate any of the ship
    /// until it has all of it. Only the master's grid rolls for failures, and only the master uninstalls a loose conduit
    /// and drops its addon as a spawned item. With no network at all, the grid simply runs and this client decides.
    [RequireComponent(typeof(ConduitCells))]
    public sealed class ConduitCellNetwork : MonoBehaviour
    {
        private const float SpawnRetrySeconds = 5f;
        private const float HeldReportSeconds = 10f;

        [Tooltip("The ConduitCellState prefab, registered on SceneNetworking.")]
        [SerializeField] private NetworkObject _statePrefab;
        [Tooltip("How many times a second the master writes the cells out (for heat) and everyone else checks theirs.")]
        [SerializeField] private float _syncsPerSecond = 4f;

        private readonly Dictionary<ConduitCellState, int> _readStamps = new Dictionary<ConduitCellState, int>();
        private readonly List<ConduitCellState> _chunks = new List<ConduitCellState>();
        private ConduitCells _cells;
        private float _nextSync;
        private float _spawnAsked = -SpawnRetrySeconds;
        private float _authorityAsked = -SpawnRetrySeconds;
        private float _nextHeldReport;
        private int _stamp;
        private int _writtenRevision = -1;
        private bool _joined;

        private void Awake()
        {
            _cells = GetComponent<ConduitCells>();
            _cells.AddonDropped += Drop;
        }

        private void OnDestroy()
        {
            if (_cells != null) _cells.AddonDropped -= Drop;
        }

        /// A conduit holding an addon was uninstalled here (where cells are decided): its addon falls out as an item
        /// where the conduit was, spawned for everyone, or made locally with no network.
        private void Drop(Vector3Int cell, ConduitAddon addon, int state)
        {
            var prefab = addon != null ? addon.Item : null;
            if (prefab == null) return;
            var at = GridCell.ToWorld(cell);
            AddonItem item = null;
            if (LocalZone.Contains(cell)) LocalZone.MakeLocal(prefab.gameObject, at, Quaternion.identity).TryGetComponent(out item);
            else if (WorldManager.IsNetworkReady)
            {
                if (!WorldManager.CanSpawn || !prefab.TryGetComponent<NetworkObject>(out var networked)) return;
                var spawned = WorldManager.Spawn(networked, at, Quaternion.identity, $"addon item '{prefab.name}' dropped by an uninstalled conduit");
                if (spawned != null) spawned.TryGetComponent(out item);
            }
            else item = Instantiate(prefab, at, Quaternion.identity);
            if (item != null && item.CarriesState) item.State = state;   // a junction's count goes with it
        }

        private void Update()
        {
            var grid = PowerGrid.Current;
            bool decides = !WorldManager.IsNetworkReady || PlayerManager.IsMaster;
            if (grid != null && grid.Graph != null) grid.Graph.DecidesFailures = decides;
            _cells.Decides = decides;

            if (!WorldManager.IsNetworkReady)
            {
                if (grid != null) grid.Held = false;
                return;
            }

            _chunks.Clear();
            foreach (var chunk in ConduitCellState.All) if (chunk.Live) _chunks.Add(chunk);

            if (PlayerManager.IsMaster)
            {
                _joined = true;
                if (grid != null) grid.Held = false;
                var cells = _cells.Write();
                int needed = Mathf.Max(1, (cells.Length + ConduitCellState.Room - 1) / ConduitCellState.Room);
                if (_chunks.Count < needed)
                {
                    if (!WorldManager.CanSpawn || _statePrefab == null || Time.time - _spawnAsked < SpawnRetrySeconds) return;
                    _spawnAsked = Time.time;
                    WorldManager.Spawn(_statePrefab, transform.position, Quaternion.identity, "conduit cell state");
                    return;
                }
                foreach (var chunk in _chunks)
                {
                    if (chunk.Object.HasStateAuthority) continue;
                    if (Time.time - _authorityAsked < SpawnRetrySeconds) return;
                    _authorityAsked = Time.time;
                    StartCoroutine(WorldManager.TakeAuthority(chunk.Object, _ => { }));
                    return;
                }
                if (_cells.Revision == _writtenRevision && Time.time < _nextSync) return;
                _nextSync = Time.time + 1f / Mathf.Max(0.1f, _syncsPerSecond);
                Write(cells, needed);
                return;
            }

            if (Time.time >= _nextSync || !_joined)
            {
                _nextSync = Time.time + 1f / Mathf.Max(0.1f, _syncsPerSecond);
                bool complete = Read();
                if (complete && !_joined) Debug.Log($"ConduitCellNetwork: every chunk read at {Time.timeSinceLevelLoad:0.0}s ({_cells.CellCount} cells); the grid runs");
                if (complete) _joined = true;
            }
            if (grid != null) grid.Held = !_joined;
            if (!_joined && Time.time >= _nextHeldReport) ReportHeld();
        }

        /// Gives every chunk its index (those written before keep theirs) and its share of the cells, in order.
        private void Write(int[] cells, int needed)
        {
            // Chunks already numbered keep their place; new ones (never written) take the next free numbers.
            var ordered = _chunks.Where(c => c.Stamp != 0).OrderBy(c => c.Index).Concat(_chunks.Where(c => c.Stamp == 0)).ToList();
            _stamp = _stamp == int.MaxValue ? 1 : _stamp + 1;
            int total = Mathf.Min(ordered.Count, needed);
            for (int index = 0; index < ordered.Count; index++)
            {
                var ints = ordered[index].Data.IntArray;
                int from = index * ConduitCellState.Room;
                int length = index < total ? Mathf.Clamp(cells.Length - from, 0, ConduitCellState.Room) : 0;
                for (int i = 0; i < length; i++)
                    if (ints[ConduitCellState.Header + i] != cells[from + i]) ints.Set(ConduitCellState.Header + i, cells[from + i]);
                if (ints[3] != total) ints.Set(3, total);
                if (ints[2] != length) ints.Set(2, length);
                if (ints[1] != index) ints.Set(1, index);
                ints.Set(0, _stamp);
            }
            _writtenRevision = _cells.Revision;
        }

        /// Puts the chunks back together in order and takes the cells, once any has been written again. True once
        /// every chunk the master is writing has arrived and been read.
        private bool Read()
        {
            var written = _chunks.Where(c => c.Stamp != 0).OrderBy(c => c.Index).ToList();
            if (written.Count == 0) return false;
            int total = written.Max(c => c.Total);
            for (int index = 0; index < total; index++)
                if (written.All(c => c.Index != index)) return false;   // not all here yet: wait for the rest

            bool fresh = !_joined;
            foreach (var chunk in written)
                if (!_readStamps.TryGetValue(chunk, out var seen) || seen != chunk.Stamp) { fresh = true; _readStamps[chunk] = chunk.Stamp; }
            if (!fresh) return true;

            var cells = new List<int>();
            foreach (var chunk in written)
            {
                if (chunk.Index >= total) continue;
                var ints = chunk.Data.IntArray;
                int length = Mathf.Clamp(ints[2], 0, ConduitCellState.Room);
                for (int i = 0; i < length; i++) cells.Add(ints[ConduitCellState.Header + i]);
            }
            _cells.Read(cells.ToArray());
            return true;
        }

        /// Says, every so often, why a joined client's grid is still held.
        private void ReportHeld()
        {
            _nextHeldReport = Time.time + HeldReportSeconds;
            if (Time.timeSinceLevelLoad < HeldReportSeconds) return;
            Debug.LogWarning($"ConduitCellNetwork: grid still held at {Time.timeSinceLevelLoad:0}s: {_chunks.Count} chunk(s) here, not all written or arrived yet");
        }
    }
}
