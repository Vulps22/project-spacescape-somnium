using SomniumSpace.Worlds.SpaceScape.Networking;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Arcs on a charged battery (battery.md → S3, S5): a short-lived jagged line that flickers for a few frames, and
    /// on the S5 a crack. Purely a show: an arc hurts nobody and changes nothing.
    ///
    /// How often: none below Charge From; from there Per Minute At Start, climbing exponentially to Per Minute At Top
    /// at Top At (or Per Minute At Top throughout, with Top At unset). The charge is the battery's level against its
    /// rated charge where it can be overcharged (OverchargeVentModule: up to 110%), else its share of its hold. Every
    /// Step seconds of the shared clock one roll decides whether an arc fires, drawn from a hash of the battery's place
    /// on the ship and the step, so every client rolls the same numbers and a room of players sees the same arc at the
    /// same moment with nothing sent. Where: from one of the start points (or from the surface round Start Around), to
    /// the nearest end point at least Shortest away (or, with none, to the glass wall round the battery's centre line).
    ///
    /// Strikes: when an overchargeable battery vents, one huge bolt leaps from a strike start (the S5's lower front)
    /// onto a strike end (its guard rail), right in front of whoever is standing there, forking onto the rail like
    /// lightning, with its own heavy sound. It is drawn from the vent's number, so every client draws the same one.
    /// Still to the grounded frame, so still harmless.
    [RequireComponent(typeof(CapacitorModule))]
    public sealed class BatteryArcs : MonoBehaviour
    {
        [Header("When")]
        [Tooltip("No arcs below this charge (1 = full; an overchargeable battery goes on to 1.1).")]
        [SerializeField] private float _chargeFrom = 0.95f;
        [Tooltip("Arcs a minute at Charge From.")]
        [Range(0f, 120f)] [SerializeField] private float _perMinuteAtStart = 6f;
        [Tooltip("Arcs a minute at Top At, and beyond.")]
        [Range(0f, 120f)] [SerializeField] private float _perMinuteAtTop = 120f;
        [Tooltip("The charge where the rate reaches Per Minute At Top. At or below Charge From, the rate is Per Minute At Top throughout.")]
        [SerializeField] private float _topAt = 1.1f;
        [Tooltip("Seconds of the shared clock per roll.")]
        [SerializeField] private float _step = 0.1f;

        [Header("Where (in the battery's own space)")]
        [Tooltip("Where arcs leave from. Left empty, they leave the surface of Start Around.")]
        [SerializeField] private Vector3[] _starts = new Vector3[0];
        [Tooltip("A moving part arcs leave the surface of, when there are no start points.")]
        [SerializeField] private Transform _startAround;
        [SerializeField] private float _startRadius = 0.42f;
        [Tooltip("Where arcs strike. Left empty, they strike the glass wall: Wall Radius from the centre line.")]
        [SerializeField] private Vector3[] _ends = new Vector3[0];
        [Tooltip("The shortest arc to a strike point, so none jumps a hand's width.")]
        [SerializeField] private float _shortest = 0.8f;
        [SerializeField] private float _wallRadius = 0.6f;

        [Header("Look")]
        [SerializeField] private Material _material;
        [SerializeField] private float _width = 0.03f;
        [Tooltip("Seconds an arc stays.")]
        [SerializeField] private float _lifetime = 0.15f;
        [Tooltip("How far a kink can stray from the straight line, as a share of the arc's length.")]
        [SerializeField] private float _jag = 0.12f;
        [SerializeField] private int _kinks = 8;

        [Header("Strikes (on a vent)")]
        [Tooltip("Where strikes leave from.")]
        [SerializeField] private Vector3[] _strikeStarts = new Vector3[0];
        [Tooltip("Where strikes land: one is picked, and each fork lands on another.")]
        [SerializeField] private Vector3[] _strikeEnds = new Vector3[0];
        [SerializeField] private float _strikeWidth = 0.14f;
        [SerializeField] private float _strikeLifetime = 1.2f;
        [SerializeField] private float _strikeJag = 0.18f;
        [Tooltip("Forks off the main bolt, each to another strike end.")]
        [SerializeField] private int _forks = 6;
        [Tooltip("A strike's own sound, heavier than the crack. Left empty, strikes use the crack.")]
        [SerializeField] private AudioClip _strikeSound;
        [SerializeField] private float _strikeVolume = 1f;
        [SerializeField] private float _strikePitch = 1f;

        [Header("Sound")]
        [Tooltip("The crack. Optional.")]
        [SerializeField] private AudioClip _crack;
        [SerializeField] private float _volume = 0.6f;

        private const int Pool = 8;

        private CapacitorModule _hold;
        private OverchargeVentModule _vent;
        private LineRenderer[] _lines;
        private Vector3[] _from, _to;
        private float[] _until, _jagOf;
        private Vector3[] _points;
        private AudioSource _audio;
        private long _rolled = long.MinValue;

        private void Awake()
        {
            _hold = GetComponent<CapacitorModule>();
            TryGetComponent(out _vent);
            _lines = new LineRenderer[Pool];
            _from = new Vector3[Pool];
            _to = new Vector3[Pool];
            _until = new float[Pool];
            _jagOf = new float[Pool];
            _points = new Vector3[_kinks + 1];
            for (int i = 0; i < Pool; i++)
            {
                var line = new GameObject("Arc").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.useWorldSpace = false;
                line.positionCount = _kinks + 1;
                line.widthMultiplier = _width;
                line.sharedMaterial = _material;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                _lines[i] = line;
            }
            if (_crack == null && _strikeSound == null) return;
            _audio = new GameObject("Crack").AddComponent<AudioSource>();
            _audio.transform.SetParent(transform, false);
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
        }

        private void OnEnable()
        {
            if (_vent != null) _vent.Vented += OnVented;
        }

        private void OnDisable()
        {
            if (_vent != null) _vent.Vented -= OnVented;
        }

        private void OnVented(int vent) => Strike(Hash(Seed(), (uint)vent ^ 0xA5A5A5A5u));

        private float Level => _vent != null ? _vent.Level : (float)_hold.Capacitor.Fraction;

        // Arcs a minute at this charge: none below Charge From, then climbing exponentially to the top rate.
        private float PerMinute(float level)
        {
            if (level <= 0f || level < _chargeFrom) return 0f;
            if (_topAt <= _chargeFrom || _perMinuteAtStart <= 0f) return _perMinuteAtTop;
            float t = Mathf.Clamp01((level - _chargeFrom) / (_topAt - _chargeFrom));
            return _perMinuteAtStart * Mathf.Pow(_perMinuteAtTop / _perMinuteAtStart, t);
        }

        private void Update()
        {
            Roll();
            for (int i = 0; i < Pool; i++)
            {
                if (!_lines[i].enabled) continue;
                if (Time.time >= _until[i]) { _lines[i].enabled = false; continue; }
                Jag(i);
            }
        }

        // Every step of the shared clock since the last roll, at most a few after a stall, so none is missed or doubled.
        private void Roll()
        {
            long step = (long)System.Math.Floor(WorldManager.SharedTime / _step);
            if (_rolled == long.MinValue || step - _rolled > 5) _rolled = step - 1;
            float chance = PerMinute(Level) / 60f * _step;
            for (long s = _rolled + 1; s <= step; s++)
            {
                uint hash = Hash(Seed(), (uint)s);
                if (hash / (float)uint.MaxValue < chance) Fire(hash);
            }
            _rolled = step;
        }

        private int FreeLine()
        {
            for (int i = 0; i < Pool; i++) if (!_lines[i].enabled) return i;
            return -1;
        }

        // A free line, or else the one closest to going out: a strike is never skipped for ordinary arcs.
        private int TakeLine()
        {
            int free = FreeLine();
            if (free >= 0) return free;
            int soonest = 0;
            for (int i = 1; i < Pool; i++) if (_until[i] < _until[soonest]) soonest = i;
            return soonest;
        }

        private void Show(int slot, Vector3 from, Vector3 to, float width, float lifetime, float jag)
        {
            _from[slot] = from;
            _to[slot] = to;
            _until[slot] = Time.time + lifetime;
            _jagOf[slot] = jag;
            _lines[slot].widthMultiplier = width;
            _lines[slot].enabled = true;
            Jag(slot);
        }

        private void Crack(AudioClip clip, Vector3 at, float volume, float pitch)
        {
            if (_audio == null || clip == null) return;
            _audio.transform.localPosition = at;
            _audio.pitch = pitch;
            _audio.PlayOneShot(clip, volume);
        }

        private void Fire(uint hash)
        {
            int slot = FreeLine();
            if (slot < 0) return;

            uint a = Hash(hash, 1u), b = Hash(hash, 2u), c = Hash(hash, 3u);
            Vector3 from;
            if (_starts.Length > 0) from = _starts[(int)(a % (uint)_starts.Length)];
            else if (_startAround != null)
                from = transform.InverseTransformPoint(_startAround.position) + Direction(a, b) * _startRadius;
            else return;

            Vector3 to;
            if (_ends.Length > 0)
            {
                to = from;
                float best = float.MaxValue;
                foreach (var end in _ends)
                {
                    float d = (end - from).sqrMagnitude;
                    if (d >= _shortest * _shortest && d < best) { best = d; to = end; }
                }
                if (best == float.MaxValue) return;
            }
            else
            {
                float angle = c / (float)uint.MaxValue * Mathf.PI * 2f;
                to = new Vector3(Mathf.Cos(angle) * _wallRadius, from.y + (b / (float)uint.MaxValue - 0.5f) * 0.3f,
                    Mathf.Sin(angle) * _wallRadius);
            }

            Show(slot, from, to, _width, _lifetime, _jag);
            Crack(_crack, (from + to) * 0.5f, _volume, 1f);
        }

        // The vent: one huge bolt onto the rail, and forks off it onto other rail points.
        private void Strike(uint hash)
        {
            if (_strikeStarts.Length == 0 || _strikeEnds.Length == 0) return;
            int slot = TakeLine();
            Vector3 from = _strikeStarts[(int)(Hash(hash, 5u) % (uint)_strikeStarts.Length)];
            Vector3 to = _strikeEnds[(int)(Hash(hash, 6u) % (uint)_strikeEnds.Length)];
            Show(slot, from, to, _strikeWidth, _strikeLifetime, _strikeJag);
            for (int f = 0; f < _forks; f++)
            {
                int fork = TakeLine();
                if (fork == slot) break;
                float t = 0.35f + 0.4f * (Hash(hash, 10u + (uint)f) / (float)uint.MaxValue);
                Vector3 end = _strikeEnds[(int)(Hash(hash, 20u + (uint)f) % (uint)_strikeEnds.Length)];
                Show(fork, Vector3.Lerp(from, to, t), end, _strikeWidth * 0.5f, _strikeLifetime * 0.8f, _strikeJag);
            }
            Crack(_strikeSound != null ? _strikeSound : _crack, to, _strikeVolume, _strikePitch);
        }

        // A fresh jagged path each frame, so it flickers. Only the shape is local; when and where are shared.
        private void Jag(int i)
        {
            Vector3 from = _from[i], to = _to[i];
            float stray = (to - from).magnitude * _jagOf[i];
            for (int k = 0; k <= _kinks; k++)
            {
                float t = k / (float)_kinks;
                float pinch = Mathf.Sin(t * Mathf.PI);              // the ends stay put
                _points[k] = Vector3.Lerp(from, to, t) + Random.insideUnitSphere * stray * pinch;
            }
            _lines[i].SetPositions(_points);
        }

        // The battery's place on the ship, so each battery rolls its own numbers and every client the same ones.
        private uint Seed()
        {
            var p = Vector3Int.RoundToInt(transform.position * 2f);
            return (uint)(p.x * 73856093) ^ (uint)(p.y * 19349663) ^ (uint)(p.z * 83492791);
        }

        private static Vector3 Direction(uint a, uint b)
        {
            float z = a / (float)uint.MaxValue * 2f - 1f;
            float angle = b / (float)uint.MaxValue * Mathf.PI * 2f;
            float r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(angle), z, r * Mathf.Sin(angle));
        }

        private static uint Hash(uint a, uint b)
        {
            uint h = a * 0x9E3779B1u ^ b * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }
    }
}
