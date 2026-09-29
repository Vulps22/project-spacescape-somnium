using System.Collections;
using Fusion;
using SomniumSpace.Worlds.SpaceScape.Community;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// Addon items as objects everyone has (networking.md: an addon item's pose is whoever holds it; one
    /// existing, taken out or put in, is spawned or despawned).
    ///
    /// The item a hologram shows in its addon slot is a local picture, stripped of its networking. When a
    /// hand lifts it out, it is swapped the next frame for a spawned item of the same prefab, at the same
    /// place and size, moved into the same hand. An item installed on a conduit is despawned, taking
    /// authority first when this client does not have it. Size travels with the item's NetworkRigidbody3D
    /// (Sync Scale), written by whoever holds it.
    ///
    /// With no network (the Editor without Photon) none of this runs: the picture stays the item, and an
    /// installed one is destroyed locally.
    public sealed class AddonItemNetwork
    {
        private readonly MonoBehaviour _host;

        public AddonItemNetwork(MonoBehaviour host, ConduitHolograms holograms)
        {
            _host = host;
            holograms.ItemShown += OnItemShown;
            holograms.ItemTakenOut += OnItemTakenOut;
            holograms.RemoveItem = RemoveItem;
            holograms.SizesItem = SizesItem;
        }

        public void Detach(ConduitHolograms holograms)
        {
            holograms.ItemShown -= OnItemShown;
            holograms.ItemTakenOut -= OnItemTakenOut;
            if (holograms.RemoveItem == RemoveItem) holograms.RemoveItem = null;
            if (holograms.SizesItem == SizesItem) holograms.SizesItem = null;
        }

        /// The picture is never spawned, so its NetworkGrabbable would ask Fusion for authority over nothing
        /// when grabbed, and throw.
        private static void OnItemShown(AddonItem item)
        {
            if (item.TryGetComponent<NetworkGrabbable>(out var grabbable)) UnityEngine.Object.Destroy(grabbable);
        }

        private void OnItemTakenOut(AddonItem item, AddonItem prefab)
        {
            if (WorldManager.CanSpawn && prefab != null && prefab.TryGetComponent<NetworkObject>(out var networked))
                _host.StartCoroutine(SwapForSpawned(item, networked));
        }

        /// Next frame, once XRI has finished the grab: spawns the real item where the picture is, at its
        /// size, and moves the hand onto it.
        private static IEnumerator SwapForSpawned(AddonItem picture, NetworkObject prefab)
        {
            yield return null;
            if (picture == null) yield break;

            var t = picture.transform;
            var spawned = WorldManager.Spawn(prefab, t.position, t.rotation, $"addon item '{prefab.name}' taken out");
            if (spawned == null || !spawned.TryGetComponent<AddonItem>(out var item)) yield break;
            item.transform.localScale = t.lossyScale;   // it grows from the slot's size once out of the cell

            var grab = picture.Grab;
            var manager = grab.interactionManager;
            var hand = grab.isSelected ? grab.firstInteractorSelecting : null;
            picture.Retire();
            if (hand != null && manager != null) manager.SelectExit(hand, grab);
            UnityEngine.Object.Destroy(picture.gameObject);
            if (hand != null && manager != null) manager.SelectEnter(hand, item.Grab);
        }

        /// Despawns an installed item for everyone. False for one that is not on the network, which the
        /// hologram then destroys itself.
        private bool RemoveItem(AddonItem item)
        {
            if (!item.TryGetComponent<NetworkObject>(out var networked) || !networked.IsValid) return false;
            item.Hold();   // pinned where it went in until it is gone
            string context = $"addon item '{item.name}' installed";
            if (networked.HasStateAuthority) WorldManager.Despawn(networked, context);
            else _host.StartCoroutine(WorldManager.TakeAuthority(networked, granted =>
            {
                if (granted && networked != null) WorldManager.Despawn(networked, context);
            }));
            return true;
        }

        /// Whoever holds an item sizes it, and whoever has its authority sizes it once it is let go; every
        /// other client is sent its size. An item not on the network is sized here.
        private static bool SizesItem(AddonItem item) =>
            item.Grab.isSelected
            || !item.TryGetComponent<NetworkObject>(out var networked) || !networked.IsValid
            || networked.HasStateAuthority;
    }
}
