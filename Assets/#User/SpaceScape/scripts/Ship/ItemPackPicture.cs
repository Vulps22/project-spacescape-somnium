using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A still, shut copy of a pack: only its meshes, with its count lamps showing how many are in. What a loader
    /// shows for a pack it holds (loading.md: in a loader, a pack is the loader's state, not an object). Never
    /// spawned, nothing to grab, no colliders.
    public sealed class ItemPackPicture
    {
        private readonly Renderer[] _lamps;
        private readonly Material[] _lampOn;
        private readonly Material _lampOff;
        private int _shown = -1;

        /// The pack prefab it shows.
        public ItemPack Prefab { get; }

        /// Its root, at the pack's origin (the bottom of the box).
        public Transform Root { get; }

        public ItemPackPicture(ItemPack prefab, Transform parent)
        {
            Prefab = prefab;
            Root = new GameObject($"{prefab.name} (in loader)").transform;
            Root.SetParent(parent, false);

            var source = prefab.transform;
            var prefabLamps = prefab.Lamps;
            _lamps = new Renderer[prefabLamps.Length];
            _lampOn = new Material[prefabLamps.Length];
            _lampOff = prefab.LampOff;
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var t = renderer.transform;
                var part = new GameObject(t.name);
                part.layer = t.gameObject.layer;
                part.transform.SetParent(Root, false);
                part.transform.localPosition = source.InverseTransformPoint(t.position);
                part.transform.localRotation = Quaternion.Inverse(source.rotation) * t.rotation;
                part.transform.localScale = t.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = part.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                int lamp = System.Array.IndexOf(prefabLamps, renderer);
                if (lamp < 0) continue;
                _lamps[lamp] = copy;
                _lampOn[lamp] = renderer.sharedMaterial;
            }
        }

        /// Lights a lamp for each filled place.
        public void ShowCount(int filled)
        {
            if (filled == _shown) return;
            _shown = filled;
            int count = 0;
            for (int bits = filled; bits != 0; bits &= bits - 1) count++;
            for (int i = 0; i < _lamps.Length; i++)
                if (_lamps[i] != null) _lamps[i].sharedMaterial = i < count ? _lampOn[i] : _lampOff;
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
        }
    }
}
