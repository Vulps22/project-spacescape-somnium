using System;
using Fusion;
using SomniumSpace.Network.Bridge;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// A slot's lock and occupant, the same for every player. The master (the slot's state authority)
    /// decides both: it spawns the slot's starting component through Fusion, installs it, and writes its
    /// network id; everyone else finds that object and installs it in the same slot. A pull on another
    /// client is sent to the master, which unlocks the slot for everyone. So is a hand taking the component
    /// out or putting one in: that client does it at once, and keeps its own occupant for a moment while the
    /// master catches up, so the correction does not undo it.
    ///
    /// With no network at all (only ever the Editor without Photon) the slot makes its component locally.
    [RequireComponent(typeof(ComponentSlot))]
    public sealed class SlotNetwork : MonoBehaviour
    {
        /// Wire ids. Append only; never insert or renumber.
        private enum SlotMessageType : byte
        {
            Pull = 0,
            TakeOut = 1,   // the component's network id
            PutIn = 2,     // the component's network id
        }

        private const int LockInt = 0;
        private const int OccupantInt = 1;
        private const int Emptied = -1;   // the occupant after a hand took the component out: never spawn again
        private const float SpawnRetrySeconds = 5f;
        private const float OfflineAfterSeconds = 1f;
        private const float KeepLocalSeconds = 2f;

        [Tooltip("Where the lock and occupant are replicated: two ints (0 locked / 1 unlocked, and the component's network id, or -1 once a hand has emptied it).")]
        [SerializeField] private NetworkBridgeData _data;

        private ComponentSlot _slot;
        private float _spawnAskedAt = float.MinValue;
        private bool _local;
        private float _keepLocalUntil = float.MinValue;

        private bool Live => _data != null && _data.Object != null && _data.Object.IsValid;

        private void Awake()
        {
            _slot = GetComponent<ComponentSlot>();
            _slot.PullRequested += OnPullRequested;
            _slot.TakenOut += OnTakenOut;
            _slot.PutIn += OnPutIn;
            if (_data != null) _data.OnMessageToController += OnMessageToController;
            if (_data != null && _data.IntCount < 2)
                Debug.LogError($"SlotNetwork: '{name}' needs two ints but its NetworkBridgeData holds {_data.IntCount}", this);
        }

        private void OnDestroy()
        {
            if (_slot != null)
            {
                _slot.PullRequested -= OnPullRequested;
                _slot.TakenOut -= OnTakenOut;
                _slot.PutIn -= OnPutIn;
            }
            if (_data != null) _data.OnMessageToController -= OnMessageToController;
        }

        private void Update()
        {
            if (!WorldManager.IsNetworkReady || !Live)
            {
                _slot.Decides = true;
                // No network at all, which only happens in the Editor: make the component here, once. In a
                // world the runner can take a few seconds to appear, and making it locally then would leave
                // everyone with their own unnetworked copy.
                if (!_local && Application.isEditor && !WorldManager.HasRunner && Time.timeSinceLevelLoad > OfflineAfterSeconds)
                {
                    _local = true;
                    _slot.StartLocally();
                }
                return;
            }

            _slot.Decides = _data.Object.HasStateAuthority;
            if (_slot.Decides) Decide();
            else Follow();
        }

        private void Decide()
        {
            var ints = _data.IntArray;
            SetIfChanged(ints, LockInt, _slot.Unlocked ? 1 : 0);

            // The starting component, spawned once by whoever is master when the ship first has none.
            if (_slot.Component == null && ints[OccupantInt] == 0 && _slot.StartsWith != null && WorldManager.CanSpawn
                && Time.time - _spawnAskedAt > SpawnRetrySeconds)
            {
                _spawnAskedAt = Time.time;
                SpawnStartingComponent();
            }
            else if (_slot.Component == null && ints[OccupantInt] > 0)
            {
                // Became master of a slot whose component was already spawned: take it up.
                InstallById(ints[OccupantInt]);
            }

            SetIfChanged(ints, OccupantInt, OccupantValue(ints[OccupantInt]));
        }

        /// The occupant to write: the component's id, 0 while the starting component has yet to be made, or
        /// Emptied once the slot has held one and been emptied.
        private int OccupantValue(int written) =>
            _slot.Component != null ? IdOf(_slot.Component) : written == 0 ? 0 : Emptied;

        private void Follow()
        {
            var ints = _data.IntArray;
            if ((ints[LockInt] != 0) != _slot.Unlocked) _slot.SetUnlocked(ints[LockInt] != 0);

            int occupant = ints[OccupantInt];
            if (occupant == IdOf(_slot.Component) || Time.time < _keepLocalUntil) return;
            if (occupant <= 0) { _slot.Remove(); return; }
            InstallById(occupant);
        }

        private void SpawnStartingComponent()
        {
            var prefab = _slot.StartsWith;
            if (!_slot.Fits(prefab))
            {
                Debug.LogWarning($"SlotNetwork: '{name}' starts with '{prefab.name}', which is not a {_slot.Size} component", this);
                return;
            }
            if (!prefab.TryGetComponent<NetworkObject>(out var networked))
            {
                Debug.LogError($"SlotNetwork: '{prefab.name}' has no NetworkObject, so it cannot be spawned for '{name}'", this);
                return;
            }

            var pose = _slot.SeatPose(prefab);
            var spawned = WorldManager.Spawn(networked, pose.position, pose.rotation, $"starting component of '{name}'");
            if (spawned != null && spawned.TryGetComponent<GridNode>(out var component)) _slot.Install(component);
        }

        /// Installs the component with this network id, once it has arrived here.
        private void InstallById(int raw)
        {
            var found = WorldManager.Find((uint)raw);
            if (found == null || !found.TryGetComponent<GridNode>(out var component)) return;
            if (_slot.Component != null && _slot.Component != component) _slot.Remove();
            _slot.Install(component);
        }

        private static int IdOf(GridNode component) =>
            component != null && component.TryGetComponent<NetworkObject>(out var networked) && networked.IsValid
                ? (int)networked.Id.Raw : 0;

        private static void SetIfChanged(NetworkArray<int> ints, int i, int value)
        {
            if (ints[i] != value) ints.Set(i, value);
        }

        private void OnPullRequested()
        {
            if (Live) _data.RPC_SendMessageToController(MessageFrame.Pack((byte)SlotMessageType.Pull, null));
        }

        /// A hand here took the component out: the master records it now; anyone else tells the master.
        private void OnTakenOut(GridNode component) => Tell(SlotMessageType.TakeOut, component);

        /// A hand here put a component in: the master records it now; anyone else tells the master.
        private void OnPutIn(GridNode component) => Tell(SlotMessageType.PutIn, component);

        private void Tell(SlotMessageType type, GridNode component)
        {
            if (!Live) return;
            if (_slot.Decides) { WriteOccupant(); return; }
            _keepLocalUntil = Time.time + KeepLocalSeconds;
            _data.RPC_SendMessageToController(MessageFrame.Pack((byte)type, BitConverter.GetBytes(IdOf(component))));
        }

        private void WriteOccupant()
        {
            var ints = _data.IntArray;
            SetIfChanged(ints, OccupantInt, OccupantValue(ints[OccupantInt]));
            SetIfChanged(ints, LockInt, _slot.Unlocked ? 1 : 0);
        }

        private void OnMessageToController(byte[] framed)
        {
            if (!Live || !_data.Object.HasStateAuthority) return;
            if (!MessageFrame.Unpack(framed, out byte id, out byte[] payload)) return;
            int component = payload != null && payload.Length >= 4 ? BitConverter.ToInt32(payload, 0) : 0;
            switch ((SlotMessageType)id)
            {
                case SlotMessageType.Pull:
                    _slot.Unlock();
                    break;
                case SlotMessageType.TakeOut:
                    if (component != 0 && IdOf(_slot.Component) == component) _slot.TakenOutElsewhere(_slot.Component);
                    WriteOccupant();
                    break;
                case SlotMessageType.PutIn:
                    var found = WorldManager.Find((uint)component);
                    if (_slot.Component == null && found != null && found.TryGetComponent<GridNode>(out var put)
                        && _slot.Install(put))
                        _slot.SetUnlocked(false);
                    WriteOccupant();
                    break;
                default:
                    Debug.LogWarning($"SlotNetwork: '{name}' got unknown message {id}", this);
                    break;
            }
        }

        private void OnValidate()
        {
            if (_data == null) _data = GetComponent<NetworkBridgeData>();
        }
    }
}
