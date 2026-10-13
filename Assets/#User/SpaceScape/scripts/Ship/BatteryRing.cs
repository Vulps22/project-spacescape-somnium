using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The S5 battery's readout (battery.md → S5): bunches of light racing round the ring, one per twentieth of the
    /// charge, violet turning white. As it fills they lengthen, close up and race faster (BatteryHum's pitch climbs on
    /// the same curve), and at full they merge into one solid band. Under strain, from 95% through the overcharge to
    /// the vent at 110% (OverchargeVentModule), it shimmers harder and the magnet blocks' seams glow. The band's shader
    /// (SpaceScape/BatteryRing) draws the bunches; this sets its values from the networked charge and carries their
    /// travel on each frame.
    /// Nothing is sent.
    [RequireComponent(typeof(CapacitorModule))]
    public sealed class BatteryRing : MonoBehaviour
    {
        [Tooltip("The ring's inner band, drawn by SpaceScape/BatteryRing.")]
        [SerializeField] private Renderer _band;
        [Tooltip("The magnet blocks' seams, glowing faintly near full.")]
        [SerializeField] private Renderer _seams;
        [Tooltip("How many bunches at full: each is that share of the charge.")]
        [SerializeField] private int _bunches = 20;
        [Tooltip("A bunch's length round the ring, as a share of the whole ring, when it first appears.")]
        [SerializeField] private float _shortest = 0.015f;
        [Tooltip("The bunches' colour as the battery starts to fill, and when full.")]
        [SerializeField] private Color _low = new Color(0.45f, 0.12f, 1f);
        [SerializeField] private Color _high = new Color(0.95f, 0.93f, 1f);
        [Tooltip("The bunches' glow strength as the battery starts to fill, and when full.")]
        [SerializeField] private Vector2 _glow = new Vector2(1f, 2.4f);
        [Tooltip("The seams' glow at full.")]
        [SerializeField] private Color _seamGlow = new Color(0.35f, 0.3f, 0.6f);
        [Tooltip("Charge from which it strains: shimmering, the seams glowing, harder up to the vent.")]
        [SerializeField] private float _heldFrom = 0.95f;
        [Tooltip("Turns a second round the ring when it starts to fill, and at full.")]
        [SerializeField] private Vector2 _speed = new Vector2(0.12f, 0.9f);

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SegmentsId = Shader.PropertyToID("_Segments");
        private static readonly int LengthId = Shader.PropertyToID("_Length");
        private static readonly int ShimmerId = Shader.PropertyToID("_Shimmer");
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");

        private CapacitorModule _hold;
        private OverchargeVentModule _vent;
        private MaterialPropertyBlock _block;
        private float _phase;

        private void Awake()
        {
            _hold = GetComponent<CapacitorModule>();
            TryGetComponent(out _vent);
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            // Every frame: the bunches travel on at the charge's speed, carried here so a change of speed never jumps.
            float level = _vent != null ? _vent.Level : (float)_hold.Capacitor.Fraction;
            float charge = Mathf.Clamp01(level);

            // N bunches evenly spaced; each grows from its shortest to a full 1/N of the ring, so at full they touch.
            int count = charge > 0f ? Mathf.CeilToInt(charge * _bunches - 0.0001f) : 0;
            float arc = Mathf.Lerp(_shortest, 1f / _bunches, charge);
            float held = Mathf.InverseLerp(_heldFrom, _vent != null ? _vent.VentsAt : 1f, level);
            if (_band != null)
            {
                _band.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, Color.Lerp(_low, _high, charge) * Mathf.Lerp(_glow.x, _glow.y, charge));
                _block.SetFloat(SegmentsId, count);
                _block.SetFloat(LengthId, count > 0 ? Mathf.Clamp01(arc * count) : 0f);
                _block.SetFloat(ShimmerId, held);
                if (count > 0) _phase = Mathf.Repeat(_phase + Mathf.Lerp(_speed.x, _speed.y, charge) * Time.deltaTime, 1f);
                _block.SetFloat(PhaseId, _phase);
                _band.SetPropertyBlock(_block);
            }
            if (_seams != null)
            {
                _seams.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, _seamGlow * held);
                _seams.SetPropertyBlock(_block);
            }
        }
    }
}
