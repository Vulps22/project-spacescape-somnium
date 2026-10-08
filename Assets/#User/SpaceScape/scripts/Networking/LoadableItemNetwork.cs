using Fusion;
using SomniumSpace.Worlds.SpaceScape.Ship;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// Loadable items as objects everyone has: spawned like addon items, and despawned for everyone once an item
    /// port has drawn one in or a pack has taken one back, taking authority first when this client does not have it (loading.md).
    public static class LoadableItemNetwork
    {
        /// Despawns an item that has gone into a port or back into a pack. False for one not on the network, which
        /// the caller then destroys itself.
        public static bool Remove(LoadableItem item)
        {
            if (item == null || !item.TryGetComponent<NetworkObject>(out var networked) || !networked.IsValid) return false;
            string context = $"loadable item '{item.name}' loaded";
            if (networked.HasStateAuthority) WorldManager.Despawn(networked, context);
            else item.StartCoroutine(WorldManager.TakeAuthority(networked, granted =>
            {
                if (granted && networked != null) WorldManager.Despawn(networked, context);
            }));
            return true;
        }
    }
}
