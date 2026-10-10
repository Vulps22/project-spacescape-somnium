using System.Collections.Generic;
using System.Linq;
using SomniumSpace.Worlds.SpaceScape.Player;
using SomniumSpace.Worlds.SpaceScape.Ship;
using Fusion;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// The item conduits' cells for every player (loading.md → Item conduit rules). The master spawns as many
    /// ItemConduitState chunks as the cells need (thirty cells each, never fewer than one) and writes the cells into
    /// them whenever they change, and a few times a second; every other client puts the chunks together in order and
    /// takes the cells from them, late joiners included. A hand on a non-master changes its own cells at once and
    /// ShipNetwork sends the edit to the master; until the master's cells catch up, this client keeps its own.
    [RequireComponent(typeof(ItemConduits))]
    public sealed class ItemConduitNetwork : MonoBehaviour
    {
        private const float SpawnRetrySeconds = 5f;
        private const float EditHoldSeconds = 2f;

        [Tooltip("The ItemConduitState prefab, registered on SceneNetworking.")]
        [SerializeField] private NetworkObject _statePrefab;
        [Tooltip("How many times a second the master writes the cells out and everyone else checks theirs.")]
        [SerializeField] private float _syncsPerSecond = 4f;

        private readonly Dictionary<ItemConduitState, int> _readStamps = new Dictionary<ItemConduitState, int>();
        private ItemConduits _conduits;
        private float _nextSync;
        private float _spawnAsked = -SpawnRetrySeconds;
        private float _authorityAsked = -SpawnRetrySeconds;
        private float _heldUntil;
        private int _stamp;
        private int _writtenRevision = -1;

        private void Awake()
        {
            _conduits = GetComponent<ItemConduits>();
            _conduits.EditedByHand += OnEditedByHand;
        }

        private void OnDestroy()
        {
            if (_conduits != null) _conduits.EditedByHand -= OnEditedByHand;
        }

        private void OnEditedByHand(ItemConduits.Edit edit, Vector3Int cell, int a, int b) =>
            _heldUntil = Time.time + EditHoldSeconds;

        private void Update()
        {
            if (!WorldManager.IsNetworkReady) return;
            var chunks = ItemConduitState.All.Where(c => c.Live).ToList();

            if (PlayerManager.IsMaster)
            {
                var cells = _conduits.Write();
                int needed = Mathf.Max(1, (cells.Length + ItemConduitState.Room - 1) / ItemConduitState.Room);
                if (chunks.Count < needed)
                {
                    if (!WorldManager.CanSpawn || _statePrefab == null || Time.time - _spawnAsked < SpawnRetrySeconds) return;
                    _spawnAsked = Time.time;
                    WorldManager.Spawn(_statePrefab, transform.position, Quaternion.identity, "item conduit state");
                    return;
                }
                foreach (var chunk in chunks)
                {
                    if (chunk.Object.HasStateAuthority) continue;
                    if (Time.time - _authorityAsked < SpawnRetrySeconds) return;
                    _authorityAsked = Time.time;
                    StartCoroutine(WorldManager.TakeAuthority(chunk.Object, _ => { }));
                    return;
                }
                if (_conduits.Revision == _writtenRevision && Time.time < _nextSync) return;
                _nextSync = Time.time + 1f / Mathf.Max(0.1f, _syncsPerSecond);
                Write(chunks, cells);
                return;
            }

            if (chunks.Count == 0 || Time.time < _nextSync) return;
            _nextSync = Time.time + 1f / Mathf.Max(0.1f, _syncsPerSecond);
            Read(chunks);
        }

        /// Gives every chunk its index (those written before keep theirs) and its share of the cells, in order.
        private void Write(List<ItemConduitState> chunks, int[] cells)
        {
            // Chunks already numbered keep their place; new ones (never written) take the next free numbers.
            var ordered = chunks.Where(c => c.Stamp != 0).OrderBy(c => c.Index).Concat(chunks.Where(c => c.Stamp == 0)).ToList();
            _stamp = _stamp == int.MaxValue ? 1 : _stamp + 1;
            for (int index = 0; index < ordered.Count; index++)
            {
                var ints = ordered[index].Data.IntArray;
                int from = index * ItemConduitState.Room;
                int length = Mathf.Clamp(cells.Length - from, 0, ItemConduitState.Room);
                for (int i = 0; i < length; i++)
                    if (ints[i + 3] != cells[from + i]) ints.Set(i + 3, cells[from + i]);
                if (ints[2] != length) ints.Set(2, length);
                if (ints[1] != index) ints.Set(1, index);
                ints.Set(0, _stamp);
            }
            _writtenRevision = _conduits.Revision;
        }

        /// Puts the chunks back together in order and takes the cells, once any has been written again.
        private void Read(List<ItemConduitState> chunks)
        {
            if (Time.time < _heldUntil) return;
            var written = chunks.Where(c => c.Stamp != 0).OrderBy(c => c.Index).ToList();
            if (written.Count == 0) return;
            bool fresh = false;
            foreach (var chunk in written)
                if (!_readStamps.TryGetValue(chunk, out var seen) || seen != chunk.Stamp) { fresh = true; _readStamps[chunk] = chunk.Stamp; }
            if (!fresh) return;

            var cells = new List<int>();
            foreach (var chunk in written)
            {
                var ints = chunk.Data.IntArray;
                int length = Mathf.Clamp(ints[2], 0, ItemConduitState.Room);
                for (int i = 0; i < length; i++) cells.Add(ints[i + 3]);
            }
            _conduits.Read(cells.ToArray());
        }
    }
}
