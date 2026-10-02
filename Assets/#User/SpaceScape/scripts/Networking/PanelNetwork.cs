using System;
using System.Collections.Generic;
using SomniumSpace.Network.Bridge;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// Every access panel's open or shut, the same for every player: one bit per panel in one
    /// NetworkBridgeData, so a ship of walls is one network object, not one each. Every client lists the
    /// panels in the same order, by position, so a panel's place in that list is its bit.
    ///
    /// The master (the data's state authority) opens a panel when a hand there passes through its socket or
    /// another client asks, and shuts it once nobody is in front of it; it writes the bits, and everyone else
    /// follows them. A late joiner simply reads which are open.
    [RequireComponent(typeof(NetworkBridgeData))]
    public sealed class PanelNetwork : MonoBehaviour
    {
        /// Wire ids. Append only; never insert or renumber.
        private enum PanelMessageType : byte
        {
            Open = 0,   // the panel's index
        }

        [Tooltip("Where the panels are replicated: one bit per panel, 32 to an int.")]
        [SerializeField] private NetworkBridgeData _data;

        private readonly List<AccessPanel> _panels = new List<AccessPanel>();
        private readonly Dictionary<AccessPanel, Action> _asks = new Dictionary<AccessPanel, Action>();

        private bool Live => _data != null && _data.Object != null && _data.Object.IsValid;

        private void Awake()
        {
            if (_data != null) _data.OnMessageToController += OnMessageToController;
        }

        private void Start()
        {
            _panels.AddRange(AccessPanel.All);
            _panels.Sort((a, b) => Compare(a.transform.position, b.transform.position));
            if (_data != null && _data.IntCount * 32 < _panels.Count)
                Debug.LogError($"PanelNetwork: {_panels.Count} panels need {(_panels.Count + 31) / 32} ints but the NetworkBridgeData holds {_data.IntCount}; the rest will not be shared", this);

            for (int i = 0; i < _panels.Count; i++)
            {
                int index = i;
                Action ask = () => Ask(index);
                _asks[_panels[i]] = ask;
                _panels[i].OpenRequested += ask;
            }
        }

        private void OnDestroy()
        {
            if (_data != null) _data.OnMessageToController -= OnMessageToController;
            foreach (var pair in _asks)
                if (pair.Key != null) pair.Key.OpenRequested -= pair.Value;
        }

        private void Update()
        {
            if (!WorldManager.IsNetworkReady || !Live)
            {
                foreach (var panel in _panels) panel.Decides = true;
                return;
            }

            bool decides = _data.Object.HasStateAuthority;
            var ints = _data.IntArray;
            int shared = Mathf.Min(_panels.Count, _data.IntCount * 32);
            for (int i = 0; i < shared; i++)
            {
                var panel = _panels[i];
                panel.Decides = decides;
                int word = i / 32, bit = 1 << (i % 32);
                if (decides)
                {
                    int value = panel.Open ? ints[word] | bit : ints[word] & ~bit;
                    if (ints[word] != value) ints.Set(word, value);
                }
                else
                {
                    bool open = (ints[word] & bit) != 0;
                    if (open != panel.Open) panel.SetOpen(open);
                }
            }
        }

        private void Ask(int index)
        {
            if (Live) _data.RPC_SendMessageToController(MessageFrame.Pack((byte)PanelMessageType.Open, BitConverter.GetBytes(index)));
        }

        private void OnMessageToController(byte[] framed)
        {
            if (!Live || !_data.Object.HasStateAuthority) return;
            if (!MessageFrame.Unpack(framed, out byte id, out byte[] payload)) return;
            switch ((PanelMessageType)id)
            {
                case PanelMessageType.Open:
                    int index = payload != null && payload.Length >= 4 ? BitConverter.ToInt32(payload, 0) : -1;
                    if (index >= 0 && index < _panels.Count) _panels[index].SetOpen(true);
                    break;
                default:
                    Debug.LogWarning($"PanelNetwork: got unknown message {id}", this);
                    break;
            }
        }

        private static int Compare(Vector3 a, Vector3 b)
        {
            var x = GridCell.ToCell(a);
            var y = GridCell.ToCell(b);
            if (x.x != y.x) return x.x.CompareTo(y.x);
            if (x.y != y.y) return x.y.CompareTo(y.y);
            return x.z.CompareTo(y.z);
        }

        private void OnValidate()
        {
            if (_data == null) _data = GetComponent<NetworkBridgeData>();
        }
    }
}
