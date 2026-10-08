using Fusion;
using SomniumSpace.Worlds.SpaceScape.Player;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Networking
{
    /// Puts an item in its box whenever the box is empty: take one out and another appears. Any Item: an addon
    /// item, or a loadable item like a fuel rod. The item hangs
    /// where it appears, out of physics, until a hand first lets go of it (Item); from then on it is an
    /// ordinary physics object for good. The master alone looks and spawns, through Fusion, so everyone (late
    /// joiners too) has the same items; there is nothing else to share, since whether the box is empty is
    /// asked again each time. A variant, like a +4 junction, is the same prefab with its state set: the master
    /// spawns it, so it has the item's authority and AddonItemStateNetwork carries the state to everyone.
    ///
    /// With no network at all (only ever the Editor without Photon) it makes the item locally.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ItemDispenser : MonoBehaviour
    {
        private const float OfflineAfterSeconds = 1f;

        [Tooltip("The item it puts out: an Item prefab (an addon item, a fuel rod) registered on SceneNetworking.")]
        [SerializeField] private Item _item;
        [Tooltip("The state each addon item is given, for one whose state travels with it: a junction's extra segments, 1 to 4. -1: the prefab's own. Other items ignore it.")]
        [SerializeField] private int _state = -1;
        [Tooltip("What counts as something in the box. Triggers never count, nor does anything under this object.")]
        [SerializeField] private LayerMask _occupantLayers = ~0;
        [Tooltip("Seconds between looks at the box.")]
        [SerializeField] private float _checkSeconds = 0.5f;

        private static readonly Collider[] Found = new Collider[16];
        private BoxCollider _box;
        private float _nextCheck;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            if (!_box.isTrigger) _box.isTrigger = true;
        }

        private void Update()
        {
            if (_item == null || Time.time < _nextCheck) return;
            _nextCheck = Time.time + _checkSeconds;

            bool online = WorldManager.IsNetworkReady;
            bool offline = !online && Application.isEditor && !WorldManager.HasRunner && Time.timeSinceLevelLoad > OfflineAfterSeconds;
            if (online && !(PlayerManager.IsMaster && WorldManager.CanSpawn)) return;
            if (!online && !offline) return;   // the runner can take a few seconds to appear in a world
            if (Occupied()) return;

            var at = _box.bounds.center;
            if (online)
            {
                if (!_item.TryGetComponent<NetworkObject>(out var prefab))
                {
                    Debug.LogError($"ItemDispenser '{name}': '{_item.name}' has no NetworkObject, so it cannot be shared", this);
                    _item = null;
                    return;
                }
                var spawned = WorldManager.Spawn(prefab, at, transform.rotation, $"dispenser '{name}'");
                if (spawned != null && spawned.TryGetComponent<Item>(out var made)) Prepare(made);
            }
            else
            {
                Prepare(Instantiate(_item, at, transform.rotation));
            }
        }

        /// Gives a new addon item its state, and holds any item still where it appeared until a hand has had it.
        private void Prepare(Item made)
        {
            if (_state >= 0 && made is AddonItem addon) addon.State = _state;
            made.Body.isKinematic = true;
        }

        /// True when anything solid on the Occupant Layers, other than this dispenser, is inside the box.
        private bool Occupied()
        {
            var t = _box.transform;
            Vector3 centre = t.TransformPoint(_box.center);
            Vector3 half = Vector3.Scale(_box.size * 0.5f, t.lossyScale);
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            int count = Physics.OverlapBoxNonAlloc(centre, half, Found, t.rotation, _occupantLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (Found[i] != null && !Found[i].transform.IsChildOf(transform)) return true;
            return false;
        }

        private void OnValidate()
        {
            if (TryGetComponent<BoxCollider>(out var box) && !box.isTrigger) box.isTrigger = true;
        }
    }
}
