using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A battery's hum (battery.md → S3, S5): a looping drone, the machine's presence. Silent below Fade From; from
    /// there it grows louder and climbs in pitch with the charge (on the S5, on the curve the ring's bunches speed up
    /// on, BatteryRing, so the sound and the light are one machine spinning up). Under strain, from Strain From through
    /// any overcharge to the vent (OverchargeVentModule), it warbles, harder toward the top. Worked out here from the
    /// networked charge: nothing is sent.
    [RequireComponent(typeof(CapacitorModule))]
    public sealed class BatteryHum : MonoBehaviour
    {
        [Tooltip("The drone, a seamless loop.")]
        [SerializeField] private AudioClip _loop;
        [Tooltip("Where it sounds from, in the battery's own space (the ring's centre).")]
        [SerializeField] private Vector3 _at = new Vector3(0f, 0.12f, 0f);
        [Tooltip("Silent below this charge; the volume rises from nothing here to its full-charge volume at full. 0: it sounds as soon as there is any charge.")]
        [SerializeField] private float _fadeFrom;
        [Tooltip("Volume as it starts to sound, and at full.")]
        [SerializeField] private Vector2 _volume = new Vector2(0.15f, 0.8f);
        [Tooltip("Pitch as it starts to sound, and at full. Pitch is speed too: a pulsing loop pulses slower when lower.")]
        [SerializeField] private Vector2 _pitch = new Vector2(0.8f, 1.2f);
        [Tooltip("Charge from which it strains.")]
        [SerializeField] private float _strainFrom = 0.95f;
        [Tooltip("At the vent: how far the pitch wobbles, and how many times a second.")]
        [SerializeField] private float _warble = 0.06f;
        [SerializeField] private float _warbleRate = 7f;
        [Tooltip("At the vent: how much louder it is than at full.")]
        [SerializeField] private float _strainGain = 0.2f;
        [Tooltip("Metres within which it is at full volume, and beyond which it is gone.")]
        [SerializeField] private Vector2 _distance = new Vector2(3f, 25f);

        private CapacitorModule _hold;
        private OverchargeVentModule _vent;
        private AudioSource _source;

        private void Awake()
        {
            _hold = GetComponent<CapacitorModule>();
            TryGetComponent(out _vent);
            if (_loop == null) return;
            var go = new GameObject("Hum");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = _at;
            _source = go.AddComponent<AudioSource>();
            _source.clip = _loop;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = _distance.x;
            _source.maxDistance = _distance.y;
            _source.volume = 0f;
            // A random start into the loop, so two batteries side by side are not in step.
            _source.time = Random.value * _loop.length;
        }

        private void Update()
        {
            if (_source == null) return;
            float level = _vent != null ? _vent.Level : (float)_hold.Capacitor.Fraction;
            if (level <= 0.001f || level < _fadeFrom)
            {
                if (_source.isPlaying) _source.Stop();
                return;
            }
            if (!_source.isPlaying) _source.Play();

            float charge = Mathf.Clamp01(level);
            float strain = Mathf.InverseLerp(_strainFrom, _vent != null ? _vent.VentsAt : 1f, level);
            float wobble = Mathf.Sin(Time.time * _warbleRate * Mathf.PI * 2f) + 0.5f * Mathf.Sin(Time.time * _warbleRate * 2.3f);
            float rise = _fadeFrom > 0f ? Mathf.InverseLerp(_fadeFrom, 1f, charge) : charge;
            _source.volume = Mathf.Lerp(_volume.x, _volume.y, rise) * (1f + _strainGain * strain);
            _source.pitch = Mathf.Lerp(_pitch.x, _pitch.y, rise) + _warble * strain * wobble;
        }
    }
}
