using SomniumSpace.Network.Bridge;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// A carried addon item's state (a junction's count), for everyone: whoever has the item's authority writes
    /// it, everyone else takes it, late joiners included. Only an item whose addon's state travels with it needs
    /// one. The item shown in a hologram's slot is never spawned, so this does nothing there.
    [RequireComponent(typeof(AddonItem))]
    public sealed class AddonItemStateNetwork : MonoBehaviour
    {
        [Tooltip("One int: the state plus one, 0 until written.")]
        [SerializeField] private NetworkBridgeData _data;

        private AddonItem _item;

        private bool Live => _data != null && _data.Object != null && _data.Object.IsValid;

        private void Awake() => _item = GetComponent<AddonItem>();

        private void Update()
        {
            if (!Live) return;
            var ints = _data.IntArray;
            if (_data.Object.HasStateAuthority)
            {
                int written = _item.State + 1;
                if (ints[0] != written) ints.Set(0, written);
            }
            else if (ints[0] > 0 && _item.State != ints[0] - 1)
            {
                _item.State = ints[0] - 1;
            }
        }

        private void OnValidate()
        {
            if (_data == null) _data = GetComponent<NetworkBridgeData>();
        }
    }
}
