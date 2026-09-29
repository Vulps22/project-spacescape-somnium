using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// An addon as something to carry: picked up, and let go inside an open conduit's addon slot to
    /// install what it stands for. It stays where it was put until a hand first takes it.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class AddonItem : MonoBehaviour
    {
        [Tooltip("The addon prefab this installs on a conduit: the part that goes on the cable, like the rocker.")]
        [SerializeField] private ConduitAddon _installs;
        [Tooltip("Seconds it takes to shrink to fit an open addon slot, or grow back to full size.")]
        [SerializeField] private float _resizeSeconds = 1f;

        private static readonly List<AddonItem> Items = new List<AddonItem>();
        private Vector3 _fullScale;
        private Vector3? _targetScale;
        private bool _displayed;
        private bool _retired;

        /// Every addon item in the world.
        public static IReadOnlyList<AddonItem> All => Items;

        /// Its size when it is not in a slot: the prefab's.
        public Vector3 FullScale => _fullScale;

        /// How wide or tall its solid parts are at a scale of 1, whichever is larger, for fitting it to a slot.
        public float UnitAcross
        {
            get
            {
                var bounds = new Bounds(); bool any = false;
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
                    var local = r.localBounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                        var point = Vector3.Scale(transform.InverseTransformPoint(r.transform.TransformPoint(corner)), Vector3.one);
                        if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                return any ? Mathf.Max(bounds.size.x, bounds.size.y) : 0f;
            }
        }

        /// Eases to a size over the resize time rather than snapping to it.
        public void ResizeTo(Vector3 scale) => _targetScale = scale;

        /// True while it sits in a hologram's addon slot as the installed addon's item, until a hand takes it.
        public bool Displayed => _displayed;

        /// Stops it answering to hands, for an item about to be replaced or removed: letting go of it offers it
        /// to no slot.
        public void Retire() => _retired = true;

        private void Awake()
        {
            _fullScale = transform.localScale;
            // Its size is this script's alone. Tracking scale, XRI writes back the size it was grabbed at every
            // frame it is held, undoing any resize depending on which runs last.
            Grab.trackScale = false;
            // XRI puts a let-go object back under the parent it had when grabbed. The item shown in a hologram's
            // addon slot started under that slot, and went back under it: gone with the hologram, and sized
            // against the slot's scale.
            Grab.retainTransformParent = false;
        }

        private void Update()
        {
            if (_targetScale == null) return;
            var target = _targetScale.Value;
            float rate = Mathf.Max(_fullScale.magnitude, 1e-4f) / Mathf.Max(0.01f, _resizeSeconds);
            transform.localScale = Vector3.MoveTowards(transform.localScale, target, rate * Time.deltaTime);
            if (transform.localScale == target) _targetScale = null;
        }

        private XRGrabInteractable _grab;
        private Rigidbody _body;

        /// Raised when a hand lets go of any addon item, for an open addon slot to take it.
        public static event Action<AddonItem> Released;

        /// The addon prefab this installs.
        public ConduitAddon Installs => _installs;

        /// What a hand grabs.
        public XRGrabInteractable Grab => _grab != null ? _grab : _grab = GetComponent<XRGrabInteractable>();

        /// Pins it where it is, for sitting in a slot until it is taken.
        public void Hold()
        {
            _displayed = true;
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
        }

        private void OnEnable()
        {
            Items.Add(this);
            Grab.selectExited.AddListener(OnLetGo);
        }

        private void OnDisable()
        {
            Items.Remove(this);
            Grab.selectExited.RemoveListener(OnLetGo);
        }

        /// Lets it fall once a hand has had it, then offers it to any slot it was dropped in.
        private void OnLetGo(SelectExitEventArgs args)
        {
            if (Grab.isSelected || _retired) return;
            _displayed = false;
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.isKinematic = false;
            Released?.Invoke(this);
        }
    }
}
