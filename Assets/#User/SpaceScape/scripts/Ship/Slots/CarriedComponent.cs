using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A component as something to carry (docs/grid.md, Moving a component). A hand can take it only while it
    /// is loose or its slot is unlocked; taking it lifts it out of the slot. Let go where a slot accepts it
    /// (empty, its size, centred and square enough), it goes in and the slot locks; let go anywhere else, it
    /// falls. Its pose reaches everyone through NetworkGrabbable and NetworkRigidbody3D, and which slot holds
    /// it through the slot's own data (SlotNetwork).
    [RequireComponent(typeof(SlottedComponent))]
    [RequireComponent(typeof(GridNode))]
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarriedComponent : MonoBehaviour
    {
        private SlottedComponent _slotted;
        private GridNode _tile;
        private XRGrabInteractable _grab;
        private Rigidbody _body;

        private void Awake()
        {
            _slotted = GetComponent<SlottedComponent>();
            _tile = GetComponent<GridNode>();
            _grab = GetComponent<XRGrabInteractable>();
            _body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnLetGo);
        }

        private void OnDisable()
        {
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnLetGo);
        }

        private void Update()
        {
            // Never switched off in a hand: that would drop it.
            if (_grab.isSelected) return;
            var slot = _slotted.Slot;
            bool free = slot == null || slot.Unlocked;
            if (_grab.enabled != free) _grab.enabled = free;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            var slot = _slotted.Slot;
            if (slot != null) slot.TakeOut(_tile);
        }

        /// Puts it in the nearest slot that accepts it, or lets it fall.
        private void OnLetGo(SelectExitEventArgs args)
        {
            if (_grab.isSelected) return;

            ComponentSlot best = null;
            float nearest = float.MaxValue;
            foreach (var slot in ComponentSlot.All)
            {
                if (slot == null || !slot.Accepts(_tile)) continue;
                float distance = slot.DistanceTo(_tile);
                if (distance < nearest) { nearest = distance; best = slot; }
            }
            if (best != null && best.PutInFromHand(_tile)) return;

            // XRI put back the kinematic it had when grabbed, which was the slot's.
            _body.isKinematic = false;
        }
    }
}
