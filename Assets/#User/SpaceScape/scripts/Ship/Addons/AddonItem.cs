using System;
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
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
        }

        private void OnEnable() => Grab.selectExited.AddListener(OnLetGo);

        private void OnDisable() => Grab.selectExited.RemoveListener(OnLetGo);

        /// Lets it fall once a hand has had it, then offers it to any slot it was dropped in.
        private void OnLetGo(SelectExitEventArgs args)
        {
            if (Grab.isSelected) return;
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.isKinematic = false;
            Released?.Invoke(this);
        }
    }
}
