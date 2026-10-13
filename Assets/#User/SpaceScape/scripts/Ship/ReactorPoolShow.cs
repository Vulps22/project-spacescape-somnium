using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The S3 reactor's readout on its model (reactor.md → S3: The Pool). All of it is worked out here from the
    /// networked reactor state and the tile's temperature: nothing is sent. The rod floating between the blades is
    /// ReactorChamberRods'.
    ///
    /// - The water level is the coolant: the drum's litres as a share of a full drum.
    /// - The blades rise with the rods, out of the water, and drop back in on a SCRAM, with a splash as they hit it.
    ///   Their cables shorten and lengthen to the motors on the bridge.
    /// - Bubbles rise off the rod, more with output, and a rolling boil once the reactor is above its working
    ///   temperature.
    [RequireComponent(typeof(ReactorBehaviourModule))]
    public sealed class ReactorPoolShow : MonoBehaviour
    {
        [Header("Water")]
        [Tooltip("The water: its origin at the tank's floor, scaled up and down with the coolant left.")]
        [SerializeField] private Transform _water;
        [Tooltip("The water's height when the drum is full, from the tank's floor, in metres.")]
        [SerializeField] private float _fullDepth = 1.85f;

        [Header("Blades")]
        [Tooltip("The control blades: their origin at their tops, fully in.")]
        [SerializeField] private Transform _blades;
        [Tooltip("How far the blades rise fully out, in metres.")]
        [SerializeField] private float _travel = 1.42f;
        [Tooltip("The blades' length, top to bottom, in metres.")]
        [SerializeField] private float _bladeLength = 1.05f;
        [Tooltip("The cables from the blades' tops to the motors: their origin at the motors, scaled to reach the blades.")]
        [SerializeField] private Transform _cables;
        [Tooltip("How far below the motors the blades' tops hang fully in, in metres.")]
        [SerializeField] private float _cableLength = 1.83f;

        [Header("Bubbles")]
        [Tooltip("Particles for the bubbles and the splash: a soft round sprite.")]
        [SerializeField] private Material _bubbleMaterial;
        [Tooltip("Where the bubbles rise from: the rod's centre, in the reactor's own space.")]
        [SerializeField] private Vector3 _rodAt = new Vector3(-0.25f, -0.6f, -0.25f);
        [Tooltip("Bubbles a second at full output.")]
        [SerializeField] private float _bubblesPerSecond = 40f;
        [Tooltip("How many times more bubbles, and how much bigger, in a rolling boil.")]
        [SerializeField] private float _boil = 4f;
        [Tooltip("Bubbles' rising speed, in metres a second.")]
        [SerializeField] private float _rise = 0.45f;
        [Tooltip("The bubbles' tint.")]
        [SerializeField] private Color _bubbleColour = new Color(0.8f, 0.92f, 1f, 0.6f);

        private ReactorBehaviourModule _reactor;
        private int _waterUp, _cablesUp;   // the local axis of each that points up the reactor
        private Vector3 _waterScale, _cablesScale, _bladesHome, _bladesUp;
        private float _floor;              // the tank's floor, in the reactor's own space
        private float _lastBottom = float.NaN;
        private ParticleSystem _bubbles, _splash;

        private void Start()
        {
            _reactor = GetComponent<ReactorBehaviourModule>();
            if (_water != null)
            {
                _waterScale = _water.localScale;
                _waterUp = UpAxis(_water);
                _floor = transform.InverseTransformPoint(_water.position).y;
            }
            if (_cables != null) { _cablesScale = _cables.localScale; _cablesUp = UpAxis(_cables); }
            if (_blades != null)
            {
                _bladesHome = _blades.localPosition;
                _bladesUp = _blades.parent != null ? _blades.parent.InverseTransformDirection(transform.up) : Vector3.up;
            }
            if (_bubbleMaterial != null)
            {
                _bubbles = MakeParticles("Bubbles", _rodAt, 0.012f, 0.03f);
                _splash = MakeParticles("Splash", _rodAt, 0.02f, 0.05f);
                var shape = _bubbles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(0.35f, 1f, 0.35f);
                var splash = _splash.shape;
                splash.shapeType = ParticleSystemShapeType.Circle;
                splash.rotation = new Vector3(-90f, 0f, 0f);
                splash.radius = 0.5f;
            }
        }

        /// Which of a transform's own axes points most nearly up the reactor.
        private int UpAxis(Transform t)
        {
            var up = t.InverseTransformDirection(transform.up);
            float x = Mathf.Abs(up.x), y = Mathf.Abs(up.y), z = Mathf.Abs(up.z);
            return x >= y && x >= z ? 0 : y >= z ? 1 : 2;
        }

        private ParticleSystem MakeParticles(string name, Vector3 at, float size, float sizeHigh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(size, sizeHigh);
            main.startSpeed = 0f;
            main.startColor = _bubbleColour;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            velocity.y = new ParticleSystem.MinMaxCurve(_rise * 0.8f, _rise * 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = _bubbleMaterial;
            ps.Play();
            return ps;
        }

        private void Update()
        {
            if (_reactor == null) return;
            var r = _reactor.Reactor;
            var core = r.Core;

            // The water level is the coolant left.
            float full = (float)System.Math.Max(1.0, _reactor.FullCoolantLitres);
            float level = Mathf.Clamp01((float)(r.Coolant.Litres / full));
            float surface = _floor + _fullDepth * level;
            if (_water != null)
            {
                var s = _waterScale;
                s[_waterUp] = _waterScale[_waterUp] * Mathf.Max(0.001f, level);
                _water.localScale = s;
            }

            // The blades rise with the rods; the cables reach them from the motors.
            float lift = _travel * (float)core.Withdrawal;
            if (_blades != null) _blades.localPosition = _bladesHome + _bladesUp * lift;
            if (_cables != null)
            {
                var s = _cablesScale;
                s[_cablesUp] = _cablesScale[_cablesUp] * Mathf.Max(0.01f, (_cableLength - lift) / _cableLength);
                _cables.localScale = s;
            }

            // A splash as the blades' bottoms hit the water coming down fast (a SCRAM).
            float bottom = (_blades != null ? transform.InverseTransformPoint(_blades.position).y : 0f) - _bladeLength;
            if (_splash != null && !float.IsNaN(_lastBottom) && _lastBottom > surface && bottom <= surface &&
                (_lastBottom - bottom) / Mathf.Max(0.0001f, Time.deltaTime) > 0.3f)
            {
                _splash.transform.localPosition = new Vector3(_rodAt.x, surface, _rodAt.z);
                _splash.Emit(40);
            }
            _lastBottom = bottom;

            // Bubbles off the rod, more with output; a rolling boil once above the working temperature.
            if (_bubbles != null)
            {
                float output = core.MaxOutput > 0.0 ? Mathf.Clamp01((float)(core.WattsProduced / core.MaxOutput)) : 0f;
                var node = _reactor.Tile != null ? _reactor.Tile.Node : null;
                bool boiling = node != null && node.Celsius > core.WorkingCelsius;
                bool under = surface > _rodAt.y;
                var emission = _bubbles.emission;
                emission.rateOverTime = under ? _bubblesPerSecond * output * (boiling ? _boil : 1f) : 0f;
                var main = _bubbles.main;
                main.startLifetime = Mathf.Max(0.05f, (surface - _rodAt.y) / _rise);   // gone at the surface
                main.startSizeMultiplier = boiling ? _boil * 0.5f : 1f;
            }
        }
    }
}
