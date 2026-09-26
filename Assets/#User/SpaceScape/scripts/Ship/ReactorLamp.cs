using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The lamp on a reactor's rim. Dark with no power, amber once the control system has something
    /// in it, green and turning once the core is actually making watts — and it turns faster the
    /// harder the core is working, so output is readable from across the room with no numbers.
    public sealed class ReactorLamp : MonoBehaviour
    {
        [Tooltip("The reactor this lamp watches. Found automatically in a parent.")]
        [SerializeField] private ReactorBehaviourModule _reactor;
        [Tooltip("The light that shines. Found automatically in a child.")]
        [SerializeField] private Light _light;
        [Tooltip("The bulb mesh tinted to match. Found automatically on the light.")]
        [SerializeField] private Renderer _bulb;

        [Header("Colours")]
        [Tooltip("Colour while the magnets hold charge but the core makes nothing.")]
        [SerializeField] private Color _accumulating = new Color(1f, 0.62f, 0.1f);
        [Tooltip("Colour while the core is making power.")]
        [SerializeField] private Color _producing = new Color(0.25f, 1f, 0.4f);
        [Tooltip("Brightness of the light when on.")]
        [SerializeField] private float _intensity = 3f;

        [Header("Spin")]
        [Tooltip("How fast the lamp spins at full output. It spins proportionally slower below that.")]
        [SerializeField] private float _maxDegreesPerSecond = 220f;

        private MaterialPropertyBlock _block;

        private void Awake()
        {
            if (_reactor == null) _reactor = GetComponentInParent<ReactorBehaviourModule>();
            if (_light == null) _light = GetComponentInChildren<Light>();
        }

        private void Update()
        {
            if (_reactor == null) return;

            var reactor = _reactor.Reactor;
            double output = reactor.Core.WattsProduced;
            double max = reactor.Core.MaxOutput;
            bool anyPower = reactor.Control != null && reactor.Control.Charge > 0.0;

            if (output > 0.0)
            {
                Show(_producing);
                float load = max > 0.0 ? Mathf.Clamp01((float)(output / max)) : 0f;
                transform.Rotate(Vector3.right, _maxDegreesPerSecond * load * Time.deltaTime, Space.Self);
            }
            else if (anyPower)
            {
                Show(_accumulating);
            }
            else
            {
                Hide();
            }
        }

        /// Lights the bulb in a colour and tints the bulb itself so it reads when off-screen edge-on.
        private void Show(Color colour)
        {
            if (_light != null)
            {
                _light.enabled = true;
                _light.color = colour;
                _light.intensity = _intensity;
            }
            Tint(colour);
        }

        private void Hide()
        {
            if (_light != null) _light.enabled = false;
            Tint(Color.black);
        }

        /// Uses a property block so every reactor does not end up with its own material instance.
        private void Tint(Color colour)
        {
            if (_bulb == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            _bulb.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", colour);
            _block.SetColor("_EmissionColor", colour);
            _bulb.SetPropertyBlock(_block);
        }

        private void OnValidate()
        {
            if (_reactor == null) _reactor = GetComponentInParent<ReactorBehaviourModule>();
            if (_light == null) _light = GetComponentInChildren<Light>();
            if (_bulb == null && _light != null) _bulb = _light.GetComponent<Renderer>();
        }
    }
}
