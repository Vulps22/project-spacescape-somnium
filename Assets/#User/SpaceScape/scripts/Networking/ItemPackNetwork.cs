using System.Collections;
using Fusion;
using SomniumSpace.Network.Bridge;
using SomniumSpace.Worlds.SpaceScape.Community;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// A pack's state for everyone (loading.md → Networking): which places are filled and whether the lid is
    /// open. Whoever has the pack's authority writes it, everyone else takes it, late joiners included. A hand
    /// on a client without the authority changes the pack at once and takes the authority to write it; until
    /// that comes back, this client keeps its own state. If it is refused, the next read puts the state back.
    ///
    /// It also makes the pack's pictures harmless (never spawned, so their network grab is stripped, as
    /// AddonItemNetwork does for a hologram's) and swaps a picture a hand takes for a spawned item in that hand.
    [RequireComponent(typeof(ItemPack))]
    public sealed class ItemPackNetwork : MonoBehaviour
    {
        [Tooltip("Two ints: the filled places plus one, and the lid (1 shut, 2 open); 0 until written.")]
        [SerializeField] private NetworkBridgeData _data;

        private ItemPack _pack;
        private bool _claiming;

        private bool Live => _data != null && _data.Object != null && _data.Object.IsValid;

        private void Awake()
        {
            _pack = GetComponent<ItemPack>();
            _pack.PictureMade += OnPictureMade;
            _pack.ChangedByHand += OnChangedByHand;
            _pack.TakeOut = TakeOut;
        }

        private void OnDestroy()
        {
            if (_pack == null) return;
            _pack.PictureMade -= OnPictureMade;
            _pack.ChangedByHand -= OnChangedByHand;
            _pack.TakeOut = null;
        }

        private void Update()
        {
            if (!Live) return;
            var ints = _data.IntArray;
            if (_data.Object.HasStateAuthority)
            {
                int filled = _pack.Filled + 1, open = _pack.Open ? 2 : 1;
                if (ints[0] != filled) ints.Set(0, filled);
                if (ints[1] != open) ints.Set(1, open);
            }
            else if (!_claiming && ints[0] > 0 && ints[1] > 0)
            {
                int filled = ints[0] - 1;
                bool open = ints[1] == 2;
                if (_pack.Filled != filled || _pack.Open != open) _pack.Apply(filled, open);
            }
        }

        private void OnChangedByHand()
        {
            if (!Live || _data.Object.HasStateAuthority || _claiming) return;
            _claiming = true;
            StartCoroutine(WorldManager.TakeAuthority(_data.Object, granted => _claiming = false));
        }

        private static void OnPictureMade(LoadableItem picture)
        {
            if (picture.TryGetComponent<NetworkGrabbable>(out var grabbable)) DestroyImmediate(grabbable);
        }

        /// Spawns a real item where the picture is, next frame once XRI has finished the grab, and moves the
        /// hand onto it. False with no network, and the pack lets the picture be the item.
        private bool TakeOut(LoadableItem picture, LoadableItem prefab)
        {
            if (!WorldManager.CanSpawn || prefab == null || !prefab.TryGetComponent<NetworkObject>(out var networked)) return false;
            StartCoroutine(SwapForSpawned(picture, networked));
            return true;
        }

        private IEnumerator SwapForSpawned(LoadableItem picture, NetworkObject prefab)
        {
            yield return null;
            if (picture == null) yield break;

            var t = picture.transform;
            var spawned = WorldManager.Spawn(prefab, t.position, t.rotation, $"item '{prefab.name}' taken from pack '{name}'");
            var grab = picture.Grab;
            var manager = grab.interactionManager;
            var hand = grab.isSelected ? grab.firstInteractorSelecting : null;
            _pack.ReturnPicture(picture);
            if (spawned == null || !spawned.TryGetComponent<LoadableItem>(out var item)) yield break;
            if (hand != null && manager != null) manager.SelectEnter(hand, item.Grab);
        }

        private void OnValidate()
        {
            if (_data == null) _data = GetComponent<NetworkBridgeData>();
        }
    }
}
