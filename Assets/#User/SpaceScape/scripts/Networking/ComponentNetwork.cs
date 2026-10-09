using System;
using Fusion;
using SomniumSpace.Network.Bridge;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// Keeps one component the same for every player. The master (the state authority over this scene
    /// object) writes its heat, whether it is severed, and every INetworkedState module's values into a NetworkBridgeData a few
    /// times a second; every other client reads them back in and simulates on from there.
    ///
    /// Values travel as a float's bits in each int. Slot 0 is a counter the master bumps with every write,
    /// so a client can tell a fresh write from the zeros that are there before the first one arrives.
    ///
    /// The reactor's dial and the disruptor's aim are what a non-master sets: a hand moves it here at once and
    /// the new value is sent to the master, which applies it and writes it back out with everything else. The
    /// disruptor's fire button on a non-master asks the master to fire; the master fires and tells everyone, so
    /// every client shows the shot. An item port on a non-master that loads an item tells the master which of the
    /// component's LoadingModules took it, and the master loads it too and writes it out. A loader on a non-master
    /// that takes in a pack, or whose eject switch a hand throws, tells the master; only the master puts a leaving
    /// pack back into the world, as a spawned pack.
    [RequireComponent(typeof(GridNode))]
    public sealed class ComponentNetwork : MonoBehaviour
    {
        /// Wire ids. Append only; never insert or renumber.
        private enum ComponentMessageType : byte
        {
            DialTurned = 0,
            AimMoved = 1,   // the disruptor's aim: two floats
            Fire = 2,       // asked of the master
            Fired = 3,      // from the master, to everyone
            RadarButton = 4, // a radar button pressed elsewhere: -1 Previous, +1 Next, 0 Select
            RadarZoom = 5,   // a radar's zoom lever moved elsewhere: one float
            ItemLoaded = 6,  // an item port elsewhere loaded an item: which of the component's LoadingModules, as an int
            PackFed = 7,     // a loader elsewhere took in a pack: its kind and its filled places, two ints
            EjectSwitch = 8, // a loader's eject switch thrown elsewhere: 1 on, 0 off, as an int
        }

        [Tooltip("Where the state is replicated. Any size with room for 3 + the modules' values; the log says if it is too small.")]
        [SerializeField] private NetworkBridgeData _data;
        [Tooltip("How many times a second the master writes the state out.")]
        [SerializeField] private float _writesPerSecond = 4f;
        [Tooltip("How many times a second a dial held on this client is sent to the master.")]
        [SerializeField] private float _dialSendsPerSecond = 10f;

        private GridNode _tile;
        private PowerGrid _grid;
        private INetworkedState[] _modules;
        private ReactorBehaviourModule _reactor;
        private float[] _values;
        private int _written;
        private int _lastRead;
        private float _nextWrite;
        private float _nextDialSend;
        private float? _pendingDial;
        private DisruptorBehaviourModule _disruptor;
        private Vector2? _pendingAim;
        private RadarBehaviourModule _radar;
        private float? _pendingZoom;
        private LoadingModule[] _loading;
        private Action[] _loadedHandlers;
        private LoaderBehaviourModule _loader;
        private bool _usable;

        private void Awake()
        {
            _tile = GetComponent<GridNode>();
            _modules = GetComponents<INetworkedState>();

            _grid = FindFirstObjectByType<PowerGrid>();

            int count = 2; // heat, severed
            foreach (var module in _modules) count += module.StateCount;
            _values = new float[count];

            if (_data == null)
            {
                Debug.LogError($"ComponentNetwork: '{name}' has no NetworkBridgeData; it will not be shared", this);
                return;
            }
            if (_data.IntCount < count + 1)
            {
                Debug.LogError($"ComponentNetwork: '{name}' needs {count + 1} ints but its NetworkBridgeData holds {_data.IntCount}; it will not be shared", this);
                return;
            }

            _usable = true;
            _data.OnMessageToController += OnMessageToController;
            _data.OnMessageToAll += OnMessageToAll;
            if (TryGetComponent(out _reactor)) _reactor.DialTurned += OnDialTurned;
            if (TryGetComponent(out _disruptor))
            {
                _disruptor.AimMoved += OnAimMoved;
                _disruptor.FireRequested += OnFireRequested;
                _disruptor.Fired += OnFired;
            }
            if (TryGetComponent(out _radar))
            {
                _radar.ButtonRequested += OnRadarButton;
                _radar.ZoomMoved += OnZoomMoved;
            }
            if (TryGetComponent(out _loader))
            {
                _loader.FedByHand += OnPackFed;
                _loader.EjectPressed += OnEjectPressed;
                _loader.PutOut = PutOutPack;
            }
            // Every client has the same LoadingModules in the same order, so one is named by its place.
            _loading = GetComponents<LoadingModule>();
            _loadedHandlers = new Action[_loading.Length];
            for (int i = 0; i < _loading.Length; i++)
            {
                int index = i;
                _loadedHandlers[i] = () => OnItemLoaded(index);
                _loading[i].LoadedByHand += _loadedHandlers[i];
            }
        }

        private void OnDestroy()
        {
            if (_data != null) _data.OnMessageToController -= OnMessageToController;
            if (_data != null) _data.OnMessageToAll -= OnMessageToAll;
            if (_reactor != null) _reactor.DialTurned -= OnDialTurned;
            if (_disruptor != null)
            {
                _disruptor.AimMoved -= OnAimMoved;
                _disruptor.FireRequested -= OnFireRequested;
                _disruptor.Fired -= OnFired;
            }
            if (_radar != null)
            {
                _radar.ButtonRequested -= OnRadarButton;
                _radar.ZoomMoved -= OnZoomMoved;
            }
            if (_loader != null)
            {
                _loader.FedByHand -= OnPackFed;
                _loader.EjectPressed -= OnEjectPressed;
                _loader.PutOut = null;
            }
            if (_loading != null) for (int i = 0; i < _loading.Length; i++) _loading[i].LoadedByHand -= _loadedHandlers[i];
        }

        private bool IsLive => _usable && _data.Object != null && _data.Object.IsValid && _tile.Node != null && _grid != null && _grid.Graph != null;

        private void Update()
        {
            // A gun nobody can be told about (no network, or not in a grid) decides for itself.
            if (_disruptor != null) _disruptor.Decides = !IsLive || _data.Object.HasStateAuthority;
            if (_radar != null) _radar.Decides = !IsLive || _data.Object.HasStateAuthority;
            if (_loader != null) _loader.Decides = !IsLive || _data.Object.HasStateAuthority;
            if (!IsLive) return;

            if (_data.Object.HasStateAuthority)
            {
                if (Time.time >= _nextWrite)
                {
                    _nextWrite = Time.time + 1f / Mathf.Max(0.1f, _writesPerSecond);
                    Write();
                }
                return;
            }

            Read();
            SendPendingDial();
            SendPendingAim();
            SendPendingZoom();
        }

        private void Write()
        {
            _values[0] = (float)_tile.Node.Celsius;
            _values[1] = _tile.Node.IsPopped ? 1f : 0f;
            int at = 2;
            foreach (var module in _modules)
            {
                module.WriteState(_values, at);
                at += module.StateCount;
            }

            var ints = _data.IntArray;
            for (int i = 0; i < _values.Length; i++) ints.Set(i + 1, BitConverter.SingleToInt32Bits(_values[i]));

            _written = _written == int.MaxValue ? 1 : _written + 1;
            ints.Set(0, _written);
        }

        private void Read()
        {
            var ints = _data.IntArray;
            int stamp = ints[0];
            if (stamp == 0 || stamp == _lastRead) return;
            _lastRead = stamp;

            for (int i = 0; i < _values.Length; i++) _values[i] = BitConverter.Int32BitsToSingle(ints[i + 1]);

            _tile.Node.Celsius = _values[0];
            _grid.Graph.CorrectPopped(_tile.Node, _values[1] > 0.5f);
            int at = 2;
            foreach (var module in _modules)
            {
                module.ReadState(_values, at);
                at += module.StateCount;
            }
        }

        private void OnDialTurned(float withdrawal)
        {
            if (IsLive && !_data.Object.HasStateAuthority) _pendingDial = withdrawal;
        }

        // At most a few times a second while a hand is moving it, and the last value always goes.
        private void SendPendingDial()
        {
            if (_pendingDial == null || Time.time < _nextDialSend) return;
            _nextDialSend = Time.time + 1f / Mathf.Max(0.1f, _dialSendsPerSecond);

            byte[] payload = BitConverter.GetBytes(_pendingDial.Value);
            _pendingDial = null;
            _data.RPC_SendMessageToController(MessageFrame.Pack((byte)ComponentMessageType.DialTurned, payload));
        }

        private void OnAimMoved(Vector2 aim)
        {
            if (IsLive && !_data.Object.HasStateAuthority) _pendingAim = aim;
        }

        // Like the dial: a few times a second while a hand moves it, and the last value always goes.
        private void SendPendingAim()
        {
            if (_pendingAim == null || Time.time < _nextDialSend) return;
            _nextDialSend = Time.time + 1f / Mathf.Max(0.1f, _dialSendsPerSecond);

            var aim = _pendingAim.Value;
            _pendingAim = null;
            byte[] payload = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(aim.x), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(aim.y), 0, payload, 4, 4);
            _data.RPC_SendMessageToController(MessageFrame.Pack((byte)ComponentMessageType.AimMoved, payload));
        }

        private void OnFireRequested()
        {
            if (IsLive) _data.RPC_SendMessageToController(MessageFrame.Pack((byte)ComponentMessageType.Fire, null));
        }

        /// The master fired: everyone, the master included, shows the shot. With no network, only here.
        private void OnFired()
        {
            if (IsLive) _data.RPC_SendMessageToAll(MessageFrame.Pack((byte)ComponentMessageType.Fired, null));
            else _disruptor.ShowShot();
        }

        private void OnZoomMoved(float zoom)
        {
            if (IsLive && !_data.Object.HasStateAuthority) _pendingZoom = zoom;
        }

        // Like the dial: a few times a second while a hand moves it, and the last value always goes.
        private void SendPendingZoom()
        {
            if (_pendingZoom == null || Time.time < _nextDialSend) return;
            _nextDialSend = Time.time + 1f / Mathf.Max(0.1f, _dialSendsPerSecond);
            byte[] payload = BitConverter.GetBytes(_pendingZoom.Value);
            _pendingZoom = null;
            _data.RPC_SendMessageToController(MessageFrame.Pack((byte)ComponentMessageType.RadarZoom, payload));
        }

        private void OnRadarButton(int button)
        {
            if (IsLive) _data.RPC_SendMessageToController(MessageFrame.Pack((byte)ComponentMessageType.RadarButton, BitConverter.GetBytes(button)));
        }

        private void OnItemLoaded(int index)
        {
            if (IsLive && !_data.Object.HasStateAuthority)
                _data.RPC_SendMessageToController(MessageFrame.Pack((byte)ComponentMessageType.ItemLoaded, BitConverter.GetBytes(index)));
        }

        private void OnPackFed(int kind, int filled)
        {
            if (!IsLive || _data.Object.HasStateAuthority) return;
            byte[] payload = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(kind), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(filled), 0, payload, 4, 4);
            _data.RPC_SendMessageToController(MessageFrame.Pack((byte)ComponentMessageType.PackFed, payload));
        }

        private void OnEjectPressed(bool on)
        {
            if (IsLive && !_data.Object.HasStateAuthority)
                _data.RPC_SendMessageToController(MessageFrame.Pack((byte)ComponentMessageType.EjectSwitch, BitConverter.GetBytes(on ? 1 : 0)));
        }

        /// Spawns a pack leaving the loader, on the master. Made here with no network.
        private ItemPack PutOutPack(ItemPack prefab, Vector3 at, Quaternion turn)
        {
            if (!IsLive) return Instantiate(prefab, at, turn);
            if (!WorldManager.CanSpawn || !prefab.TryGetComponent<NetworkObject>(out var networked)) return null;
            var spawned = WorldManager.Spawn(networked, at, turn, $"pack '{prefab.name}' put out by loader '{name}'");
            return spawned != null ? spawned.GetComponent<ItemPack>() : null;
        }

        private void OnMessageToAll(byte[] framed)
        {
            if (!MessageFrame.Unpack(framed, out byte id, out _)) return;
            if ((ComponentMessageType)id == ComponentMessageType.Fired && _disruptor != null) _disruptor.ShowShot();
        }

        private void OnMessageToController(byte[] framed)
        {
            if (!IsLive || !_data.Object.HasStateAuthority) return;
            if (!MessageFrame.Unpack(framed, out byte id, out byte[] data)) return;

            switch ((ComponentMessageType)id)
            {
                case ComponentMessageType.DialTurned:
                    if (_reactor != null && data.Length >= 4) _reactor.TargetWithdrawal = BitConverter.ToSingle(data, 0);
                    break;
                case ComponentMessageType.AimMoved:
                    if (_disruptor != null && data.Length >= 8)
                        _disruptor.SetAim(new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4)));
                    break;
                case ComponentMessageType.Fire:
                    if (_disruptor != null) _disruptor.Fire();
                    break;
                case ComponentMessageType.RadarZoom:
                    if (_radar != null && data.Length >= 4) _radar.SetZoom(BitConverter.ToSingle(data, 0));
                    break;
                case ComponentMessageType.RadarButton:
                    if (_radar != null && data.Length >= 4) _radar.Apply(BitConverter.ToInt32(data, 0));
                    break;
                case ComponentMessageType.ItemLoaded:
                    if (data.Length >= 4)
                    {
                        int index = BitConverter.ToInt32(data, 0);
                        if (index >= 0 && index < _loading.Length) _loading[index].Load();
                    }
                    break;
                case ComponentMessageType.PackFed:
                    if (_loader != null && data.Length >= 8) _loader.Add(BitConverter.ToInt32(data, 0), BitConverter.ToInt32(data, 4));
                    break;
                case ComponentMessageType.EjectSwitch:
                    if (_loader != null && data.Length >= 4) _loader.Ejecting = BitConverter.ToInt32(data, 0) != 0;
                    break;
                default:
                    Debug.LogWarning($"ComponentNetwork: '{name}' got unknown message {id}", this);
                    break;
            }
        }

        private void OnValidate()
        {
            if (_data == null) _data = GetComponent<NetworkBridgeData>();
        }
    }
}
