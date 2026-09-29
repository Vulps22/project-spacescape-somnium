using System;
using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// Sends and receives our messages on one networked object, framed by MessageFrame. Each user keeps its
    /// own append-only `enum ...MessageType : byte`.
    ///
    /// Holds Somnium's NetworkBridgeEvents beside it rather than inheriting from it: a Fusion network type in
    /// our assembly would need Fusion's weaver, which Somnium does not run on world code, and one unwoven
    /// type stops every scene network object from registering.
    public sealed class NetworkMessenger : MonoBehaviour
    {
        [Tooltip("Somnium's NetworkBridgeEvents on this object, which carries the messages.")]
        [SerializeField] private NetworkBridgeEvents _bridge;

        /// A message reached every client, including whoever sent it.
        public event Action<byte, byte[]> MessageToAll;

        /// A message reached every client except the state authority.
        public event Action<byte, byte[]> MessageToProxies;

        /// A message reached the state authority.
        public event Action<byte, byte[]> MessageToController;

        /// The networked object has been spawned (for a scene object: registered).
        public event Action OnSpawned
        {
            add { if (_bridge != null) _bridge.OnSpawned += value; }
            remove { if (_bridge != null) _bridge.OnSpawned -= value; }
        }

        /// True while there is a live NetworkObject to send on.
        public bool CanSend => _bridge != null && _bridge.Object != null && _bridge.Object.IsValid;

        public void SendToAll(byte id, byte[] data) => _bridge.RPC_SendMessageToAll(MessageFrame.Pack(id, data));
        public void SendToProxies(byte id, byte[] data) => _bridge.RPC_SendMessageToProxies(MessageFrame.Pack(id, data));
        public void SendToController(byte id, byte[] data) => _bridge.RPC_SendMessageToController(MessageFrame.Pack(id, data));

        private void Awake()
        {
            if (_bridge == null) _bridge = GetComponent<NetworkBridgeEvents>();
            if (_bridge == null)
            {
                Debug.LogError($"NetworkMessenger: '{name}' has no NetworkBridgeEvents beside it; nothing will be sent or heard", this);
                return;
            }
            _bridge.OnMessageToAll += RaiseToAll;
            _bridge.OnMessageToProxies += RaiseToProxies;
            _bridge.OnMessageToController += RaiseToController;
        }

        private void OnDestroy()
        {
            if (_bridge == null) return;
            _bridge.OnMessageToAll -= RaiseToAll;
            _bridge.OnMessageToProxies -= RaiseToProxies;
            _bridge.OnMessageToController -= RaiseToController;
        }

        private void RaiseToAll(byte[] f) { if (MessageFrame.Unpack(f, out byte id, out byte[] d)) MessageToAll?.Invoke(id, d); }
        private void RaiseToProxies(byte[] f) { if (MessageFrame.Unpack(f, out byte id, out byte[] d)) MessageToProxies?.Invoke(id, d); }
        private void RaiseToController(byte[] f) { if (MessageFrame.Unpack(f, out byte id, out byte[] d)) MessageToController?.Invoke(id, d); }

        private void OnValidate()
        {
            if (_bridge == null) _bridge = GetComponent<NetworkBridgeEvents>();
        }
    }
}
