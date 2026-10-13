using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The S3 battery's readout (battery.md → S3): the sphere's height up the column is the charge, resting on the
    /// base when empty and at the top when full, and the plasma beam stretches from the base up to its underside,
    /// faint violet low down, whiter as it nears full. Worked out here from the networked charge: nothing is sent.
    [RequireComponent(typeof(CapacitorModule))]
    public sealed class BatteryLift : MonoBehaviour
    {
        [Tooltip("The sphere, exported resting on the base (empty), its origin at its centre.")]
        [SerializeField] private Transform _sphere;
        [Tooltip("The beam, exported one metre tall, its origin at its foot.")]
        [SerializeField] private Renderer _beam;
        [Tooltip("How far the sphere rises from empty to full, in metres.")]
        [SerializeField] private float _travel = 1.26f;
        [Tooltip("The beam's colour when nearly empty.")]
        [SerializeField] private Color _low = new Color(0.45f, 0.12f, 1f);
        [Tooltip("The beam's colour when full.")]
        [SerializeField] private Color _high = new Color(0.95f, 0.93f, 1f);
        [Tooltip("The beam's glow strength when nearly empty, and when full.")]
        [SerializeField] private Vector2 _glow = new Vector2(0.4f, 2f);

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private CapacitorModule _hold;
        private MaterialPropertyBlock _block;
        private Vector3 _rest;
        private float _shown = -1f;

        private void Awake()
        {
            _hold = GetComponent<CapacitorModule>();
            _block = new MaterialPropertyBlock();
            if (_sphere != null) _rest = _sphere.localPosition;
        }

        private void Update()
        {
            float charge = (float)_hold.Capacitor.Fraction;
            if (Mathf.Abs(charge - _shown) < 0.0005f) return;
            _shown = charge;

            float rise = charge * _travel;
            if (_sphere != null) _sphere.localPosition = _rest + Vector3.up * rise;
            if (_beam == null) return;

            // The sphere's underside sits on the beam's foot at rest, so the beam is as long as the rise.
            _beam.enabled = rise > 0.001f;
            var scale = _beam.transform.localScale;
            scale.y = Mathf.Max(rise, 0.001f);
            _beam.transform.localScale = scale;
            _beam.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, Color.Lerp(_low, _high, charge) * Mathf.Lerp(_glow.x, _glow.y, charge));
            _beam.SetPropertyBlock(_block);
        }
    }
}
