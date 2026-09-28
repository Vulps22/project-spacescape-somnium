using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Player
{
    /// What the rest of the game asks about players. Forwards to PlayerBridge, so that when a guard or
    /// a correction is needed it lands here once rather than in every caller.
    ///
    /// Logs every arrival, departure and change of master, under a banner that marks where this run
    /// starts in the client log (it is append-only across runs).
    public sealed class PlayerManager : MonoBehaviour
    {
        /// A player is here. Every client.
        public static event Action<PlayerIdentity> PlayerJoined;

        /// A player has gone. Every client.
        public static event Action<PlayerIdentity> PlayerLeft;

        /// The local player is here.
        public static event Action<PlayerIdentity> LocalPlayerJoined;

        /// A remote peer can now receive messages.
        public static event Action OtherPlayerJoined
        {
            add { PlayerBridge.OtherPlayerJoined += value; }
            remove { PlayerBridge.OtherPlayerJoined -= value; }
        }

        /// The local player is now the master.
        public static event Action BecameWorldMaster
        {
            add { PlayerBridge.BecameWorldMaster += value; }
            remove { PlayerBridge.BecameWorldMaster -= value; }
        }

        public static bool IsMaster => PlayerBridge.IsMaster;
        public static PlayerIdentity LocalPlayer => PlayerBridge.LocalPlayer;
        public static PlayerIdentity GetPlayer(string id) => PlayerBridge.GetPlayer(id);

        private static bool _sessionAnnounced;

        private void Awake()
        {
            if (!_sessionAnnounced)
            {
                _sessionAnnounced = true;
                Debug.Log($"[SpaceScape] Logging Session Started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }

            PlayerBridge.PlayerAdded += OnPlayerAdded;
            PlayerBridge.PlayerRemoved += OnPlayerRemoved;
            PlayerBridge.LocalPlayerAdded += OnLocalPlayerAdded;
            PlayerBridge.BecameWorldMaster += OnBecameWorldMaster;
        }

        private void Start()
        {
            if (!PlayerBridge.IsPresent)
                Debug.LogError("PlayerManager: no PlayerBridge in the scene; players will never be seen", this);
        }

        private void OnDestroy()
        {
            PlayerBridge.PlayerAdded -= OnPlayerAdded;
            PlayerBridge.PlayerRemoved -= OnPlayerRemoved;
            PlayerBridge.LocalPlayerAdded -= OnLocalPlayerAdded;
            PlayerBridge.BecameWorldMaster -= OnBecameWorldMaster;
        }

        private void OnPlayerAdded(PlayerIdentity who)
        {
            Debug.Log($"PlayerManager: {who} joined");
            PlayerJoined?.Invoke(who);
        }

        private void OnPlayerRemoved(PlayerIdentity who)
        {
            Debug.Log($"PlayerManager: {who} left");
            PlayerLeft?.Invoke(who);
        }

        private void OnLocalPlayerAdded(PlayerIdentity who)
        {
            Debug.Log($"PlayerManager: local player is {who}, master={IsMaster}");
            LocalPlayerJoined?.Invoke(who);
        }

        private void OnBecameWorldMaster() => Debug.Log($"PlayerManager: {LocalPlayer} is now the master");
    }
}
