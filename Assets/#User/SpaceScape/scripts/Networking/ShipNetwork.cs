using System;
using System.Text;
using SomniumSpace.Worlds.SpaceScape.Player;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// The ship's messages, on the ship's one messenger.
    ///
    /// Hello: each client announces itself when it is ready and again whenever someone joins, and everyone
    /// logs what arrives, which shows the message path works both ways.
    ///
    /// Holograms: opening and closing a conduit's hologram is told to everyone, so other players see the
    /// conduit boxed and cannot open it. Nothing is kept for late joiners: a hologram is only ever usable
    /// by the one who opened it. When two open the same conduit at once, the lower player id keeps it.
    ///
    /// Conduit edits: a hand on a non-master client that changes a conduit sends the conduit as it now
    /// stands to the master, which makes its own copy match; the conduit chunks carry it to everyone.
    ///
    /// Item conduit edits: as conduit edits, a hand on a non-master sends what it did to a run to the master,
    /// which does the same and writes the runs out (ItemConduitNetwork). Opening and closing an item conduit's
    /// hologram is told to everyone by cell, as a conduit's is by slot, with the same rule when two open at once.
    ///
    /// Addon items: spawned when taken out of a conduit, despawned when put in (AddonItemNetwork).
    ///
    /// Components shoving each other: a loose component's physics runs on whoever has its authority, so when
    /// this client's hand swings a component into a loose one, this client asks for the loose one's
    /// authority (as NetworkGrabbable would on a grab) and the shove plays out here, where the holder is.
    [RequireComponent(typeof(NetworkMessenger))]
    public sealed class ShipNetwork : MonoBehaviour
    {
        /// Wire ids. Append only; never insert or renumber.
        private enum ShipMessageType : byte
        {
            Hello = 0,
            HologramOpened = 1,
            HologramClosed = 2,
            ConduitEdited = 3,
            ItemRunEdited = 4,   // an item conduit grown, turned or cut elsewhere: edit, x, y, z, entry, exit as ints
            ItemHologramOpened = 5,  // a player opened an item conduit's hologram: x, y, z as ints, then their id
            ItemHologramClosed = 6,
        }

        [SerializeField] private NetworkMessenger _messenger;

        private ConduitHolograms _holograms;
        private ItemConduits _itemConduits;
        private ItemConduitHolograms _itemHolograms;
        private AddonItemNetwork _addonItems;

        private void Awake()
        {
            if (_messenger == null) _messenger = GetComponent<NetworkMessenger>();
            _holograms = FindFirstObjectByType<ConduitHolograms>();
            _itemConduits = FindFirstObjectByType<ItemConduits>();
            _itemHolograms = FindFirstObjectByType<ItemConduitHolograms>();
            if (_itemConduits != null) _itemConduits.EditedByHand += OnItemRunEdited;
            if (_itemHolograms != null)
            {
                _itemHolograms.Opened += OnItemHologramOpened;
                _itemHolograms.Closed += OnItemHologramClosed;
            }

            // OnSpawned can run before Start, so everything is subscribed here.
            _messenger.OnSpawned += SayHello;
            _messenger.MessageToAll += OnMessageToAll;
            _messenger.MessageToController += OnMessageToController;
            PlayerManager.LocalPlayerJoined += OnLocalPlayerJoined;
            PlayerManager.OtherPlayerJoined += SayHello;
            PlayerManager.PlayerLeft += OnPlayerLeft;
            CarriedComponent.Bumped += OnComponentBumped;
            Item.Remove = ItemNetwork.Remove;
            if (_holograms != null)
            {
                _holograms.Opened += OnHologramOpened;
                _holograms.Closed += OnHologramClosed;
                _holograms.Edited += OnConduitEdited;
                _addonItems = new AddonItemNetwork(this, _holograms);
            }
        }

        private void OnDestroy()
        {
            if (_messenger != null)
            {
                _messenger.OnSpawned -= SayHello;
                _messenger.MessageToAll -= OnMessageToAll;
                _messenger.MessageToController -= OnMessageToController;
            }
            PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;
            PlayerManager.OtherPlayerJoined -= SayHello;
            PlayerManager.PlayerLeft -= OnPlayerLeft;
            CarriedComponent.Bumped -= OnComponentBumped;
            if (Item.Remove == ItemNetwork.Remove) Item.Remove = null;
            if (_itemConduits != null) _itemConduits.EditedByHand -= OnItemRunEdited;
            if (_itemHolograms != null)
            {
                _itemHolograms.Opened -= OnItemHologramOpened;
                _itemHolograms.Closed -= OnItemHologramClosed;
            }
            if (_holograms != null)
            {
                _holograms.Opened -= OnHologramOpened;
                _holograms.Closed -= OnHologramClosed;
                _holograms.Edited -= OnConduitEdited;
                _addonItems?.Detach(_holograms);
            }
        }

        private void OnLocalPlayerJoined(PlayerIdentity _) => SayHello();

        private static void OnComponentBumped(CarriedComponent component)
        {
            if (component.TryGetComponent<Community.NetworkGrabbable>(out var grabbable)) grabbable.RequestControl();
        }

        // Called from whichever of the messenger spawning and the local player arriving comes last;
        // the other call finds one of them missing and does nothing.
        private void SayHello()
        {
            var me = PlayerManager.LocalPlayer;
            if (!me.Exists || !_messenger.CanSend) return;

            _messenger.SendToAll((byte)ShipMessageType.Hello, Encoding.UTF8.GetBytes(me.Id));
        }

        private void OnHologramOpened(GridNode tile) => SendHologram(ShipMessageType.HologramOpened, tile);
        private void OnHologramClosed(GridNode tile) => SendHologram(ShipMessageType.HologramClosed, tile);

        private void SendHologram(ShipMessageType type, GridNode tile)
        {
            var me = PlayerManager.LocalPlayer;
            int slot = ConduitNetwork.SlotOf(tile);
            if (!me.Exists || slot < 0 || !_messenger.CanSend) return;

            byte[] id = Encoding.UTF8.GetBytes(me.Id);
            byte[] payload = new byte[4 + id.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(slot), 0, payload, 0, 4);
            Buffer.BlockCopy(id, 0, payload, 4, id.Length);
            _messenger.SendToAll((byte)type, payload);
        }

        private void OnConduitEdited(GridNode tile)
        {
            if (PlayerManager.IsMaster || !WorldManager.IsNetworkReady || !_messenger.CanSend) return;
            var edit = ConduitNetwork.DescribeEdit(tile);
            if (edit == null) return;

            byte[] payload = new byte[edit.Length * 4];
            Buffer.BlockCopy(edit, 0, payload, 0, payload.Length);
            _messenger.SendToController((byte)ShipMessageType.ConduitEdited, payload);
        }

        private void OnItemHologramOpened(Vector3Int cell) => SendItemHologram(ShipMessageType.ItemHologramOpened, cell);
        private void OnItemHologramClosed(Vector3Int cell) => SendItemHologram(ShipMessageType.ItemHologramClosed, cell);

        private void SendItemHologram(ShipMessageType type, Vector3Int cell)
        {
            var me = PlayerManager.LocalPlayer;
            if (!me.Exists || !_messenger.CanSend) return;
            byte[] id = Encoding.UTF8.GetBytes(me.Id);
            byte[] payload = new byte[12 + id.Length];
            Buffer.BlockCopy(new[] { cell.x, cell.y, cell.z }, 0, payload, 0, 12);
            Buffer.BlockCopy(id, 0, payload, 12, id.Length);
            _messenger.SendToAll((byte)type, payload);
        }

        private void OnItemHologramMessage(bool opened, byte[] data)
        {
            if (_itemHolograms == null || data.Length < 12) return;
            int[] xyz = new int[3];
            Buffer.BlockCopy(data, 0, xyz, 0, 12);
            var cell = new Vector3Int(xyz[0], xyz[1], xyz[2]);
            string player = Encoding.UTF8.GetString(data, 12, data.Length - 12);
            string me = PlayerManager.LocalPlayer.Id;
            if (string.IsNullOrEmpty(player) || player == me) return;

            // Both opened it at once: the lower id keeps it.
            if (opened && _itemHolograms.Open == cell && string.CompareOrdinal(me, player) < 0) return;
            _itemHolograms.SetOpenElsewhere(cell, player, opened);
        }

        private void OnItemRunEdited(ItemConduits.Edit edit, Vector3Int cell, GridDirection entry, GridDirection exit)
        {
            if (PlayerManager.IsMaster || !WorldManager.IsNetworkReady || !_messenger.CanSend) return;
            int[] message = { (int)edit, cell.x, cell.y, cell.z, (int)entry, (int)exit };
            byte[] payload = new byte[message.Length * 4];
            Buffer.BlockCopy(message, 0, payload, 0, payload.Length);
            _messenger.SendToController((byte)ShipMessageType.ItemRunEdited, payload);
        }

        private void OnMessageToController(byte id, byte[] data)
        {
            switch ((ShipMessageType)id)
            {
                case ShipMessageType.ItemRunEdited:
                    if (data.Length < 24 || _itemConduits == null || !PlayerManager.IsMaster) return;
                    int[] run = new int[6];
                    Buffer.BlockCopy(data, 0, run, 0, 24);
                    _itemConduits.Apply((ItemConduits.Edit)run[0], new Vector3Int(run[1], run[2], run[3]),
                        (GridDirection)run[4], (GridDirection)run[5]);
                    break;
                case ShipMessageType.ConduitEdited:
                    if (data.Length % 4 != 0) return;
                    int[] edit = new int[data.Length / 4];
                    Buffer.BlockCopy(data, 0, edit, 0, data.Length);
                    ConduitNetwork.ApplyEdit(edit);
                    break;
                default:
                    Debug.LogWarning($"ShipNetwork: unknown controller message {id}", this);
                    break;
            }
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((ShipMessageType)id)
            {
                case ShipMessageType.Hello:
                    string fromId = Encoding.UTF8.GetString(data);
                    var from = PlayerManager.GetPlayer(fromId);
                    Debug.Log($"ShipNetwork: hello from {(from.Exists ? from.ToString() : fromId)}; local {PlayerManager.LocalPlayer}, master={PlayerManager.IsMaster}");
                    break;
                case ShipMessageType.HologramOpened:
                case ShipMessageType.HologramClosed:
                    OnHologramMessage((ShipMessageType)id == ShipMessageType.HologramOpened, data);
                    break;
                case ShipMessageType.ItemHologramOpened:
                case ShipMessageType.ItemHologramClosed:
                    OnItemHologramMessage((ShipMessageType)id == ShipMessageType.ItemHologramOpened, data);
                    break;
                default:
                    Debug.LogWarning($"ShipNetwork: unknown message {id}", this);
                    break;
            }
        }

        private void OnHologramMessage(bool opened, byte[] data)
        {
            if (_holograms == null || data.Length < 4) return;
            var tile = ConduitNetwork.TileAt(BitConverter.ToInt32(data, 0));
            string player = Encoding.UTF8.GetString(data, 4, data.Length - 4);
            string me = PlayerManager.LocalPlayer.Id;
            if (tile == null || string.IsNullOrEmpty(player) || player == me) return;

            // Both opened it at once: the lower id keeps it, and the other's box closes the loser's hologram.
            if (opened && _holograms.Open == tile && string.CompareOrdinal(me, player) < 0) return;

            _holograms.SetOpenElsewhere(tile, player, opened);
        }

        private void OnPlayerLeft(PlayerIdentity who)
        {
            if (_holograms != null) _holograms.ClearOpenElsewhere(who.Id);
            if (_itemHolograms != null) _itemHolograms.ClearOpenElsewhere(who.Id);
        }

        private void OnValidate()
        {
            if (_messenger == null) _messenger = GetComponent<NetworkMessenger>();
        }
    }
}
