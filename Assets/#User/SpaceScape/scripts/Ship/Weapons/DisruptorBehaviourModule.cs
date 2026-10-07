using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The disruptor, phase 1: a gun aimed by two levers (RodLevers, axis 0 the traverse, axis 1 the elevation)
    /// and fired by a button, all on its back face, facing into the ship. Its barrel turns up to 45° either way
    /// from straight out of the slot's front on each axis: the traverse lever at 0 aims 45° left, at 1 45°
    /// right; the elevation lever at 0 aims 45° down, at 1 45° up. Both at 0 is the bottom left corner of its
    /// field of fire.
    ///
    /// It is a load (LoadBehaviourModule) with Drain 0: it charges to full and holds. Firing a charged
    /// gun spends the whole charge (LoadBehaviour.Discharge); an uncharged one does nothing. A worn one can
    /// misfire as it fills (the load's own roll), which spends the charge with nothing ready.
    ///
    /// Its readout says Ready (charged), Charging (its hold is filling) or Not Charging.
    ///
    /// Networking (ComponentNetwork): the aim is this component's state, after the load's own, written by whoever decides (the
    /// component's state authority, or anyone with no network). A slider moved on another client moves here
    /// at once and is sent to the decider; the levers and barrel follow the aim everywhere else. Pressing fire
    /// on another client asks the decider, which fires and tells everyone (Fired) so the shot is seen by all.
    public sealed class DisruptorBehaviourModule : LoadBehaviourModule, ILeverDial
    {
        [Header("Barrel")]
        [Tooltip("Turns left and right about its up: the traverse.")]
        [SerializeField] private Transform _traverse;
        [Tooltip("Turns up and down about its right, under the traverse: the elevation.")]
        [SerializeField] private Transform _elevation;
        [Tooltip("The end of the barrel, pointing the way it fires (+Z).")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("Degrees the barrel can turn either side of straight out, on each axis.")]
        [SerializeField] private float _fieldOfFire = 45f;

        [Header("Controls")]
        [Tooltip("The fire button: a PushButton, pressed by pushing a hand into it or grabbing it. The two aiming levers are RodLevers that find this themselves.")]
        [SerializeField] private PushButton _fire;
        [Tooltip("Lit while it is charged and ready to fire. Optional.")]
        [SerializeField] private Renderer _readyLamp;
        [Tooltip("The lamp's material while ready.")]
        [SerializeField] private Material _readyMaterial;
        [Tooltip("The lamp's material while charging.")]
        [SerializeField] private Material _chargingMaterial;

        [Header("Readout")]
        [Tooltip("Text showing Ready, Charging or Not Charging. Optional.")]
        [SerializeField] private TMP_Text _readout;

        [Header("Shot")]
        [Tooltip("Drawn from the muzzle while a shot is showing.")]
        [SerializeField] private LineRenderer _beam;
        [Tooltip("How far the beam reaches, in metres.")]
        [SerializeField] private float _beamLength = 500f;
        [Tooltip("Seconds a shot shows for.")]
        [SerializeField] private float _beamSeconds = 0.15f;

        [Header("Sound")]
        [Tooltip("Played once it is charged: the part before Loop Start once, then the loop until it is fired. Must be Decompress On Load. Optional.")]
        [SerializeField] private AudioClip _chargedClip;
        [Tooltip("Where the loop starts in the charged sound, in seconds. Snapped to the nearest rising zero crossing so it does not click.")]
        [SerializeField] private float _loopStart = 1.05f;
        [Tooltip("How long the loop is, in seconds. Its end is snapped the same way.")]
        [SerializeField] private float _loopLength = 0.1f;
        [Tooltip("Played when it fires. Optional.")]
        [SerializeField] private AudioClip _fireClip;
        [Tooltip("Volume of its sounds, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [Tooltip("Distance, in metres, beyond which its sounds cannot be heard.")]
        [SerializeField] private float _soundMaxDistance = 25f;

        // The charged sound cut into its intro and loop, once per clip and settings, shared by every gun.
        private static readonly Dictionary<(AudioClip, float, float), (AudioClip intro, AudioClip loop)> Cuts =
            new Dictionary<(AudioClip, float, float), (AudioClip, AudioClip)>();

        private Readout _readoutShown;
        private string _readoutState;
        private readonly bool[] _held = new bool[2];
        private Vector2 _aim = new Vector2(0.5f, 0.5f);
        private bool _shownReady;
        private bool _lampKnown;
        private AudioSource _introSource;
        private AudioSource _loopSource;
        private AudioSource _fireSource;
        private AudioClip _intro;
        private AudioClip _loop;

        /// Where it is aimed, each 0 to 1: x the traverse (0 fully left), y the elevation (0 fully down).
        public Vector2 Aim => _aim;

        /// True where this client decides its aim and its shots. Set by the network; true with no network.
        public bool Decides { get; set; } = true;

        /// True while a hand here holds either lever, so a correction does not pull the aim out of it.
        public bool AimHeld => _held[0] || _held[1];

        /// Raised when a hand here moves the aim, for the network to send to whoever decides.
        public event Action<Vector2> AimMoved;

        /// Raised when the fire button is pressed on a client that does not decide, for the network to ask.
        public event Action FireRequested;

        /// Raised where it decides, when it fires, for the network to show the shot to everyone.
        public event Action Fired;

        private void Awake()
        {
            if (_beam != null) _beam.enabled = false;
            _introSource = MakeSource("Charged");
            _loopSource = MakeSource("Charged Loop");
            _loopSource.loop = true;
            _fireSource = MakeSource("Fire");
            if (_chargedClip != null) (_intro, _loop) = Cut(_chargedClip, _loopStart, _loopLength);
        }

        private AudioSource MakeSource(string name)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(_muzzle != null ? _muzzle : transform, false);
            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = _soundMaxDistance;
            source.volume = _volume;
            return source;
        }

        /// The charged sound's intro (start to the loop) and loop, each cut on a rising zero crossing so the loop
        /// joins its own start without a click. Public for the Editor's preview.
        public static (AudioClip intro, AudioClip loop) Cut(AudioClip clip, float loopStart, float loopLength)
        {
            var key = (clip, loopStart, loopLength);
            if (Cuts.TryGetValue(key, out var made)) return made;

            int channels = clip.channels;
            var data = new float[clip.samples * channels];
            if (!clip.GetData(data, 0))
            {
                Debug.LogWarning($"DisruptorBehaviourModule: could not read '{clip.name}'; set it to Decompress On Load");
                return Cuts[key] = (clip, null);
            }

            int start = RisingZero(data, channels, Mathf.RoundToInt(loopStart * clip.frequency));
            int end = RisingZero(data, channels, start + Mathf.Max(1, Mathf.RoundToInt(loopLength * clip.frequency)));
            start = Mathf.Clamp(start, 1, clip.samples - 2);
            end = Mathf.Clamp(end, start + 1, clip.samples - 1);

            var intro = AudioClip.Create(clip.name + " (intro)", start, channels, clip.frequency, false);
            var introData = new float[start * channels];
            System.Array.Copy(data, 0, introData, 0, introData.Length);
            intro.SetData(introData, 0);

            var loop = AudioClip.Create(clip.name + " (loop)", end - start, channels, clip.frequency, false);
            var loopData = new float[(end - start) * channels];
            System.Array.Copy(data, start * channels, loopData, 0, loopData.Length);
            loop.SetData(loopData, 0);

            return Cuts[key] = (intro, loop);
        }

        /// The nearest sample frame to the one given where the first channel crosses zero going up.
        private static int RisingZero(float[] data, int channels, int near)
        {
            int frames = data.Length / channels;
            near = Mathf.Clamp(near, 1, frames - 1);
            for (int offset = 0; offset < frames; offset++)
            {
                foreach (int f in new[] { near - offset, near + offset })
                    if (f >= 1 && f < frames && data[(f - 1) * channels] < 0f && data[f * channels] >= 0f) return f;
                if (near - offset < 1 && near + offset >= frames) break;
            }
            return near;
        }

        /// Charged: the intro once, then the loop from exactly where it ends, until it is fired.
        private void StartChargedSound()
        {
            if (_intro == null || _loop == null) return;
            double at = AudioSettings.dspTime + 0.05;
            _introSource.clip = _intro;
            _introSource.PlayScheduled(at);
            _loopSource.clip = _loop;
            _loopSource.PlayScheduled(at + _intro.samples / (double)_intro.frequency);
        }

        private void StopChargedSound()
        {
            if (_introSource != null) _introSource.Stop();
            if (_loopSource != null) _loopSource.Stop();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_fire != null) _fire.Pressed += OnFirePressed;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_fire != null) _fire.Pressed -= OnFirePressed;
        }

        private void Start()
        {
            Point();
            if (_readout == null) return;
            _readoutShown = new Readout(_readout, () => ReadoutState, ReadoutChanged);
            _readoutShown.Listen();
        }

        private void OnDestroy() => _readoutShown?.Stop();

        /// What the readout says: Ready once charged, Charging while its hold is filling, Not Charging otherwise.
        private string ReadoutState =>
            Working ? "Ready" : Hold != null && Hold.NetWatts > 0.0 ? "Charging" : "Not Charging";

        private bool ReadoutChanged()
        {
            string state = ReadoutState;
            if (state == _readoutState) return false;
            _readoutState = state;
            return true;
        }

        private void Update() => ShowReady();

        // Its two dials, for the levers: 0 the traverse, 1 the elevation.
        public float GetDial(int axis) => axis == 0 ? _aim.x : _aim.y;

        public void SetDialHeld(int axis, bool held)
        {
            if (axis == 0 || axis == 1) _held[axis] = held;
        }

        public void TurnDial(int axis, float value)
        {
            var aim = axis == 0 ? new Vector2(value, _aim.y) : new Vector2(_aim.x, value);
            aim = new Vector2(Mathf.Clamp01(aim.x), Mathf.Clamp01(aim.y));
            if (aim == _aim) return;
            _aim = aim;
            Point();
            AimMoved?.Invoke(_aim);
        }

        /// Sets the aim, for the decider taking a hand's aim from another client, or anyone taking the decider's.
        public void SetAim(Vector2 aim)
        {
            aim = new Vector2(Mathf.Clamp01(aim.x), Mathf.Clamp01(aim.y));
            if (aim == _aim) return;
            _aim = aim;
            Point();
        }

        /// Turns the barrel to the aim. The levers follow the aim themselves.
        private void Point()
        {
            if (_traverse != null) _traverse.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-_fieldOfFire, _fieldOfFire, _aim.x), 0f);
            if (_elevation != null) _elevation.localRotation = Quaternion.Euler(-Mathf.Lerp(-_fieldOfFire, _fieldOfFire, _aim.y), 0f, 0f);
        }

        private void OnFirePressed()
        {
            Debug.Log($"[SpaceScape] Disruptor '{name}' fire pressed: {(Working ? "charged" : $"not charged ({ChargeFraction:P0})")}, {(Decides ? "fires here" : "asks the master")}", this);
            if (Decides) Fire();
            else FireRequested?.Invoke();
        }

        /// Fires if it is charged: spends the whole charge, hits whatever is along the barrel, and shows the
        /// shot. Where it decides only. A test target hit is knocked out (TargetField; the master decides).
        public void Fire()
        {
            if (!Working) return;
            Load.Discharge();
            if (Along(out var hit) && TargetField.Current != null) TargetField.Current.Hit(TargetField.Current.IndexOf(hit.collider));
            if (Fired != null) Fired.Invoke();
            else ShowShot();
        }

        /// Shows a shot: the beam from the muzzle for a moment, and the sound. On every client.
        public void ShowShot()
        {
            StopChargedSound();
            if (_fireClip != null) _fireSource.PlayOneShot(_fireClip);
            if (_beam != null && _muzzle != null) StartCoroutine(Beam());
        }

        /// The first solid thing along the barrel, within the beam's reach.
        private bool Along(out RaycastHit hit)
        {
            hit = default;
            return _muzzle != null && Physics.Raycast(_muzzle.position, _muzzle.forward, out hit, _beamLength, ~0, QueryTriggerInteraction.Ignore);
        }

        // Drawn to whatever it hit, on each client as that client sees it, or to its full reach.
        private IEnumerator Beam()
        {
            _beam.useWorldSpace = true;
            _beam.positionCount = 2;
            _beam.SetPosition(0, _muzzle.position);
            _beam.SetPosition(1, Along(out var hit) ? hit.point : _muzzle.position + _muzzle.forward * _beamLength);
            _beam.enabled = true;
            yield return new WaitForSeconds(_beamSeconds);
            _beam.enabled = false;
        }

        /// Follows its charge: the lamp, and the charged sound starting when it is ready and stopping when it
        /// is not. Its charge is networked, so every client hears the same.
        private void ShowReady()
        {
            bool ready = Working;
            if (_lampKnown && ready == _shownReady) return;
            bool was = _lampKnown && _shownReady;
            _lampKnown = true;
            _shownReady = ready;
            var material = ready ? _readyMaterial : _chargingMaterial;
            if (_readyLamp != null && material != null) _readyLamp.sharedMaterial = material;
            if (ready && !was) StartChargedSound();
            else if (!ready) StopChargedSound();
        }

        // The load's own value first, then the aim.
        public override int StateCount => base.StateCount + 2;

        public override void WriteState(float[] to, int at)
        {
            base.WriteState(to, at);
            at += base.StateCount;
            to[at] = _aim.x;
            to[at + 1] = _aim.y;
        }

        public override void ReadState(float[] from, int at)
        {
            base.ReadState(from, at);
            at += base.StateCount;
            if (!AimHeld) SetAim(new Vector2(from[at], from[at + 1]));
        }
    }
}
