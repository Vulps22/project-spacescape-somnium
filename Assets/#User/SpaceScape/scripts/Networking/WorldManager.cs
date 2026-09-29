using System;
using System.Collections;
using Fusion;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// What the rest of the game calls to create, remove and take control of networked objects.
    /// Forwards to WorldBridge, so a guard or correction lands here once rather than in every caller.
    public static class WorldManager
    {
        public static bool IsNetworkReady => WorldBridge.IsNetworkReady;
        public static bool HasRunner => WorldBridge.HasRunner;
        public static bool CanSpawn => WorldBridge.CanSpawn;

        public static event Action NetworkReady
        {
            add { WorldBridge.NetworkReady += value; }
            remove { WorldBridge.NetworkReady -= value; }
        }

        public static NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, string context) =>
            WorldBridge.Spawn(prefab, position, rotation, context);

        public static void Place(NetworkObject obj, Vector3 position, Quaternion rotation) =>
            WorldBridge.Place(obj, position, rotation);

        public static bool Despawn(NetworkObject obj, string context) => WorldBridge.Despawn(obj, context);
        public static bool DespawnIfStateAuthority(NetworkObject obj) => WorldBridge.DespawnIfStateAuthority(obj);
        public static NetworkObject Find(uint rawId) => WorldBridge.Find(rawId);
        public static IEnumerator TakeAuthority(NetworkObject obj, Action<bool> granted) => WorldBridge.TakeAuthority(obj, granted);
    }
}
