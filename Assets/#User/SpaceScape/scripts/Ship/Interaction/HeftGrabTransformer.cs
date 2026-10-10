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
    ///
    /// With two hands it keeps its pose in the frame the hands make between them: centred on them, pointing from
    /// the first to the second, up the way both hands lean. Pushing one hand and pulling the other turns it,
    /// raising one tilts it, twisting both rolls it. Whenever a hand takes hold or lets go, the pose is taken
    /// afresh from where it is, so it never jumps.
    public sealed class HeftGrabTransformer : XRBaseGrabTransformer
    {
        private Vector3 _localPosition;     // this object's position in the hands' frame, at the last grab or let go
        private Quaternion _localRotation;  // and its rotation
        private int _hands;                 // how many hands it was last held by

        /// How far below the hand's hold it hangs, in metres.
        public float Sag { get; set; }

        protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

        public override void OnGrab(XRGrabInteractable grabInteractable)
        {
            base.OnGrab(grabInteractable);
            _hands = grabInteractable.interactorsSelecting.Count;
            Hold(grabInteractable, false);
        }

        public override void OnGrabCountChanged(XRGrabInteractable grabInteractable, Pose targetPose, Vector3 localScale)
        {
            base.OnGrabCountChanged(grabInteractable, targetPose, localScale);
            int hands = grabInteractable.interactorsSelecting.Count;
            if (hands == _hands) return;   // called once for each list it is registered in
            _hands = hands;
            Hold(grabInteractable, true);
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose, ref Vector3 localScale)
        {
            if (!Frame(grabInteractable, out var frame)) return;
            targetPose.position = frame.position + frame.rotation * _localPosition + Vector3.down * Sag;
            targetPose.rotation = frame.rotation * _localRotation;
        }

        /// Takes its pose as it is now in the hands' frame. Already in a hand, it hangs Sag below its hold, which is
        /// taken back out so it does not sag again.
        private void Hold(XRGrabInteractable grab, bool sagging)
        {
            if (!Frame(grab, out var frame)) return;
            var inverse = Quaternion.Inverse(frame.rotation);
            Vector3 position = transform.position + (sagging ? Vector3.up * Sag : Vector3.zero);
            _localPosition = inverse * (position - frame.position);
            _localRotation = inverse * transform.rotation;
        }

        /// The frame of the hands holding it: one hand's attach point, or for two the frame between them. False
        /// with no hand.
        private static bool Frame(XRGrabInteractable grab, out Pose frame)
        {
            frame = Pose.identity;
            if (grab == null || grab.interactorsSelecting.Count == 0) return false;
            var first = grab.interactorsSelecting[0].GetAttachTransform(grab);
            if (first == null) return false;
            frame = new Pose(first.position, first.rotation);
            if (grab.interactorsSelecting.Count < 2) return true;

            var second = grab.interactorsSelecting[1].GetAttachTransform(grab);
            if (second == null) return true;
            Vector3 across = second.position - first.position;
            if (across.sqrMagnitude < 1e-6f) return true;   // hands together: as one hand
            Vector3 up = first.up + second.up;
            if (Vector3.Cross(across, up).sqrMagnitude < 1e-6f) up = first.forward + second.forward;
            frame = new Pose((first.position + second.position) * 0.5f, Quaternion.LookRotation(across, up));
            return true;
        }
    }
}
