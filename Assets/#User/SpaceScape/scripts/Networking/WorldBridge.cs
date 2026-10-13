using System;
using System.Collections;
using Fusion;
using Fusion.Addons.Physics;
using SomniumSpace.Worlds.SpaceScape.Community;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// Fusion, for anything that exists in the shared world: spawn, place, despawn, take authority,
    /// find. Only WorldManager calls this. Players and messaging are not here.
    public static class WorldBridge
    {
        /// How long to wait for Fusion to hand over state authority before giving up.
        public const float AuthorityTimeout = 2f;

        /// Whether Fusion is running here at all. False in the Editor without Photon.
        public static bool HasRunner => SceneNetworking.NetworkRunnerRef != null || NetworkRunner.Instances.Count > 0;

        /// The local peer has joined and the scene's network objects are registered.
        public static bool IsNetworkReady => SceneNetworking.IsNetworkReady;

        /// Seconds on the session's shared clock, the same on every client, for things every client works out for
        /// itself at the same moment. Local time while Fusion is not running (the Editor without Photon).
        public static double SharedTime
        {
            get
            {
                var runner = SceneNetworking.NetworkRunnerRef;
                return runner != null && runner.IsRunning ? runner.SimulationTime : Time.timeAsDouble;
            }
        }

        /// Raised when IsNetworkReady becomes true.
        public static event Action NetworkReady
        {
            add { SceneNetworking.OnLocalPlayerJoined += value; }
            remove { SceneNetworking.OnLocalPlayerJoined -= value; }
        }

        /// Whether Fusion can create an object right now. Asks the runner, never our own statics:
        /// spawning early instantiates the prefab and then throws, leaving an orphan.
        public static bool CanSpawn
        {
            get
            {
                var runner = SceneNetworking.NetworkRunnerRef;
                return runner != null && runner.IsRunning && runner.LocalPlayer.IsRealPlayer && SceneNetworking.IsNetworkReady;
            }
        }

        /// Spawns a prefab registered on SceneNetworking and places it. Who owns it is the prefab's
        /// NetworkObject's call (Is Master Client Object, Allow State Authority Override); otherwise the
        /// spawner. Null on any failure, logged against the context.
        public static NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, string context)
        {
            if (prefab == null)
            {
                Debug.LogError($"WorldBridge.Spawn '{context}': no prefab given");
                return null;
            }

            var net = SceneNetworking.Instance;
            var runner = SceneNetworking.NetworkRunnerRef;
            if (net == null || runner == null) return null;

            if (!net.NetworkPrefabs.TryGetValue(prefab, out NetworkPrefabId prefabId))
            {
                Debug.LogError($"WorldBridge.Spawn '{context}': '{prefab.name}' is not registered on SceneNetworking");
                return null;
            }

            NetworkObject spawned;
            try
            {
                spawned = runner.Spawn(prefabId, position, rotation);
            }
            catch (Exception e)
            {
                Debug.LogError($"WorldBridge.Spawn '{context}': Fusion threw spawning '{prefab.name}': {e.Message}");
                return null;
            }

            if (spawned != null) Place(spawned, position, rotation);
            return spawned;
        }

        /// Moves an object in both its transform and its network state. A kinematic
        /// NetworkRigidbody3D ignores a transform write and snaps back, so it is teleported.
        public static void Place(NetworkObject obj, Vector3 position, Quaternion rotation)
        {
            obj.transform.SetPositionAndRotation(position, rotation);
            if (obj.TryGetComponent<NetworkRigidbody3D>(out var body)) body.Teleport(position, rotation);
        }

        /// Removes an object. Warns and returns false when it cannot: Fusion's own Despawn does
        /// nothing without state authority and says nothing about it.
        public static bool Despawn(NetworkObject obj, string context)
        {
            if (obj == null) return false;

            var runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null)
            {
                Debug.LogWarning($"WorldBridge.Despawn '{context}': no runner; '{obj.name}' left standing");
                return false;
            }
            if (!obj.HasStateAuthority)
            {
                Debug.LogWarning($"WorldBridge.Despawn '{context}': no state authority over '{obj.name}'");
                return false;
            }

            runner.Despawn(obj);
            return true;
        }

        /// Removes an object only if this client simulates it, silently otherwise. For code every
        /// client runs where exactly one is meant to act.
        public static bool DespawnIfStateAuthority(NetworkObject obj)
        {
            var runner = SceneNetworking.NetworkRunnerRef;
            if (obj == null || runner == null || !obj.HasStateAuthority) return false;
            runner.Despawn(obj);
            return true;
        }

        /// The object with this network id, or null.
        public static NetworkObject Find(uint rawId)
        {
            var runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null || rawId == 0) return null;
            return runner.TryFindObject(new NetworkId { Raw = rawId }, out NetworkObject obj) ? obj : null;
        }

        /// Requests state authority and waits up to AuthorityTimeout for it.
        ///
        ///     bool ok = false;
        ///     yield return WorldBridge.TakeAuthority(obj, r => ok = r);
        public static IEnumerator TakeAuthority(NetworkObject obj, Action<bool> granted)
        {
            if (obj == null)
            {
                granted?.Invoke(false);
                yield break;
            }

            if (!obj.HasStateAuthority)
            {
                obj.RequestStateAuthority();

                // Null-tested each pass: reading HasStateAuthority on a despawned object throws.
                float waited = 0f;
                while (obj != null && !obj.HasStateAuthority && waited < AuthorityTimeout)
                {
                    yield return null;
                    waited += Time.deltaTime;
                }
            }

            granted?.Invoke(obj != null && obj.HasStateAuthority);
        }
    }
}
