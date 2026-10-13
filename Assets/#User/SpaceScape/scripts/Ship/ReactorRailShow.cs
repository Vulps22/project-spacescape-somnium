using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The S2 reactor's readout on its model (reactor.md → S2: The Rail): the accelerator's coils, the beam and the
    /// target vessel. All of it is worked out here from the networked reactor state and the tile's temperature:
    /// nothing is sent. The rod in the target is ReactorChamberRods'.
    ///
    /// - The coils light gun to target as far as the beam current reaches, chasing toward the target, faster with
    ///   output. When the current falls (a SCRAM) they go dark one after another, gun to target.
    /// - The beam runs from the gun's muzzle to the target, thicker and brighter with current, and goes ragged and
    ///   flickers once the reactor is above its working temperature. No beam with no current.
    /// - The vessel's bands go from blue to amber-red as the reactor nears its working temperature, redder past it.
    /// - The gun hums while the reactor makes power, louder and higher with output, silent with none.
    [RequireComponent(typeof(ReactorBehaviourModule))]
    public sealed class ReactorRailShow : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [Header("Coils")]
        [Tooltip("The coils, gun to target.")]
        [SerializeField] private Renderer[] _coils = new Renderer[0];
        [Tooltip("A lit coil's glow at its brightest.")]
        [SerializeField, ColorUsage(false, true)] private Color _coilGlow = new Color(0.3f, 0.65f, 1.6f);
        [Tooltip("Chase waves a second along the coils, at no output and at full.")]
        [SerializeField] private Vector2 _chasesPerSecond = new Vector2(0.5f, 3f);
        [Tooltip("How much the chase dims a coil between waves, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float _chaseDepth = 0.6f;
        [Tooltip("Coils a second that go dark as the current falls, gun to target.")]
        [SerializeField] private float _drainPerSecond = 6f;

        [Header("Beam")]
        [Tooltip("The beam: its origin at the gun's muzzle, running to the target. Scaled across its length with current.")]
        [SerializeField] private Renderer _beam;
        [Tooltip("The beam's glow at full current.")]
        [SerializeField, ColorUsage(false, true)] private Color _beamGlow = new Color(1.5f, 1.8f, 2.4f);
        [Tooltip("The beam's thickness across, at the least current and at full, as a share of the model's.")]
        [SerializeField] private Vector2 _thickness = new Vector2(0.4f, 1.6f);
        [Tooltip("Flickers a second while running hot.")]
        [SerializeField] private float _flickersPerSecond = 14f;

        [Header("Vessel")]
        [Tooltip("The target vessel's bands, tinted with heat.")]
        [SerializeField] private Renderer _bands;
        [Tooltip("The bands' glow cold.")]
        [SerializeField, ColorUsage(false, true)] private Color _cold = new Color(0.2f, 0.5f, 1.2f);
        [Tooltip("The bands' glow at the working temperature.")]
        [SerializeField, ColorUsage(false, true)] private Color _hot = new Color(2.2f, 0.75f, 0.12f);
        [Tooltip("The bands' glow well past it.")]
        [SerializeField, ColorUsage(false, true)] private Color _overheated = new Color(2.6f, 0.12f, 0.05f);
        [Tooltip("Degrees past the working temperature at which the bands are fully Overheated.")]
        [SerializeField] private float _overheat = 150f;

        [Header("Sound")]
        [Tooltip("The gun's hum: a looping AudioSource at the muzzle, on the Environment layer. It follows the reactor's output alone.")]
        [SerializeField] private AudioSource _hum;
        [Tooltip("The hum's volume at the least output and at full.")]
        [SerializeField] private Vector2 _humVolume = new Vector2(0.2f, 1f);
        [Tooltip("The hum's pitch at the least output and at full.")]
        [SerializeField] private Vector2 _humPitch = new Vector2(0.85f, 1f);

        private ReactorBehaviourModule _reactor;
        private MaterialPropertyBlock _block;
        private Vector3 _beamScale;
        private int _beamAlong;  // the beam mesh's long axis, which keeps its length
        private float _lit;      // how many coils the current reaches, gun first
        private float _phase;

        private void Start()
        {
            _reactor = GetComponent<ReactorBehaviourModule>();
            _block = new MaterialPropertyBlock();
            if (_hum != null && _hum.clip != null)
            {
                _hum.loop = true;
                _hum.playOnAwake = false;
            }
            if (_beam == null) return;
            _beamScale = _beam.transform.localScale;
            var size = _beam.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            _beamAlong = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        }

        private void Update()
        {
            if (_reactor == null) return;
            var core = _reactor.Reactor.Core;
            float current = (float)core.Withdrawal;
            float output = core.MaxOutput > 0.0 ? Mathf.Clamp01((float)(core.WattsProduced / core.MaxOutput)) : 0f;
            float heat = Heat(core, out float over);

            // The current reaches along the coils at once as it rises; falling, they drain from the gun end.
            float reach = current > 0f ? _coils.Length : 0f;
            _lit = reach >= _lit ? reach : Mathf.Max(reach, _lit - _drainPerSecond * Time.deltaTime);
            _phase += Time.deltaTime * Mathf.Lerp(_chasesPerSecond.x, _chasesPerSecond.y, output) * Mathf.PI * 2f;
            int dark = _coils.Length - Mathf.CeilToInt(_lit);   // drained from the gun end
            for (int i = 0; i < _coils.Length; i++)
            {
                if (_coils[i] == null) continue;
                float glow = 0f;
                if (_lit > 0f && i >= dark)
                {
                    float wave = 0.5f + 0.5f * Mathf.Sin(_phase - i * 0.9f);
                    glow = Mathf.Lerp(0.25f, 1f, current) * (1f - _chaseDepth * wave);
                }
                Emit(_coils[i], _coilGlow * glow);
            }

            if (_beam != null)
            {
                bool on = current > 0f && output > 0f;
                if (_beam.enabled != on) _beam.enabled = on;
                if (on)
                {
                    float flicker = over > 0f ? 1f - over * 0.7f * Mathf.PerlinNoise(Time.time * _flickersPerSecond, 0.3f) : 1f;
                    float across = Mathf.Lerp(_thickness.x, _thickness.y, current) * Mathf.Lerp(1f, flicker, 0.5f);
                    var scale = _beamScale * across;
                    scale[_beamAlong] = _beamScale[_beamAlong];
                    _beam.transform.localScale = scale;
                    Emit(_beam, _beamGlow * (Mathf.Lerp(0.3f, 1f, current) * flicker));
                }
            }

            if (_hum != null)
            {
                if (output > 0f)
                {
                    _hum.volume = Mathf.Lerp(_humVolume.x, _humVolume.y, output);
                    _hum.pitch = Mathf.Lerp(_humPitch.x, _humPitch.y, output);
                    if (!_hum.isPlaying)
                    {
                        _hum.Play();
                        if (_hum.clip != null) _hum.time = Random.Range(0f, _hum.clip.length);   // two reactors never crackle in step
                    }
                }
                else if (_hum.isPlaying) _hum.Stop();
            }

            if (_bands != null)
                Emit(_bands, heat < 1f ? Color.Lerp(_cold, _hot, heat) : Color.Lerp(_hot, _overheated, over));
        }

        /// How warm the reactor is: 0 cold to 1 at its working temperature; over is how far past it, 0 to 1.
        private float Heat(FissionCore core, out float over)
        {
            over = 0f;
            var node = _reactor.Tile != null ? _reactor.Tile.Node : null;
            if (node == null) return 0f;
            double ambient = PowerGraph.AmbientCelsius;
            float warm = (float)((node.Celsius - ambient) / System.Math.Max(1.0, core.WorkingCelsius - ambient));
            over = Mathf.Clamp01((float)(node.Celsius - core.WorkingCelsius) / Mathf.Max(1f, _overheat));
            return Mathf.Clamp01(warm);
        }

        private void Emit(Renderer renderer, Color glow)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, glow);
            renderer.SetPropertyBlock(_block);
        }
    }
}
