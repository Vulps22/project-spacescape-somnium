using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The S3 battery's plasma (battery.md → S3): a plasma ball laid along the column. Thin filaments reach from the
    /// beam on the column's centre line out to the inside of the glass, and where each touches the glass it walks up the
    /// tube, drifting round it, from the base to the sphere's underside, then starts again at the bottom. More of them
    /// the fuller it is, violet turning white; none when empty. A fresh jagged path every frame, so they flicker.
    /// Worked out here from the networked charge: only the look, so nothing is sent.
    [RequireComponent(typeof(CapacitorModule))]
    public sealed class BatteryFilaments : MonoBehaviour
    {
        [Tooltip("The sphere: filaments walk up to its underside.")]
        [SerializeField] private Transform _sphere;
        [SerializeField] private float _sphereRadius = 0.42f;
        [Tooltip("The base housing's top, where the walk starts, in the battery's own space.")]
        [SerializeField] private float _base = -0.9f;
        [Tooltip("The inside of the glass, from the centre line.")]
        [SerializeField] private float _wallRadius = 0.6f;
        [Tooltip("Filaments at full; fewer as it empties.")]
        [SerializeField] private int _atFull = 5;
        [Tooltip("Seconds for a filament to walk from the base to the sphere, slowest and fastest.")]
        [SerializeField] private Vector2 _walkSeconds = new Vector2(1.5f, 3.5f);
        [Tooltip("How far round the tube a filament drifts in a walk, in turns.")]
        [SerializeField] private float _drift = 0.3f;
        [Tooltip("How far below its glass end the filament leaves the beam, so it leans up the tube.")]
        [SerializeField] private float _lean = 0.15f;

        [Header("Look")]
        [SerializeField] private Material _material;
        [SerializeField] private float _width = 0.012f;
        [SerializeField] private int _kinks = 10;
        [Tooltip("How far a kink can stray from the straight line, as a share of the filament's length.")]
        [SerializeField] private float _jag = 0.15f;
        [SerializeField] private Color _low = new Color(0.55f, 0.25f, 1f);
        [SerializeField] private Color _high = new Color(0.95f, 0.93f, 1f);

        private CapacitorModule _hold;
        private LineRenderer[] _lines;
        private float[] _start, _seconds, _angle, _spin;
        private Vector3[] _points;

        private void Awake()
        {
            _hold = GetComponent<CapacitorModule>();
            _lines = new LineRenderer[_atFull];
            _start = new float[_atFull];
            _seconds = new float[_atFull];
            _angle = new float[_atFull];
            _spin = new float[_atFull];
            _points = new Vector3[_kinks + 1];
            for (int i = 0; i < _atFull; i++)
            {
                var line = new GameObject("Filament").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.useWorldSpace = false;
                line.positionCount = _kinks + 1;
                line.widthCurve = AnimationCurve.Linear(0f, 0.4f, 1f, 1f);     // thin at the beam, brighter on the glass
                line.widthMultiplier = _width;
                line.sharedMaterial = _material;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                _lines[i] = line;
                Begin(i, Time.time - Random.value * _walkSeconds.y);   // staggered, so they never walk in step
            }
        }

        private void Begin(int i, float at)
        {
            _start[i] = at;
            _seconds[i] = Random.Range(_walkSeconds.x, _walkSeconds.y);
            _angle[i] = Random.value * Mathf.PI * 2f;
            _spin[i] = (Random.value < 0.5f ? -1f : 1f) * _drift * Mathf.PI * 2f;
        }

        private void Update()
        {
            float charge = (float)_hold.Capacitor.Fraction;
            int count = charge > 0f ? Mathf.Clamp(Mathf.CeilToInt(charge * _atFull), 1, _atFull) : 0;
            float top = _sphere != null ? transform.InverseTransformPoint(_sphere.position).y - _sphereRadius : _base;
            Color colour = Color.Lerp(_low, _high, charge);
            for (int i = 0; i < _atFull; i++)
            {
                var line = _lines[i];
                bool on = i < count && top > _base + 0.05f;
                if (line.enabled != on) line.enabled = on;
                if (!on) continue;

                float t = (Time.time - _start[i]) / _seconds[i];
                if (t >= 1f) { Begin(i, Time.time); t = 0f; }
                float y = Mathf.Lerp(_base, top, t);
                float angle = _angle[i] + _spin[i] * t;
                var onGlass = new Vector3(Mathf.Cos(angle) * _wallRadius, y, Mathf.Sin(angle) * _wallRadius);
                var onBeam = new Vector3(0f, Mathf.Max(_base, y - _lean), 0f);
                Jag(onBeam, onGlass);
                line.SetPositions(_points);
                line.startColor = colour * 0.6f;
                line.endColor = colour;
            }
        }

        private void Jag(Vector3 from, Vector3 to)
        {
            float stray = (to - from).magnitude * _jag;
            for (int k = 0; k <= _kinks; k++)
            {
                float t = k / (float)_kinks;
                _points[k] = Vector3.Lerp(from, to, t) + Random.insideUnitSphere * stray * Mathf.Sin(t * Mathf.PI);
            }
        }
    }
}
