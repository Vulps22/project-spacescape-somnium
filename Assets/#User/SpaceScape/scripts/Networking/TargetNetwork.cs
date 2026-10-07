using System;
using SomniumSpace.Network.Bridge;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// The test targets' knocked-out bits, the same for every player: one bit per target, 32 to an int. Where
    /// they are needs nothing sent (TargetField places them from a seed). The master (the data's state
    /// authority) knocks targets out, from its own shots or another client's, brings them back, and writes the
    /// bits; everyone else follows them. Late joiners read which are down.
    [RequireComponent(typeof(TargetField))]
    public sealed class TargetNetwork : MonoBehaviour
    {
        /// Wire ids. Append only; never insert or renumber.
        private enum TargetMessageType : byte
        {
            Hit = 0,   // the target's index
        }

        [Tooltip("Where the bits are replicated: one bit per target, 32 to an int.")]
        [SerializeField] private NetworkBridgeData _data;

        private TargetField _field;

        private bool Live => _data != null && _data.Object != null && _data.Object.IsValid;

        private void Awake()
        {
            _field = GetComponent<TargetField>();
            _field.HitRequested += Ask;
            if (_data != null) _data.OnMessageToController += OnMessageToController;
        }

        private void Start()
        {
            if (_data != null && _data.IntCount * 32 < _field.Count)
                Debug.LogError($"TargetNetwork: {_field.Count} targets need {(_field.Count + 31) / 32} ints but the NetworkBridgeData holds {_data.IntCount}; the rest will not be shared", this);
        }

        private void OnDestroy()
        {
            if (_field != null) _field.HitRequested -= Ask;
            if (_data != null) _data.OnMessageToController -= OnMessageToController;
        }

        private void Update()
        {
            if (!WorldManager.IsNetworkReady || !Live)
            {
                _field.Decides = true;
                return;
            }

            bool decides = _data.Object.HasStateAuthority;
            _field.Decides = decides;
            var ints = _data.IntArray;
            int shared = Mathf.Min(_field.Count, _data.IntCount * 32);
            for (int i = 0; i < shared; i++)
            {
                int word = i / 32, bit = 1 << (i % 32);
                if (decides)
                {
                    int value = _field.IsDown(i) ? ints[word] | bit : ints[word] & ~bit;
                    if (ints[word] != value) ints.Set(word, value);
                }
                else _field.SetDown(i, (ints[word] & bit) != 0);
            }
        }

        private void Ask(int index)
        {
            if (Live) _data.RPC_SendMessageToController(MessageFrame.Pack((byte)TargetMessageType.Hit, BitConverter.GetBytes(index)));
        }

        private void OnMessageToController(byte[] framed)
        {
            if (!Live || !_data.Object.HasStateAuthority) return;
            if (!MessageFrame.Unpack(framed, out byte id, out byte[] payload)) return;
            if ((TargetMessageType)id != TargetMessageType.Hit || payload == null || payload.Length < 4) return;
            int index = BitConverter.ToInt32(payload, 0);
            if (index >= 0 && index < _field.Count) _field.SetDown(index, true);
        }

        private void OnValidate()
        {
            if (_data == null) _data = GetComponent<NetworkBridgeData>();
        }
    }
}
