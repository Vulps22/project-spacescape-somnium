using System;
using Fusion;
using SomniumSpace.Bridge.Components;
using SomniumSpace.Bridge.Player;
using SomniumSpace.Worlds.SpaceScape.Community;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Player
{
    /// Somnium's player list and SceneNetworking's master flag, in our own types. Only managers call
    /// this (docs/networking.md, Layers). PlayerHands is the other script that reads Somnium's
    /// players, for the hands alone.
    ///
    /// A MonoBehaviour because Somnium publishes its player list as UnityEvents and something has to
    /// be alive to listen. The accessors and events are static, so a subscriber can attach in its own
    /// Awake whichever wakes first.
    [RequireComponent(typeof(SomniumPlayersContainer))]
    public sealed class PlayerBridge : MonoBehaviour
    {
        [Tooltip("Somnium's list of players, filled at runtime. Found automatically on this object.")]
        [SerializeField] private SomniumPlayersContainer _players;

        /// Somnium admitted a player. Every client.
        public static event Action<PlayerIdentity> PlayerAdded;

        /// Somnium dropped a player. Every client.
        public static event Action<PlayerIdentity> PlayerRemoved;

        /// Somnium admitted the local player.
        public static event Action<PlayerIdentity> LocalPlayerAdded;

        /// A remote peer entered the Fusion room and can now receive messages.
        public static event Action OtherPlayerJoined;

        /// The local player is now the master.
        public static event Action BecameWorldMaster
        {
            add { SceneNetworking.OnBecomeWorldMaster += value; }
            remove { SceneNetworking.OnBecomeWorldMaster -= value; }
        }

        /// False if there is no player list behind this at all.
        public static bool IsPresent => _container != null;

        /// Whether the local player decides things for the world. Scene-wide, not Fusion's
        /// per-object state authority.
        public static bool IsMaster => SceneNetworking.IsMasterClient;

        /// The local player, or None until they have spawned.
        public static PlayerIdentity LocalPlayer =>
            _container == null ? PlayerIdentity.None : Identify(_container.LocalPlayer);

        /// The player with this id, or None if this client has not been told about them.
        public static PlayerIdentity GetPlayer(string id)
        {
            if (_container == null || string.IsNullOrEmpty(id)) return PlayerIdentity.None;
            return Identify(_container.GetPlayerByID(id));
        }

        private static SomniumPlayersContainer _container;

        private void Awake()
        {
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;

            if (_players == null)
            {
                Debug.LogError("PlayerBridge: no SomniumPlayersContainer; no player will ever be seen to arrive or leave", this);
                return;
            }

            _container = _players;
            _players.PlayerAdded.AddListener(OnSdkPlayerAdded);
            _players.PlayerRemoved.AddListener(OnSdkPlayerRemoved);
            _players.LocalPlayerAdded.AddListener(OnSdkLocalPlayerAdded);
        }

        private void OnDestroy()
        {
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
            if (_container == _players) _container = null;

            if (_players == null) return;
            _players.PlayerAdded.RemoveListener(OnSdkPlayerAdded);
            _players.PlayerRemoved.RemoveListener(OnSdkPlayerRemoved);
            _players.LocalPlayerAdded.RemoveListener(OnSdkLocalPlayerAdded);
        }

        private static PlayerIdentity Identify(ISomniumPlayer player)
        {
            string id = player?.Properties?.Id;
            return string.IsNullOrEmpty(id) ? PlayerIdentity.None : new PlayerIdentity(id, player.Properties.NickName);
        }

        private void OnSdkPlayerAdded(ISomniumPlayer player)
        {
            var who = Identify(player);
            if (who.Exists) PlayerAdded?.Invoke(who);
        }

        private void OnSdkPlayerRemoved(ISomniumPlayer player)
        {
            var who = Identify(player);
            if (who.Exists) PlayerRemoved?.Invoke(who);
        }

        private void OnSdkLocalPlayerAdded(ISomniumPlayer player)
        {
            var who = Identify(player);
            if (who.Exists) LocalPlayerAdded?.Invoke(who);
        }

        // The PlayerRef stays here: a bridge does not hand out the types it fronts.
        private void OnOtherPlayerJoined(PlayerRef _) => OtherPlayerJoined?.Invoke();

        private void OnValidate()
        {
            if (_players == null) _players = GetComponent<SomniumPlayersContainer>();
        }
    }
}
