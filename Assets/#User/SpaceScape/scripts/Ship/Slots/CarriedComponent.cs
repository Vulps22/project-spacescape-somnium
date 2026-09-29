using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using TMPro;
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
    ///
    /// It has weight: held, it lags the hand and hangs below it, both by its Rigidbody's mass; loose, it is
    /// solid to players and to other components, and a heavier one shoves a lighter one. Held inside a slot
    /// of its size, the slot shows how it sits, and the hand buzzes when it would snap in.
    [RequireComponent(typeof(SlottedComponent))]
    [RequireComponent(typeof(GridNode))]
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarriedComponent : MonoBehaviour
    {
        [Header("Weight")]
        [Tooltip("The mass, in kg, at which a component is as sluggish and droops as far as it ever does. Lighter ones are in proportion.")]
        [SerializeField] private float _heaviestKilograms = 2000f;
        [Tooltip("How hard the heaviest component follows the hand, 0 to 1: the share of the gap it closes each physics step. 1 keeps up exactly.")]
        [SerializeField, Range(0.05f, 1f)] private float _followWhenHeaviest = 0.15f;
        [Tooltip("How far below the hand's hold the heaviest component hangs, in metres.")]
        [SerializeField] private float _sagWhenHeaviest = 0.25f;

        [Header("Placement guide")]
        [Tooltip("Strength of the buzz on the holding hand when the component would snap in, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float _snapBuzzAmplitude = 0.6f;
        [Tooltip("Length of that buzz, in seconds.")]
        [SerializeField] private float _snapBuzzSeconds = 0.1f;

        private static readonly List<Renderer> Renderers = new List<Renderer>();

        private SlottedComponent _slotted;
        private GridNode _tile;
        private XRGrabInteractable _grab;
        private Rigidbody _body;
        private IgnoresPlayerBody _ignoresPlayer;
        private ComponentSlot _guided;
        private ComponentSlot.Guide _shown;

        /// Raised when this client's hand swings a component into a loose one, for the network to hand this
        /// client the loose one's physics, so the shove happens where the holder is.
        public static event Action<CarriedComponent> Bumped;

        /// True while it sits in no slot.
        public bool Loose => _slotted.Slot == null;

        private void Awake()
        {
            _slotted = GetComponent<SlottedComponent>();
            _tile = GetComponent<GridNode>();
            _grab = GetComponent<XRGrabInteractable>();
            _body = GetComponent<Rigidbody>();
            _ignoresPlayer = GetComponent<IgnoresPlayerBody>();
            if (_ignoresPlayer != null) _ignoresPlayer.enabled = false;   // solid until a hand has it

            FitCollider();

            // Physics-driven, so it lags the hand by its weight and shoves what it meets.
            float heft = Mathf.Clamp01(_body.mass / Mathf.Max(1f, _heaviestKilograms));
            _grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            _grab.velocityScale = Mathf.Lerp(1f, _followWhenHeaviest, heft);
            _grab.angularVelocityScale = Mathf.Lerp(1f, _followWhenHeaviest, heft);
            // Its own transformer steers it; XRI's default would steer it too, or instead.
            _grab.addDefaultGrabTransformers = false;
            var heftTransformer = GetComponent<HeftGrabTransformer>() ?? gameObject.AddComponent<HeftGrabTransformer>();
            heftTransformer.Sag = _sagWhenHeaviest * heft;
        }

        /// Fits the grab box to the model you can see, not the slot's volume: the model can be smaller, and the
        /// box's surface floated in the air around it. Leaves out text, particles, and the parts of anything
        /// with a hand interaction of its own (the reactor's lever), so the box never covers them.
        private void FitCollider()
        {
            if (!TryGetComponent<BoxCollider>(out var box)) return;
            GetComponentsInChildren(false, Renderers);
            var bounds = new Bounds();
            bool any = false;
            foreach (var r in Renderers)
            {
                if (!(r is MeshRenderer) || !r.enabled || r.GetComponent<TMP_Text>() != null) continue;
                var owner = r.GetComponentInParent<XRBaseInteractable>(true);
                if (owner != null && owner != _grab) continue;
                var local = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var point = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                    else bounds.Encapsulate(point);
                }
            }
            Renderers.Clear();
            if (!any) return;
            box.center = bounds.center;
            box.size = bounds.size;
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
            Guide(null, ComponentSlot.Guide.Hidden);
        }

        private void Update()
        {
            if (_grab.isSelected)
            {
                // Velocity tracking moves only a body that is not kinematic. NetworkGrabbable sets the prefab's
                // kinematic (true, so a spawned component waits still for its slot) back on a hand that has
                // yet to get authority, and again once it has it.
                if (_body.isKinematic) _body.isKinematic = false;
                UpdateGuide();
                return;
            }

            // Never switched off in a hand: that would drop it.
            var slot = _slotted.Slot;
            bool free = slot == null || slot.Unlocked;
            if (_grab.enabled != free) _grab.enabled = free;
        }

        /// Shows the guide on the empty slot of its size that its centre is inside, and buzzes the hand when it
        /// turns to "will snap in".
        private void UpdateGuide()
        {
            ComponentSlot inside = null;
            foreach (var slot in ComponentSlot.All)
                if (slot != null && slot.Component == null && slot.Fits(_tile) && slot.Contains(transform.position))
                {
                    inside = slot;
                    break;
                }

            var guide = ComponentSlot.Guide.Hidden;
            if (inside != null)
            {
                bool positioned = inside.IsPositioned(_tile), aligned = inside.IsAligned(_tile);
                guide = positioned && aligned ? ComponentSlot.Guide.Both
                    : positioned || aligned ? ComponentSlot.Guide.OneOf : ComponentSlot.Guide.Neither;
            }
            bool nowBoth = guide == ComponentSlot.Guide.Both && (_guided != inside || _shown != guide);
            Guide(inside, guide);
            if (nowBoth && _grab.interactorsSelecting.Count > 0)
                Haptics.Pulse(_grab.interactorsSelecting[0], _snapBuzzAmplitude, _snapBuzzSeconds);
        }

        private void Guide(ComponentSlot slot, ComponentSlot.Guide guide)
        {
            if (_guided != null && _guided != slot) _guided.ShowGuide(ComponentSlot.Guide.Hidden);
            _guided = slot;
            _shown = guide;
            if (slot != null) slot.ShowGuide(guide);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (_ignoresPlayer != null) _ignoresPlayer.enabled = true;   // carried into the player, not pushing them
            var slot = _slotted.Slot;
            if (slot != null) slot.TakeOut(_tile);
        }

        /// Puts it in the nearest slot that accepts it, or lets it fall.
        private void OnLetGo(SelectExitEventArgs args)
        {
            if (_grab.isSelected) return;
            if (_ignoresPlayer != null) _ignoresPlayer.enabled = false;
            Guide(null, ComponentSlot.Guide.Hidden);

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

        /// A held component swung into a loose one: that one's physics runs on whoever owns it, so ask for it.
        private void OnCollisionEnter(Collision collision)
        {
            if (!_grab.isSelected || collision.rigidbody == null) return;
            if (collision.rigidbody.TryGetComponent<CarriedComponent>(out var other) && other != this && other.Loose)
                Bumped?.Invoke(other);
        }
    }
}
