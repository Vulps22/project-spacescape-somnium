using UnityEngine;

namespace SpaceScape.Ship
{
    /// A lamp. Lit while it is working, as brightly as it is full, so the room is the readout.
    public sealed class LightBehaviourModule : LoadBehaviourModule
    {
        [SerializeField] private Light _light;
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
