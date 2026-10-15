using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A box of the ship that every player has their own copy of (docs/tutorial-proposal.md, Plan D → The bay is local):
    /// nothing inside it is sent or read over the network. Its conduit cells, slots and their components, access panels
    /// and failures are decided on each client. Its wiring must be an island: no conduit crosses its edge.
    [RequireComponent(typeof(BoxCollider))]
    [DefaultExecutionOrder(-10000)]   // registered before anything asks whether it is inside
    public sealed class LocalZone : MonoBehaviour
    {
        private static readonly List<LocalZone> Zones = new List<LocalZone>();
        private Bounds _bounds;

        private void Awake()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            _bounds = box.bounds;
            Zones.Add(this);
        }

        private void OnDestroy() => Zones.Remove(this);

        /// True when the point is inside any zone.
        public static bool Contains(Vector3 world)
        {
            foreach (var zone in Zones)
                if (zone != null && zone._bounds.Contains(world)) return true;
            return false;
        }

        /// True when the cell's centre is inside any zone.
        public static bool Contains(Vector3Int cell) => Zones.Count > 0 && Contains(GridCell.ToWorld(cell));

        /// A copy of a networked prefab that lives on this client only: its network parts are taken off before it wakes,
        /// so it behaves as it does in the Editor with no network.
        public static GameObject MakeLocal(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            // Made under a hidden holder, so nothing on it wakes until its network parts are gone; the prefab is untouched.
            var holder = new GameObject("Local copy");
            holder.SetActive(false);
            var made = Instantiate(prefab, position, rotation, holder.transform);
            StripNetwork(made);
            made.transform.SetParent(null, true);
            Destroy(holder);
            return made;
        }

        /// Takes off every Fusion behaviour and network object, so nothing on it looks for a network.
        public static void StripNetwork(GameObject made)
        {
            foreach (var grab in made.GetComponentsInChildren<Community.NetworkGrabbable>(true)) DestroyImmediate(grab);
            foreach (var teleport in made.GetComponentsInChildren<Community.TeleportNetworkRigidbody3D>(true)) DestroyImmediate(teleport);
            foreach (var behaviour in made.GetComponentsInChildren<Fusion.NetworkBehaviour>(true)) DestroyImmediate(behaviour);
            foreach (var networked in made.GetComponentsInChildren<Fusion.NetworkObject>(true)) DestroyImmediate(networked);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
