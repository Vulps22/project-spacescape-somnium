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
    /// Holograms: opening and closing a conduit's hologram, power or item, is told to everyone by its cell, so other
    /// players see the conduit boxed and cannot open it. Nothing is kept for late joiners: a hologram is only ever
    /// usable by the one who opened it. When two open the same conduit at once, the lower player id keeps it.
    ///
    /// Conduit edits: a hand on a non-master client that changes a conduit cell, power or item (ConduitCells), sends
    /// what it did to the master, which does the same; the cell chunks carry it to everyone (ConduitCellNetwork).
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
            HologramOpened = 1,      // retired with the conduit lattice (Conduits V2, 2026-10-12): by slot
            HologramClosed = 2,      // retired, as HologramOpened
            ConduitEdited = 3,       // retired, as HologramOpened
            ItemRunEdited = 4,       // retired with item conduit runs (2026-10-09)
            CellHologramOpened = 5,  // a player opened a conduit cell's hologram, power or item: x, y, z as ints, then their id
            CellHologramClosed = 6,
            ItemConduitEdited = 7,   // retired (Conduits V2): ConduitCellEdited replaces it
            ConduitCellEdited = 8,   // a conduit cell changed elsewhere: edit, x, y, z, a, b as ints (ConduitCells.Edit)
        }

        [SerializeField] private NetworkMessenger _messenger;

        private ConduitHolograms _holograms;
        private ConduitCells _cells;
        private ItemConduitHolograms _itemHolograms;
        private AddonItemNetwork _addonItems;

        private void Awake()
        {
            if (_messenger == null) _messenger = GetComponent<NetworkMessenger>();
            _holograms = FindFirstObjectByType<ConduitHolograms>();
            _cells = FindFirstObjectByType<ConduitCells>();
            _itemHolograms = FindFirstObjectByType<ItemConduitHolograms>();
            if (_cells != null) _cells.EditedByHand += OnCellEdited;
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
            if (_cells != null) _cells.EditedByHand -= OnCellEdited;
            if (_itemHolograms != null)
            {
                _itemHolograms.Opened -= OnItemHologramOpened;
                _itemHolograms.Closed -= OnItemHologramClosed;
            }
            if (_holograms != null)
            {
                _holograms.Opened -= OnHologramOpened;
                _holograms.Closed -= OnHologramClosed;
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

        private void OnHologramOpened(GridNode tile) => SendCellHologram(ShipMessageType.CellHologramOpened, tile.Coordinate);
        private void OnHologramClosed(GridNode tile) => SendCellHologram(ShipMessageType.CellHologramClosed, tile.Coordinate);

        private void OnItemHologramOpened(Vector3Int cell) => SendCellHologram(ShipMessageType.CellHologramOpened, cell);
        private void OnItemHologramClosed(Vector3Int cell) => SendCellHologram(ShipMessageType.CellHologramClosed, cell);

        private void SendCellHologram(ShipMessageType type, Vector3Int cell)
        {
            var me = PlayerManager.LocalPlayer;
            if (!me.Exists || !_messenger.CanSend) return;
            byte[] id = Encoding.UTF8.GetBytes(me.Id);
            byte[] payload = new byte[12 + id.Length];
            Buffer.BlockCopy(new[] { cell.x, cell.y, cell.z }, 0, payload, 0, 12);
            Buffer.BlockCopy(id, 0, payload, 12, id.Length);
            _messenger.SendToAll((byte)type, payload);
        }

        /// A player opened or closed a conduit cell's hologram: a power conduit's tile is boxed by ConduitHolograms, any
        /// other cell by ItemConduitHolograms.
        private void OnCellHologramMessage(bool opened, byte[] data)
        {
            if (data.Length < 12) return;
            int[] xyz = new int[3];
            Buffer.BlockCopy(data, 0, xyz, 0, 12);
            var cell = new Vector3Int(xyz[0], xyz[1], xyz[2]);
            string player = Encoding.UTF8.GetString(data, 12, data.Length - 12);
            string me = PlayerManager.LocalPlayer.Id;
            if (string.IsNullOrEmpty(player) || player == me) return;
            if (_cells != null) _cells.SetOpen(cell, player, opened);   // no conduit is uninstalled while a hand has it open

            var grid = PowerGrid.Current;
            var tile = grid != null ? grid.TileAt(cell) : null;
            if (tile != null && tile.TryGetComponent<ConduitBehaviourModule>(out _))
            {
                if (_holograms == null) return;
                // Both opened it at once: the lower id keeps it, and the other's box closes the loser's hologram.
                if (opened && _holograms.Open == tile && string.CompareOrdinal(me, player) < 0) return;
                _holograms.SetOpenElsewhere(tile, player, opened);
                return;
            }
            if (_itemHolograms == null) return;
            if (opened && _itemHolograms.Open == cell && string.CompareOrdinal(me, player) < 0) return;
            _itemHolograms.SetOpenElsewhere(cell, player, opened);
        }

        private void OnCellEdited(ConduitCells.Edit edit, Vector3Int cell, int a, int b)
        {
            if (PlayerManager.IsMaster || !WorldManager.IsNetworkReady || !_messenger.CanSend) return;
            int[] message = { (int)edit, cell.x, cell.y, cell.z, a, b };
            byte[] payload = new byte[message.Length * 4];
            Buffer.BlockCopy(message, 0, payload, 0, payload.Length);
            _messenger.SendToController((byte)ShipMessageType.ConduitCellEdited, payload);
        }

        private void OnMessageToController(byte id, byte[] data)
        {
            switch ((ShipMessageType)id)
            {
                case ShipMessageType.ConduitCellEdited:
                    if (data.Length < 24 || _cells == null || !PlayerManager.IsMaster) return;
                    int[] cellEdit = new int[6];
                    Buffer.BlockCopy(data, 0, cellEdit, 0, 24);
                    _cells.Apply((ConduitCells.Edit)cellEdit[0], new Vector3Int(cellEdit[1], cellEdit[2], cellEdit[3]), cellEdit[4], cellEdit[5]);
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
                case ShipMessageType.CellHologramOpened:
                case ShipMessageType.CellHologramClosed:
                    OnCellHologramMessage((ShipMessageType)id == ShipMessageType.CellHologramOpened, data);
                    break;
                default:
                    Debug.LogWarning($"ShipNetwork: unknown message {id}", this);
                    break;
            }
        }

        private void OnPlayerLeft(PlayerIdentity who)
        {
            if (_holograms != null) _holograms.ClearOpenElsewhere(who.Id);
            if (_itemHolograms != null) _itemHolograms.ClearOpenElsewhere(who.Id);
            if (_cells != null) _cells.ClearOpen(who.Id);
        }

        private void OnValidate()
        {
            if (_messenger == null) _messenger = GetComponent<NetworkMessenger>();
        }
    }
}
