using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Shows the fuel in a reactor's chambers through their glass, and the core working (reactor.md → Chambers). All
    /// of it is worked out here from the networked chamber state and the tile's temperature: nothing is sent.
    ///
    /// - In each chamber, the rod burning low down while it has one, and above it the one waiting in that chamber's
    ///   LoadingModule.
    /// - The burning rod glows by its own fuel (bright blue fresh, dim violet spent) and its own rods: nearly dark
    ///   fully in, bright fully out, pulsing harder and faster the further out. Every rod shifts toward amber-red as
    ///   the core nears its working temperature, and redder past it.
    /// - A spent rod drops out of the bottom of its chamber into the base housing, where it is hidden, as the
    ///   waiting rod slides down to take its place; with none waiting, the chamber is left empty.
    /// - The core column's bands brighten with output, and a blue light travels up and down inside the column, faster
    ///   the further the rods are out on average. Past Steady At it is too fast to see: it holds in the middle, solid,
    ///   bright and wide. Dark with no output.
    [RequireComponent(typeof(ReactorBehaviourModule))]
    public sealed class ReactorChamberRods : MonoBehaviour
    {
        [Tooltip("The fuel rod item, whose model is copied into each chamber.")]
        [SerializeField] private LoadableItem _rod;
        [Tooltip("Each chamber's centre line, in the reactor's own space, in chamber order: x and z of the chamber, y unused.")]
        [SerializeField] private Vector3[] _chambers = new Vector3[0];
        [Tooltip("Height of the burning rod's centre, in the reactor's own space.")]
        [SerializeField] private float _burningHeight = -0.2f;
        [Tooltip("Height of the waiting rod's centre, in the reactor's own space.")]
        [SerializeField] private float _waitingHeight = 0.95f;

        [Header("Glow")]
        [Tooltip("Glow of a fresh rod.")]
        [SerializeField, ColorUsage(false, true)] private Color _fresh = new Color(0.08f, 0.5f, 2.2f);
        [Tooltip("Glow of a spent rod.")]
        [SerializeField, ColorUsage(false, true)] private Color _spent = new Color(0.25f, 0.05f, 0.45f);
        [Tooltip("Share of its glow a rod keeps with its rods fully in.")]
        [SerializeField, Range(0f, 1f)] private float _idleGlow = 0.05f;
        [Tooltip("Glow of the waiting rod, which is not burning.")]
        [SerializeField, ColorUsage(false, true)] private Color _waiting = new Color(0.02f, 0.12f, 0.45f);
        [Tooltip("How far the glow swells and dips with the pulse, fully out: 0.35 is 35%.")]
        [SerializeField, Range(0f, 1f)] private float _pulse = 0.35f;
        [Tooltip("Pulses a second with the rods barely out, and fully out.")]
        [SerializeField] private Vector2 _pulsesPerSecond = new Vector2(0.4f, 3f);

        [Header("Heat")]
        [Tooltip("The rods' colour at the core's working temperature.")]
        [SerializeField, ColorUsage(false, true)] private Color _hot = new Color(2.2f, 0.75f, 0.12f);
        [Tooltip("The rods' colour once the core is this far past its working temperature (Overheat).")]
        [SerializeField, ColorUsage(false, true)] private Color _overheated = new Color(2.6f, 0.12f, 0.05f);
        [Tooltip("How much of the way to Hot the rods go at the working temperature, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float _hotShare = 0.6f;
        [Tooltip("Degrees past the working temperature at which the rods are fully Overheated.")]
        [SerializeField] private float _overheat = 150f;

        [Header("Spent rod")]
        [Tooltip("How fast a spent rod falls, in metres a second a second.")]
        [SerializeField] private float _fall = 6f;
        [Tooltip("Height, in the reactor's own space, below which a falling rod is gone: inside the base housing.")]
        [SerializeField] private float _goneBelow = -1.6f;
        [Tooltip("Seconds the waiting rod takes to slide down into the burning place.")]
        [SerializeField] private float _slideSeconds = 0.8f;

        [Header("Core")]
        [Tooltip("The core column's renderer, whose glowing bands brighten with output.")]
        [SerializeField] private Renderer _core;
        [Tooltip("The bands' glow at full output.")]
        [SerializeField, ColorUsage(false, true)] private Color _coreGlow = new Color(0.25f, 0.9f, 2.5f);
        [Tooltip("Share of the bands' glow with nothing produced.")]
        [SerializeField, Range(0f, 1f)] private float _coreIdle = 0.1f;
        [Tooltip("The light inside the core column.")]
        [SerializeField] private Light _light;
        [Tooltip("The bottom and top of the light's travel, as heights in the reactor's own space.")]
        [SerializeField] private Vector2 _travel = new Vector2(-0.8f, 1.5f);
        [Tooltip("Trips up and down a second, with the rods barely out and just below Steady At.")]
        [SerializeField] private Vector2 _tripsPerSecond = new Vector2(0.15f, 2.5f);
        [Tooltip("Average rod position (0 in, 1 out) from which the light is too fast to see and holds in the middle.")]
        [SerializeField, Range(0.1f, 1f)] private float _steadyAt = 0.7f;
        [Tooltip("The light's intensity and range while travelling, at full output.")]
        [SerializeField] private Vector2 _travelling = new Vector2(4f, 3f);
        [Tooltip("The light's intensity and range held in the middle, at full output.")]
        [SerializeField] private Vector2 _steady = new Vector2(10f, 9f);

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private ReactorBehaviourModule _reactor;
        private Transform[] _burning;
        private Transform[] _waitingRods;
        private MaterialPropertyBlock _block;
        private float[] _phase;
        private float[] _lastFuel;
        private bool[] _hadRod;
        private float[] _slide;          // seconds left of the waiting rod's slide down, per chamber
        private readonly System.Collections.Generic.List<(Transform rod, float speed)> _falling =
            new System.Collections.Generic.List<(Transform, float)>();
        private float _corePhase;

        private void Start()
        {
            _reactor = GetComponent<ReactorBehaviourModule>();
            _block = new MaterialPropertyBlock();
            int count = _chambers.Length;
            _burning = new Transform[count];
            _waitingRods = new Transform[count];
            _phase = new float[count];
            _lastFuel = new float[count];
            _hadRod = new bool[count];
            _slide = new float[count];
            for (int i = 0; i < count; i++)
            {
                _burning[i] = Copy($"Chamber {i + 1} Rod", Burning(i));
                _waitingRods[i] = Copy($"Chamber {i + 1} Waiting Rod", Waiting(i));
                _phase[i] = i * 1.7f;
                _lastFuel[i] = -1f;
            }
        }

        private Vector3 Burning(int i) => new Vector3(_chambers[i].x, _burningHeight, _chambers[i].z);

        private Vector3 Waiting(int i) => new Vector3(_chambers[i].x, _waitingHeight, _chambers[i].z);

        /// A picture of the rod's model standing upright here: its meshes only, nothing to grab or collide with.
        private Transform Copy(string name, Vector3 at)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.localPosition = at;
            if (_rod != null)
                foreach (var source in _rod.GetComponentsInChildren<MeshFilter>(true))
                {
                    var part = new GameObject(source.name).transform;
                    part.SetParent(root, false);
                    part.localPosition = _rod.transform.InverseTransformPoint(source.transform.position);
                    part.localRotation = Quaternion.Inverse(_rod.transform.rotation) * source.transform.rotation;
                    part.localScale = source.transform.lossyScale;
                    part.gameObject.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                    part.gameObject.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<Renderer>().sharedMaterials;
                }
            root.gameObject.SetActive(false);
            return root;
        }

        private void Update()
        {
            if (_reactor == null || _burning == null) return;
            var core = _reactor.Reactor.Core;
            int count = Mathf.Min(_chambers.Length, core.ChamberCount);
            var fuel = _reactor.Fuel;
            var heat = HeatTint(core);

            for (int i = 0; i < count; i++)
            {
                var chamber = core.Chambers[i];
                float now = (float)chamber.Fuel;

                // A rod spent: it drops out of the bottom, and the next (if it came) slides down from waiting.
                bool refuelled = _lastFuel[i] >= 0f && now > _lastFuel[i] + 0.5f;
                bool emptied = _hadRod[i] && !chamber.HasRod;
                if (refuelled || emptied) Drop(i);
                if (refuelled) _slide[i] = _slideSeconds;
                _lastFuel[i] = now;
                _hadRod[i] = chamber.HasRod;

                _burning[i].gameObject.SetActive(chamber.HasRod);
                if (chamber.HasRod)
                {
                    if (_slide[i] > 0f) _slide[i] = Mathf.Max(0f, _slide[i] - Time.deltaTime);
                    float k = _slideSeconds > 0f ? Mathf.SmoothStep(0f, 1f, 1f - _slide[i] / _slideSeconds) : 1f;
                    _burning[i].localPosition = Vector3.Lerp(Waiting(i), Burning(i), k);

                    float load = (float)chamber.Load;
                    _phase[i] += Time.deltaTime * Mathf.Lerp(_pulsesPerSecond.x, _pulsesPerSecond.y, load) * Mathf.PI * 2f;
                    float pulse = 1f + _pulse * load * Mathf.Sin(_phase[i]);
                    var own = Color.Lerp(_spent, _fresh, now);
                    var glow = Color.Lerp(own, heat.colour, heat.share) * (Mathf.Lerp(_idleGlow, 1f, load) * pulse);
                    Glow(_burning[i], glow);
                }
                bool waiting = i < fuel.Count && fuel[i] != null && fuel[i].HasItem;
                _waitingRods[i].gameObject.SetActive(waiting);
                if (waiting) Glow(_waitingRods[i], _waiting);
            }

            Fall();
            Core(core);
        }

        /// The colour every rod leans toward with the core's temperature, and how far: none when cold, Hot Share of
        /// the way to Hot at the working temperature, then on toward Overheated past it.
        private (Color colour, float share) HeatTint(FissionCore core)
        {
            var node = _reactor.Tile != null ? _reactor.Tile.Node : null;
            if (node == null) return (_hot, 0f);
            double ambient = PowerGraph.AmbientCelsius;
            float warm = (float)((node.Celsius - ambient) / System.Math.Max(1.0, core.WorkingCelsius - ambient));
            if (warm <= 1f) return (_hot, Mathf.Clamp01(warm) * _hotShare);
            float over = Mathf.Clamp01((float)(node.Celsius - core.WorkingCelsius) / Mathf.Max(1f, _overheat));
            return (Color.Lerp(_hot, _overheated, over), Mathf.Lerp(_hotShare, 1f, over));
        }

        /// A copy of the spent rod falls from where it burned, dim, until it is inside the base housing.
        private void Drop(int i)
        {
            var spent = Copy($"Chamber {i + 1} Spent Rod", Burning(i));
            spent.gameObject.SetActive(true);
            Glow(spent, _spent * _idleGlow);
            _falling.Add((spent, 0f));
        }

        private void Fall()
        {
            for (int f = _falling.Count - 1; f >= 0; f--)
            {
                var (rod, speed) = _falling[f];
                if (rod == null) { _falling.RemoveAt(f); continue; }
                speed += _fall * Time.deltaTime;
                rod.localPosition += Vector3.down * (speed * Time.deltaTime);
                if (rod.localPosition.y < _goneBelow)
                {
                    _parts.Remove(rod);
                    Destroy(rod.gameObject);
                    _falling.RemoveAt(f);
                    continue;
                }
                _falling[f] = (rod, speed);
            }
        }

        /// The core column's bands by output, and its light: travelling up and down, faster the further the rods are
        /// out on average, until Steady At, from where it holds in the middle, solid and wide.
        private void Core(FissionCore core)
        {
            float output = core.MaxOutput > 0.0 ? Mathf.Clamp01((float)(core.WattsProduced / core.MaxOutput)) : 0f;
            if (_core != null)
            {
                _core.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, _coreGlow * Mathf.Lerp(_coreIdle, 1f, output));
                _core.SetPropertyBlock(_block);
            }
            if (_light == null) return;

            _light.enabled = output > 0f;
            if (output <= 0f) return;
            float rods = (float)core.Withdrawal;
            float middle = (_travel.x + _travel.y) * 0.5f;
            float steady = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_steadyAt - 0.1f, _steadyAt, rods));
            float trips = Mathf.Lerp(_tripsPerSecond.x, _tripsPerSecond.y, Mathf.Clamp01(rods / _steadyAt));
            _corePhase += Time.deltaTime * trips * Mathf.PI * 2f;
            float travel = (_travel.y - _travel.x) * 0.5f * (1f - steady);
            var at = _light.transform.localPosition;
            at.y = middle - Mathf.Cos(_corePhase) * travel;
            _light.transform.localPosition = at;
            var look = Vector2.Lerp(_travelling, _steady, steady);
            _light.intensity = look.x * output;
            _light.range = look.y;
        }

        private readonly System.Collections.Generic.Dictionary<Transform, Renderer[]> _parts =
            new System.Collections.Generic.Dictionary<Transform, Renderer[]>();

        private void Glow(Transform rod, Color glow)
        {
            if (!_parts.TryGetValue(rod, out var parts)) _parts[rod] = parts = rod.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in parts)
            {
                renderer.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, glow);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
