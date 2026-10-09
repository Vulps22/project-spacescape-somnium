using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Something a player carries: an addon item, or a loadable item like a fuel rod. A dispenser puts out any of
    /// them. One that appears out of physics (a dispenser holds it still where it appeared) stays put until a hand
    /// first lets go of it; from then on it is an ordinary physics object.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public abstract class Item : MonoBehaviour
    {
        /// Removes an item that has gone into a port, back into a pack, or into a loader, for everyone. Set by the
        /// networking; false when it could not, and the caller then destroys the item itself (the Editor with no
        /// network).
        public static Func<Item, bool> Remove;

        private XRGrabInteractable _grab;
        private Rigidbody _body;

        /// What a hand grabs.
        public XRGrabInteractable Grab => _grab != null ? _grab : _grab = GetComponent<XRGrabInteractable>();

        /// Its physics body.
        public Rigidbody Body => _body != null ? _body : _body = GetComponent<Rigidbody>();

        protected virtual void OnEnable() => Grab.selectExited.AddListener(OnLetGo);

        protected virtual void OnDisable() => Grab.selectExited.RemoveListener(OnLetGo);

        /// Removes it for everyone, or destroys it here when there is no network.
        public void RemoveForEveryone()
        {
            if (Remove == null || !Remove(this)) Destroy(gameObject);
        }

        /// Lets it fall once the last hand has let go of it.
        protected virtual void OnLetGo(SelectExitEventArgs args)
        {
            if (Grab.isSelected) return;
            Body.isKinematic = false;
        }
    }
}
