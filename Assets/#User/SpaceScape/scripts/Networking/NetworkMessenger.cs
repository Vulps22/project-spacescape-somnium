using System;
using SomniumSpace.Network.Bridge;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// Sends and receives our messages on one networked object, over Somnium's NetworkBridgeEvents,
    /// framed by MessageFrame. Each user keeps its own append-only `enum ...MessageType : byte`.
    public class NetworkMessenger : NetworkBridgeEvents
    {
        /// A message reached every client, including whoever sent it.
        public event Action<byte, byte[]> MessageToAll;

        /// A message reached every client except the state authority.
        public event Action<byte, byte[]> MessageToProxies;

        /// A message reached the state authority.
        public event Action<byte, byte[]> MessageToController;

        /// True while there is a live NetworkObject to send on.
        public bool CanSend => Object != null && Object.IsValid;

        public void SendToAll(byte id, byte[] data) => RPC_SendMessageToAll(MessageFrame.Pack(id, data));
        public void SendToProxies(byte id, byte[] data) => RPC_SendMessageToProxies(MessageFrame.Pack(id, data));
        public void SendToController(byte id, byte[] data) => RPC_SendMessageToController(MessageFrame.Pack(id, data));

        private void Awake()
        {
            OnMessageToAll += RaiseToAll;
            OnMessageToProxies += RaiseToProxies;
            OnMessageToController += RaiseToController;
        }

        private void OnDestroy()
        {
            OnMessageToAll -= RaiseToAll;
            OnMessageToProxies -= RaiseToProxies;
            OnMessageToController -= RaiseToController;
        }

        private void RaiseToAll(byte[] f) { if (MessageFrame.Unpack(f, out byte id, out byte[] d)) MessageToAll?.Invoke(id, d); }
        private void RaiseToProxies(byte[] f) { if (MessageFrame.Unpack(f, out byte id, out byte[] d)) MessageToProxies?.Invoke(id, d); }
        private void RaiseToController(byte[] f) { if (MessageFrame.Unpack(f, out byte id, out byte[] d)) MessageToController?.Invoke(id, d); }
    }
}
