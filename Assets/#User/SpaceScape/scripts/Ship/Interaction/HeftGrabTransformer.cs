using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Carries something heavy: it keeps the pose it had in the hand when the hand took hold, and hangs
    /// Sag metres lower than that, so it pulls down on the hand. XRI turns gravity off while an object is
    /// held, so the droop has to be in where it is steered to. The lag is the grab's velocity tracking
    /// (CarriedComponent sets how hard it follows); this only says where it is heading.
    public sealed class HeftGrabTransformer : XRBaseGrabTransformer
    {
        private Vector3 _localPosition;     // this object's position in the hand's attach space, at grab
        private Quaternion _localRotation;  // and its rotation

        /// How far below the hand's hold it hangs, in metres.
        public float Sag { get; set; }

        public override void OnGrab(XRGrabInteractable grabInteractable)
        {
            base.OnGrab(grabInteractable);
            var hand = Hand(grabInteractable);
            if (hand == null) return;
            _localPosition = hand.InverseTransformPoint(transform.position);
            _localRotation = Quaternion.Inverse(hand.rotation) * transform.rotation;
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose, ref Vector3 localScale)
        {
            var hand = Hand(grabInteractable);
            if (hand == null) return;
            targetPose.position = hand.TransformPoint(_localPosition) + Vector3.down * Sag;
            targetPose.rotation = hand.rotation * _localRotation;
        }

        /// The attach point of the first hand holding it, or null.
        private static Transform Hand(XRGrabInteractable grab)
        {
            if (grab == null || grab.interactorsSelecting.Count == 0) return null;
            return grab.interactorsSelecting[0].GetAttachTransform(grab);
        }
    }
}
