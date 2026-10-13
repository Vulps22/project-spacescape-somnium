using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The S1 RTG's readout on its model (reactor.md → S1: RTG); it has no screen. The fins glow from dull to
    /// orange-red with heat, and the strip on the cartridge's face shows the rod's fuel, lit from the viewer's right.
    /// Worked out here from the networked reactor state and the tile's temperature: nothing is sent.
    [RequireComponent(typeof(ReactorBehaviourModule))]
    public sealed class ReactorRtgShow : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [Header("Fins")]
        [Tooltip("The fins, glowing with heat.")]
        [SerializeField] private Renderer _fins;
        [Tooltip("The fins' glow cold.")]
        [SerializeField, ColorUsage(false, true)] private Color _cold = new Color(0.05f, 0.02f, 0.01f);
        [Tooltip("The fins' glow at the working temperature.")]
        [SerializeField, ColorUsage(false, true)] private Color _hot = new Color(1.6f, 0.45f, 0.08f);
        [Tooltip("The fins' glow well past it.")]
        [SerializeField, ColorUsage(false, true)] private Color _overheated = new Color(2.6f, 0.2f, 0.05f);
        [Tooltip("Degrees past the working temperature at which the fins are fully Overheated.")]
        [SerializeField] private float _overheat = 150f;

        [Header("Fuel strip")]
        [Tooltip("The strip's segments, from the viewer's right: lit one by one as the rod's fuel allows.")]
        [SerializeField] private Renderer[] _strip = new Renderer[0];
        [Tooltip("A lit segment's glow.")]
        [SerializeField, ColorUsage(false, true)] private Color _lit = new Color(0.3f, 0.7f, 1.8f);
        [Tooltip("An unlit segment's glow.")]
        [SerializeField, ColorUsage(false, true)] private Color _unlit = new Color(0.01f, 0.02f, 0.03f);

        private ReactorBehaviourModule _reactor;
        private MaterialPropertyBlock _block;

        private void Start()
        {
            _reactor = GetComponent<ReactorBehaviourModule>();
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (_reactor == null) return;
            var core = _reactor.Reactor.Core;

            if (_fins != null)
            {
                var node = _reactor.Tile != null ? _reactor.Tile.Node : null;
                Color glow = _cold;
                if (node != null)
                {
                    double ambient = PowerGraph.AmbientCelsius;
                    float warm = (float)((node.Celsius - ambient) / System.Math.Max(1.0, core.WorkingCelsius - ambient));
                    float over = Mathf.Clamp01((float)(node.Celsius - core.WorkingCelsius) / Mathf.Max(1f, _overheat));
                    glow = warm <= 1f ? Color.Lerp(_cold, _hot, Mathf.Clamp01(warm)) : Color.Lerp(_hot, _overheated, over);
                }
                Emit(_fins, glow);
            }

            var chamber = core.ChamberCount > 0 ? core.Chambers[0] : null;
            float fuel = chamber != null && chamber.HasRod ? (float)chamber.Fuel : 0f;
            int lit = Mathf.CeilToInt(fuel * _strip.Length - 0.0001f);
            for (int i = 0; i < _strip.Length; i++)
                if (_strip[i] != null) Emit(_strip[i], i < lit ? _lit : _unlit);
        }

        private void Emit(Renderer renderer, Color glow)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, glow);
            renderer.SetPropertyBlock(_block);
        }
    }
}
