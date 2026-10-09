using SomniumSpace.Worlds.SpaceScape.Player;
using SomniumSpace.Worlds.SpaceScape.Ship;
using Fusion;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// The item conduits' runs for every player (loading.md → Item conduits, Networking). The master spawns one
    /// ItemConduitState and writes the runs into it whenever they change, and a few times a second; every other
    /// client takes them from it, late joiners included. A hand on a non-master changes its own runs at once and
    /// ShipNetwork sends the edit to the master; until the master's runs catch up, this client keeps its own.
    [RequireComponent(typeof(ItemConduits))]
    public sealed class ItemConduitNetwork : MonoBehaviour
    {
        private const float SpawnRetrySeconds = 5f;
        private const float EditHoldSeconds = 2f;

        [Tooltip("The ItemConduitState prefab, registered on SceneNetworking.")]
        [SerializeField] private NetworkObject _statePrefab;
        [Tooltip("How many times a second the master writes the runs out and everyone else checks theirs.")]
        [SerializeField] private float _syncsPerSecond = 4f;

        private ItemConduits _conduits;
        private float _nextSync;
        private float _spawnAsked = -SpawnRetrySeconds;
        private float _authorityAsked = -SpawnRetrySeconds;
        private float _heldUntil;
        private int _stamp;
        private int _lastRead;
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

        private void OnEditedByHand(ItemConduits.Edit edit, Vector3Int cell, GridDirection entry, GridDirection exit) =>
            _heldUntil = Time.time + EditHoldSeconds;

        private void Update()
        {
            if (!WorldManager.IsNetworkReady) return;
            var state = ItemConduitState.Current;

            if (PlayerManager.IsMaster)
            {
                if (state == null)
                {
                    if (!WorldManager.CanSpawn || _statePrefab == null || Time.time - _spawnAsked < SpawnRetrySeconds) return;
                    _spawnAsked = Time.time;
                    WorldManager.Spawn(_statePrefab, transform.position, Quaternion.identity, "item conduit state");
                    return;
                }
                if (!state.Live) return;
                if (!state.Object.HasStateAuthority)
                {
                    if (Time.time - _authorityAsked < SpawnRetrySeconds) return;
                    _authorityAsked = Time.time;
                    StartCoroutine(WorldManager.TakeAuthority(state.Object, _ => { }));
                    return;
                }
                if (_conduits.Revision == _writtenRevision && Time.time < _nextSync) return;
                _nextSync = Time.time + 1f / Mathf.Max(0.1f, _syncsPerSecond);
                Write(state);
                return;
            }

            if (state == null || !state.Live || Time.time < _nextSync) return;
            _nextSync = Time.time + 1f / Mathf.Max(0.1f, _syncsPerSecond);
            Read(state);
        }

        private void Write(ItemConduitState state)
        {
            var runs = _conduits.Write();
            var ints = state.Data.IntArray;
            int room = state.Data.IntCount - 2;
            if (runs.Length > room)
            {
                Debug.LogWarning($"ItemConduitNetwork: the runs need {runs.Length} ints but there is room for {room}; not written", this);
                return;
            }
            for (int i = 0; i < runs.Length; i++)
                if (ints[i + 2] != runs[i]) ints.Set(i + 2, runs[i]);
            if (ints[1] != runs.Length) ints.Set(1, runs.Length);
            _stamp = _stamp == int.MaxValue ? 1 : _stamp + 1;
            ints.Set(0, _stamp);
            _writtenRevision = _conduits.Revision;
        }

        private void Read(ItemConduitState state)
        {
            var ints = state.Data.IntArray;
            int stamp = ints[0];
            if (stamp == 0 || stamp == _lastRead || Time.time < _heldUntil) return;
            _lastRead = stamp;
            int length = Mathf.Clamp(ints[1], 0, state.Data.IntCount - 2);
            var runs = new int[length];
            for (int i = 0; i < length; i++) runs[i] = ints[i + 2];
            _conduits.Read(runs);
        }
    }
}
