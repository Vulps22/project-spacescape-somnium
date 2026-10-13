using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Keeps a grabbed object on a short straight track: from where it rests, out along Axis (its parent's up unless
    /// set) no further than Travel, and back no further than Travel Down, and never turned. It follows the hand that holds it itself, from where the hand
    /// took hold, so it needs no other grab transformer before it.
    ///
    /// XRGrabInteractable unparents whatever it grabs until it is let go, so the track is the parent it had at
    /// Awake, not whatever transform.parent is while held.
    public sealed class SlideGrabTransformer : XRBaseGrabTransformer
    {
        [Tooltip("The direction it slides out, in its parent's space: up for a lid, a drawer's front for a drawer.")]
        [SerializeField] private Vector3 _axis = Vector3.up;
        [Tooltip("How far it can be pulled out along Axis, in metres.")]
        [SerializeField] private float _travel = 0.1f;
        [Tooltip("How far it can be pushed back along Axis, in metres. 0: not at all.")]
        [SerializeField] private float _travelDown;

        private Transform _track;      // the parent it slides along, kept while XRI has it unparented
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private bool _captured;
        private Vector3 _handOffset;   // from the hand's attach point to this object, in the track's space, at grab

        /// Where it rests, in its parent's space.
        public Vector3 RestPosition => _restPosition;

        /// How far it can be pulled.
        public float Travel => _travel;

        /// How far it can be pushed.
        public float TravelDown => _travelDown;

        /// How far out it is now: 0 at rest, up to Travel pulled, down to -Travel Down pushed.
        public float Pulled => _track == null ? 0f : Vector3.Dot(_track.InverseTransformPoint(transform.position) - _restPosition, Axis);

        private Vector3 Axis => _axis.sqrMagnitude > 0f ? _axis.normalized : Vector3.up;

        private void Awake() => Capture();

        private void Capture()
        {
            if (_captured) return;
            _captured = true;
            _track = transform.parent;
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
        }

        public override void OnGrab(XRGrabInteractable grabInteractable)
        {
            base.OnGrab(grabInteractable);
            var hand = Hand(grabInteractable);
            _handOffset = hand != null && _track != null
                ? _track.InverseTransformPoint(transform.position) - _track.InverseTransformPoint(hand.position)
                : Vector3.zero;
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose, ref Vector3 localScale)
        {
            var hand = Hand(grabInteractable);
            if (_track == null || hand == null) return;

            Vector3 local = _track.InverseTransformPoint(hand.position) + _handOffset;
            float out_ = Mathf.Clamp(Vector3.Dot(local - _restPosition, Axis), -_travelDown, _travel);
            targetPose.position = _track.TransformPoint(_restPosition + Axis * out_);
            targetPose.rotation = _track.rotation * _restRotation;
        }

        /// The attach point of the first hand holding it, or null.
        private static Transform Hand(XRGrabInteractable grab)
        {
            if (grab == null || grab.interactorsSelecting.Count == 0) return null;
            return grab.interactorsSelecting[0].GetAttachTransform(grab);
        }
    }
}
