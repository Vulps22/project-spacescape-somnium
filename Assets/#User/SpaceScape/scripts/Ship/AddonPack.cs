using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A pack of conduit addons (loading.md → The addon pack), for carrying a handful from the dispensers to where the
    /// work is. Red where the fuel pack is blue. Eight places stand in a lattice round a solid middle block, rising with
    /// the lid, each holding any addon item at full size, upright, its front facing out so it can be read. It starts
    /// empty.
    ///
    /// Each place keeps which addon is in it (its position in Kinds) and its state (a junction's count), packed into
    /// Contents, so a loader carries a pack through intact and the networking shares it. An addon item let go near an
    /// empty place in the open pack goes in, and is removed; a hand taking one is given a real item (TakeOutAddon,
    /// set by the networking), state and all. Its lamps light one per filled place. The lid is the fuel pack's.
    public sealed class AddonPack : ItemPack
    {
        private const int BitsPerPlace = 4;   // kind (two bits) and a junction's count less one (two bits)

        [Header("Addons")]
        [Tooltip("The addon items it holds, one per kind (switch, junction, voltmeter): at most four. A place keeps its addon by position here, so only ever add to the end.")]
        [SerializeField] private AddonItem[] _kinds = new AddonItem[0];
        [Tooltip("How near an empty place an addon item's centre must be let go to go in, in metres.")]
        [SerializeField] private float _addonDistance = 0.3f;

        private int _contents;
        private AddonItem[] _shown = new AddonItem[0];
        private readonly System.Collections.Generic.Dictionary<AddonItem, int> _placeOf = new System.Collections.Generic.Dictionary<AddonItem, int>();

        /// Raised for each picture of an addon as it is made, before any hand can reach it, for the networking to strip.
        public event Action<AddonItem> AddonPictureMade;

        /// Gives the hand holding a picture a real addon item in its place, with its state; true when it did, false
        /// with no network. Set by the networking.
        public Func<AddonItem, AddonItem, int, bool> TakeOutAddon;

        public override int Contents
        {
            get => _contents;
            set => _contents = value;
        }

        protected override void Start()
        {
            base.Start();
            _shown = new AddonItem[Capacity];
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            AddonItem.Released += OnAddonLetGo;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AddonItem.Released -= OnAddonLetGo;
        }

        private void OnDestroy()
        {
            foreach (var picture in _shown)
                if (picture != null) Destroy(picture.gameObject);
        }

        /// Which addon a place holds (its position in Kinds), and its state.
        private int KindAt(int place) => (_contents >> (place * BitsPerPlace)) & 3;

        private int StateAt(int place) => ((_contents >> (place * BitsPerPlace + 2)) & 3) + JunctionAddon.MinExtra;

        private void Put(int place, int kind, int state)
        {
            int shift = place * BitsPerPlace;
            int bits = (kind & 3) | ((Mathf.Clamp(state - JunctionAddon.MinExtra, 0, 3) & 3) << 2);
            _contents = (_contents & ~(0xF << shift)) | (bits << shift);
        }

        /// The kind of an addon item: the position in Kinds of the one installing the same addon, or -1.
        private int KindOf(AddonItem item)
        {
            for (int i = 0; i < _kinds.Length && i < 4; i++)
                if (_kinds[i] != null && _kinds[i].Installs == item.Installs) return i;
            return -1;
        }

        private void LateUpdate()
        {
            if (_shown.Length != Capacity) return;
            bool visible = Raised > 0f, takeable = Raised >= 1f, appeared = false;
            for (int i = 0; i < Capacity; i++)
            {
                var picture = _shown[i];
                if (picture != null && picture.Grab.isSelected) continue;
                bool filled = (Filled & (1 << i)) != 0;
                int kind = KindAt(i);
                var wanted = filled && kind < _kinds.Length ? _kinds[kind] : null;
                // The wrong addon, or a junction of another count, is made again.
                if (picture != null && (wanted == null || !_placeOf.ContainsKey(picture) || picture.name != Name(wanted, i) ||
                                        (picture.CarriesState && picture.State != StateAt(i))))
                {
                    _placeOf.Remove(picture);
                    Destroy(picture.gameObject);
                    _shown[i] = picture = null;
                }
                if (picture == null && wanted != null) { _shown[i] = picture = MakePicture(i, wanted); appeared = true; }
                if (picture == null) continue;
                bool shown = visible;
                if (picture.gameObject.activeSelf != shown) { picture.gameObject.SetActive(shown); appeared |= shown; }
                if (picture.Grab.enabled != takeable) picture.Grab.enabled = takeable;
            }
            if (appeared) IgnoreOwnColliders();
        }

        private static string Name(AddonItem prefab, int place) => $"{prefab.name} (in pack, {place + 1})";

        /// A local, never-spawned copy of the addon item standing in a place, its front facing out.
        private AddonItem MakePicture(int place, AddonItem prefab)
        {
            var at = PlaceAt(place);
            if (at == null) return null;
            var picture = Instantiate(prefab, at);
            picture.name = Name(prefab, place);
            picture.transform.localPosition = Vector3.zero;
            picture.transform.localRotation = Quaternion.identity;
            picture.transform.localScale = prefab.transform.localScale;
            picture.Hold();
            if (picture.CarriesState) picture.State = StateAt(place);
            AddonPictureMade?.Invoke(picture);
            picture.Grab.selectEntered.AddListener(OnPictureTaken);
            _placeOf[picture] = place;
            picture.gameObject.SetActive(false);
            return picture;
        }

        /// A hand took the addon in a place: the place empties, and the hand is given a real item. With no network the
        /// picture itself becomes the item.
        private void OnPictureTaken(SelectEnterEventArgs args)
        {
            var picture = args.interactableObject.transform.GetComponent<AddonItem>();
            if (picture == null || !_placeOf.TryGetValue(picture, out int place) || (Filled & (1 << place)) == 0) return;

            var prefab = _kinds[KindAt(place)];
            int state = StateAt(place);
            picture.Grab.selectEntered.RemoveListener(OnPictureTaken);
            _placeOf.Remove(picture);
            _shown[place] = null;
            SetFilledByHand(Filled & ~(1 << place));
            if (TakeOutAddon != null && TakeOutAddon(picture, prefab, state)) return;

            // No network: the picture is the item now.
            picture.Grab.retainTransformParent = false;
            picture.transform.SetParent(null, true);
        }

        /// An addon item let go near an empty place in the open pack goes in.
        private void OnAddonLetGo(AddonItem item)
        {
            if (item == null || Raised < 1f || _placeOf.ContainsKey(item) || !item.Grab.enabled) return;
            int kind = KindOf(item);
            if (kind < 0) return;
            int best = -1;
            float nearest = _addonDistance;
            for (int i = 0; i < Capacity; i++)
            {
                var at = PlaceAt(i);
                if ((Filled & (1 << i)) != 0 || at == null) continue;
                float distance = Vector3.Distance(item.transform.position, at.position);
                if (distance <= nearest) { nearest = distance; best = i; }
            }
            if (best < 0) return;

            // Out of sight at once, its picture in its place, while the despawn goes through.
            int state = item.CarriesState ? item.State : JunctionAddon.MinExtra;
            item.Retire();
            item.Grab.enabled = false;
            item.Body.isKinematic = true;
            foreach (var c in item.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var r in item.GetComponentsInChildren<Renderer>()) r.enabled = false;
            Put(best, kind, state);
            SetFilledByHand(Filled | (1 << best));
            item.RemoveForEveryone();
        }
    }
}
