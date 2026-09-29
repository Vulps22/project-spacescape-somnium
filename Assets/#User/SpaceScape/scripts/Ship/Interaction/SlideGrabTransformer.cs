using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Keeps a grabbed object on a short straight track: from where it rests, out along its parent's forward,
    /// no further than Travel, and never turned. It follows the hand that holds it itself, from where the hand
    /// took hold, so it needs no other grab transformer before it.
    public sealed class SlideGrabTransformer : XRBaseGrabTransformer
    {
        [Tooltip("How far it can be pulled out along its parent's forward, in metres.")]
        [SerializeField] private float _travel = 0.1f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private bool _captured;
        private Vector3 _handOffset;   // from the hand's attach point to this object, in the parent's space, at grab

        /// Where it rests, in its parent's space.
        public Vector3 RestPosition => _restPosition;

        /// How far it can be pulled.
        public float Travel => _travel;

        /// How far out it is now, 0 at rest to Travel.
        public float Pulled => transform.parent == null ? 0f : transform.localPosition.z - _restPosition.z;

        private void Awake() => Capture();

        private void Capture()
        {
            if (_captured) return;
            _captured = true;
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
        }

        public override void OnGrab(XRGrabInteractable grabInteractable)
        {
            base.OnGrab(grabInteractable);
            var hand = Hand(grabInteractable);
            var parent = transform.parent;
            _handOffset = hand != null && parent != null
                ? parent.InverseTransformPoint(transform.position) - parent.InverseTransformPoint(hand.position)
                : Vector3.zero;
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose, ref Vector3 localScale)
        {
            var parent = transform.parent;
            var hand = Hand(grabInteractable);
            if (parent == null || hand == null) return;
            Capture();

            Vector3 local = parent.InverseTransformPoint(hand.position) + _handOffset;
            float out_ = Mathf.Clamp(local.z - _restPosition.z, 0f, _travel);
            targetPose.position = parent.TransformPoint(new Vector3(_restPosition.x, _restPosition.y, _restPosition.z + out_));
            targetPose.rotation = parent.rotation * _restRotation;
        }

        /// The attach point of the first hand holding it, or null.
        private static Transform Hand(XRGrabInteractable grab)
        {
            if (grab == null || grab.interactorsSelecting.Count == 0) return null;
            return grab.interactorsSelecting[0].GetAttachTransform(grab);
        }
    }
}
