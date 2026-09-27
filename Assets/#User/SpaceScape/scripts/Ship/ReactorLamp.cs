using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The lamp on a reactor's rim. Dark with no power, amber once the control system has something
    /// in it, green and turning once the core is actually making watts, so output is readable from
    /// across the room with no numbers. It spins up to a top speed; past that it spins no faster, but
    /// its trail closes into a ring and the room's light settles from the moving bulb to a steady glow,
    /// as though it were going too fast to follow.
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
        [Tooltip("Fastest it spins, in turns per second. 3 keeps the moving light under three flashes a second.")]
        [SerializeField] private float _topTurnsPerSecond = 3f;
        [Tooltip("Share of full output at which it reaches top speed, 0 to 1. It eases up to it: lazy when the core is barely working. Past it, the trail closes into a ring instead.")]
        [SerializeField, Range(0.1f, 1f)] private float _spinUpTo = 0.6f;

        [Header("Trail")]
        [Tooltip("The streak behind the bulb. Found automatically on the bulb.")]
        [SerializeField] private TrailRenderer _trail;
        [Tooltip("Turns per second below which there is no trail at all.")]
        [SerializeField] private float _trailFromTurns = 1.5f;
        [Tooltip("How much of a full circle the trail covers when top speed is first reached, 0 to 1. It closes to a full ring at full output.")]
        [SerializeField, Range(0f, 1f)] private float _arcAtTopSpeed = 0.25f;

        [Header("Glow")]
        [Tooltip("A steady light at the lamp's centre that takes over from the moving bulb as the ring closes, so the room settles instead of wobbling. Optional.")]
        [SerializeField] private Light _glow;

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
                float load = max > 0.0 ? Mathf.Clamp01((float)(output / max)) : 0f;
                float rising = Mathf.Clamp01(load / _spinUpTo);
                float beyond = _spinUpTo < 1f ? Mathf.Clamp01((load - _spinUpTo) / (1f - _spinUpTo)) : 0f;
                float turns = _topTurnsPerSecond * rising * rising;

                transform.Rotate(Vector3.right, turns * 360f * Time.deltaTime, Space.Self);
                Show(_producing, 1f - beyond);
                Glow(_producing, beyond);
                Trail(_producing, turns, beyond);
            }
            else if (anyPower)
            {
                Show(_accumulating, 1f);
                Glow(_accumulating, 0f);
                Trail(_accumulating, 0f, 0f);
            }
            else
            {
                Hide();
            }
        }

        /// Lights the bulb in a colour and tints the bulb itself so it reads when off-screen edge-on. The
        /// share is how much of the room's light still comes from the moving bulb.
        private void Show(Color colour, float share)
        {
            if (_light != null)
            {
                _light.enabled = share > 0.001f;
                _light.color = colour;
                _light.intensity = _intensity * share;
            }
            Tint(colour);
        }

        private void Hide()
        {
            if (_light != null) _light.enabled = false;
            Glow(Color.black, 0f);
            Trail(Color.black, 0f, 0f);
            Tint(Color.black);
        }

        /// The steady light at the centre, at a share of full brightness.
        private void Glow(Color colour, float share)
        {
            if (_glow == null) return;
            _glow.enabled = share > 0.001f;
            _glow.color = colour;
            _glow.intensity = _intensity * share;
        }

        /// No trail below its starting speed. From there it grows to a short arc by top speed, then closes to
        /// a ring as output climbs past it. A trail's length is its lifetime times how fast the bulb moves, so
        /// an arc of a share of a turn lives that share divided by the turns per second.
        private void Trail(Color colour, float turns, float beyond)
        {
            if (_trail == null) return;
            if (turns < _trailFromTurns || turns <= 0f)
            {
                if (_trail.emitting) { _trail.emitting = false; _trail.Clear(); }
                return;
            }

            float span = Mathf.Max(0.001f, _topTurnsPerSecond - _trailFromTurns);
            float arc = beyond > 0f
                ? Mathf.Lerp(_arcAtTopSpeed, 0.98f, beyond)
                : _arcAtTopSpeed * Mathf.Clamp01((turns - _trailFromTurns) / span);
            _trail.emitting = true;
            _trail.time = Mathf.Max(0.01f, arc / turns);
            _trail.startColor = colour;
            _trail.endColor = new Color(colour.r, colour.g, colour.b, 0f);
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
            if (_trail == null && _bulb != null) _trail = _bulb.GetComponent<TrailRenderer>();
        }
    }
}
