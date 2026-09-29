using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Keeps a grabbed object on a short straight track: from where it rests, up along its parent's up,
    /// no further than Travel, and never turned. It follows the hand that holds it itself, from where the hand
    /// took hold, so it needs no other grab transformer before it.
    ///
    /// XRGrabInteractable unparents whatever it grabs until it is let go, so the track is the parent it had at
    /// Awake, not whatever transform.parent is while held.
    public sealed class SlideGrabTransformer : XRBaseGrabTransformer
    {
        [Tooltip("How far it can be pulled up along its parent's up, in metres.")]
        [SerializeField] private float _travel = 0.1f;

        private Transform _track;      // the parent it slides along, kept while XRI has it unparented
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private bool _captured;
        private Vector3 _handOffset;   // from the hand's attach point to this object, in the track's space, at grab

        /// Where it rests, in its parent's space.
        public Vector3 RestPosition => _restPosition;

        /// How far it can be pulled.
        public float Travel => _travel;

        /// How far out it is now, 0 at rest to Travel.
        public float Pulled => _track == null ? 0f : _track.InverseTransformPoint(transform.position).y - _restPosition.y;

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
            float out_ = Mathf.Clamp(local.y - _restPosition.y, 0f, _travel);
            targetPose.position = _track.TransformPoint(new Vector3(_restPosition.x, _restPosition.y + out_, _restPosition.z));
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
