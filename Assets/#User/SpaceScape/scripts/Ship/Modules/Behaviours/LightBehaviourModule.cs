using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A lamp. Lit while it is working, as brightly as it is full, so the room is the readout.
    public sealed class LightBehaviourModule : LoadBehaviourModule
    {
        [Tooltip("The light it drives. Found automatically on this object.")]
        [SerializeField] private Light _light;
        [Tooltip("Brightness when full. It dims as the hold drains and goes dark when empty.")]
        [SerializeField] private float _maxIntensity = 1.6f;

        private void Update()
        {
            if (_light == null) return;
            _light.intensity = Working ? _maxIntensity * (float)ChargeFraction : 0f;
        }

        protected override void OnValidate()
        {
            if (_light == null) _light = GetComponent<Light>();
            base.OnValidate();
        }
    }
}
