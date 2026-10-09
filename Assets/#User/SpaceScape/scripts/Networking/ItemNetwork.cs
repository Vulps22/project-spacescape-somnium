using Fusion;
using SomniumSpace.Worlds.SpaceScape.Ship;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// Items as objects everyone has: spawned like addon items, and despawned for everyone once an item port has
    /// drawn one in, a pack has taken one back or a loader has taken a pack, taking authority first when this
    /// client does not have it (loading.md).
    public static class ItemNetwork
    {
        /// Despawns an item that has gone into a port, a pack or a loader. False for one not on the network, which
        /// the caller then destroys itself.
        public static bool Remove(Item item)
        {
            if (item == null || !item.TryGetComponent<NetworkObject>(out var networked) || !networked.IsValid) return false;
            string context = $"item '{item.name}' taken in";
            if (networked.HasStateAuthority) WorldManager.Despawn(networked, context);
            else item.StartCoroutine(WorldManager.TakeAuthority(networked, granted =>
            {
                if (granted && networked != null) WorldManager.Despawn(networked, context);
            }));
            return true;
        }
    }
}
