using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The S1 and S2 batteries' readout (battery.md → S1, S2): twenty cells, each a twentieth of the charge, lit in
    /// fill order. A cell is black while empty, violet from the moment it starts to fill, brightening to white when
    /// full, so the count of white cells is the charge in twentieths and the one violet cell is the twentieth in
    /// progress. Worked out here from the networked charge: nothing is sent.
    [RequireComponent(typeof(CapacitorModule))]
    public sealed class BatteryCells : MonoBehaviour
    {
        [Tooltip("The cells in fill order: the first lights first and goes dark last.")]
        [SerializeField] private Renderer[] _cells = new Renderer[0];
        [Tooltip("An empty cell's colour.")]
        [SerializeField] private Color _empty = new Color(0.01f, 0.01f, 0.012f);
        [Tooltip("A cell's colour as it starts to fill.")]
        [SerializeField] private Color _filling = new Color(0.45f, 0.12f, 1f);
        [Tooltip("A full cell's colour.")]
        [SerializeField] private Color _full = new Color(0.95f, 0.93f, 1f);
        [Tooltip("Glow strength as a cell starts to fill, and when full.")]
        [SerializeField] private Vector2 _glow = new Vector2(0.6f, 2.2f);

        private static readonly int BaseId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private CapacitorModule _hold;
        private MaterialPropertyBlock _block;
        private float _shown = -1f;

        private void Awake()
        {
            _hold = GetComponent<CapacitorModule>();
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            float charge = (float)_hold.Capacitor.Fraction;
            if (Mathf.Abs(charge - _shown) < 0.0005f) return;
            _shown = charge;

            float steps = charge * _cells.Length;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == null) continue;
                float fill = Mathf.Clamp01(steps - i);
                Color colour = fill > 0f ? Color.Lerp(_filling, _full, fill) : _empty;
                Color glow = fill > 0f ? colour * Mathf.Lerp(_glow.x, _glow.y, fill) : Color.black;
                _cells[i].GetPropertyBlock(_block);
                _block.SetColor(BaseId, colour);
                _block.SetColor(EmissionId, glow);
                _cells[i].SetPropertyBlock(_block);
            }
        }
    }
}
