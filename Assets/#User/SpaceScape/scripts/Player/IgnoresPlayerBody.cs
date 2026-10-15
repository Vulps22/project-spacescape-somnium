using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Player
{
    /// Keeps this object's colliders from touching the local player's body, while leaving them solid so a
    /// hand can still grab them. For things that follow a hand, like hologram handles: solid and moved
    /// into the player's upright capsule, they shove the player back, and the handle goes with them.
    ///
    /// Works by layer override: while enabled, every collider on it excludes the layer Somnium puts the body capsule on
    /// (Ignore Raycast, the XR.Body CharacterController). Set once, so it costs nothing per frame; it replaced
    /// re-ignoring every collider pair four times a second, which gathered the avatar's colliders for every copy and
    /// tanked the frame rate with many players and items (2026-10-13). Switched off, the colliders touch the body again,
    /// so a carried component is solid once it is put down.
    public sealed class IgnoresPlayerBody : MonoBehaviour
    {
        private const string BodyLayer = "Ignore Raycast";

        private void OnEnable() => Exclude(true);

        private void OnDisable() => Exclude(false);

        private void Exclude(bool exclude)
        {
            int layer = LayerMask.NameToLayer(BodyLayer);
            if (layer < 0) return;
            int bit = 1 << layer;
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                var mask = c.excludeLayers;
                mask.value = exclude ? mask.value | bit : mask.value & ~bit;
                c.excludeLayers = mask;
            }
        }
    }
}
