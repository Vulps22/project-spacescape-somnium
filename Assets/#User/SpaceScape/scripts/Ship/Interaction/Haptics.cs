using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Buzzes the controller behind an interactor, through the interactor if it can and its controller if not.
    public static class Haptics
    {
        /// Sends one pulse of the given strength (0 to 1) and length, in seconds.
        public static void Pulse(IXRInteractor interactor, float amplitude, float seconds)
        {
            if (interactor == null) return;
            if (interactor is XRBaseInputInteractor input && input.SendHapticImpulse(amplitude, seconds)) return;
#pragma warning disable CS0618
            var controller = (interactor as Component)?.GetComponentInParent<XRBaseController>();
            if (controller != null) controller.SendHapticImpulse(amplitude, seconds);
#pragma warning restore CS0618
        }
    }
}
