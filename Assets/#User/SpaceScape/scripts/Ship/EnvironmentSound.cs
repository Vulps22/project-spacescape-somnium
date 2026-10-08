using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Makes a sound source in code, for the one thing that still needs it: the conduit hologram, which is built in
    /// code. Everything else has its AudioSource on a child object in its prefab. Somnium sets a sound's volume by its
    /// GameObject's layer, so every source is a dedicated child object on the Environment layer.
    public static class EnvironmentSound
    {
        public const string LayerName = "Environment";

        /// A 3D sound source on its own child object, on the Environment layer, quiet past maxDistance metres.
        public static AudioSource Make(Transform parent, string name, float minDistance, float maxDistance)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer >= 0) holder.layer = layer;
            else Debug.LogError($"[SpaceScape] No '{LayerName}' layer: sound '{name}' plays on Default and ignores the volume controls", parent);

            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            return source;
        }
    }
}
