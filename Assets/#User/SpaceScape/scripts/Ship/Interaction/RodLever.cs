using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A heavy lever that sets a reactor's dial. Grabbed, it drags after the hand through a damped
    /// spring and buzzes the hand while it moves; let go, it settles wherever the dial is.
    [SelectionBase]
    public sealed class RodLever : MonoBehaviour
    {
        [Tooltip("The reactor whose dial this lever sets. Found automatically in a parent.")]
        [SerializeField] private ReactorBehaviourModule _reactor;
        [Tooltip("The part a hand grabs. Found automatically in a child.")]
        [SerializeField] private XRBaseInteractable _handle;
        [Tooltip("The part that rotates.")]
        [SerializeField] private Transform _pivot;

        [Header("Travel")]
        [Tooltip("Lever angle, in degrees, for rods fully in: no power.")]
        [SerializeField] private float _insertedAngle = -40f;
        [Tooltip("Lever angle, in degrees, for rods fully out: full power.")]
        [SerializeField] private float _withdrawnAngle = 40f;

        [Header("Weight")]
        [Tooltip("Roughly how long, in seconds, the lever takes to catch up with the hand. Bigger feels heavier.")]
        [SerializeField] private float _smoothTime = 0.35f;
        [Tooltip("Fastest the lever can ever move, however hard it is pulled.")]
        [SerializeField] private float _maxDegreesPerSecond = 60f;

        [Header("Haptics")]
        [Tooltip("Strongest buzz sent to the hand, from 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float _maxAmplitude = 0.5f;
        [Tooltip("Length of each buzz pulse, in seconds. Pulses repeat while the lever moves.")]
        [SerializeField] private float _pulseSeconds = 0.05f;
        [Tooltip("Lever speed, in degrees per second, that gives the strongest buzz. Slower buzzes proportionally less.")]
        [SerializeField] private float _speedForFullBuzz = 45f;
        [Tooltip("Below this speed the lever counts as still and does not buzz.")]
        [SerializeField] private float _stillBelowDegreesPerSecond = 1f;

        private IXRSelectInteractor _grabber;
        private float _angle;
        private float _velocity;
        private float _nextPulse;

        private void OnEnable()
        {
            if (_handle == null) return;
            _handle.selectEntered.AddListener(OnGrabbed);
            _handle.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            if (_handle == null) return;
            _handle.selectEntered.RemoveListener(OnGrabbed);
            _handle.selectExited.RemoveListener(OnReleased);
            _grabber = null;
        }

        private void Start()
        {
            _angle = DialAngle();
            Apply();
        }

        private void OnGrabbed(SelectEnterEventArgs args) => _grabber = args.interactorObject;

        private void OnReleased(SelectExitEventArgs args)
        {
            if (args.interactorObject == _grabber) _grabber = null;
        }

        private void Update()
        {
            float target = _grabber != null ? HandAngle() : DialAngle();
            _angle = Mathf.SmoothDamp(_angle, target, ref _velocity, _smoothTime, _maxDegreesPerSecond, Time.deltaTime);
            Apply();

            if (_grabber == null) return;
            if (_reactor != null) _reactor.TargetWithdrawal = Mathf.InverseLerp(_insertedAngle, _withdrawnAngle, _angle);
            Buzz();
        }

        /// The angle the grabbing hand is pulling toward, around the pivot, within the lever's travel.
        private float HandAngle()
        {
            var attach = _grabber.GetAttachTransform(_handle);
            var frame = _pivot.parent != null ? _pivot.parent : transform;
            Vector3 local = frame.InverseTransformPoint(attach.position) - _pivot.localPosition;
            float angle = Mathf.Atan2(local.z, local.y) * Mathf.Rad2Deg;
            return Mathf.Clamp(angle, Mathf.Min(_insertedAngle, _withdrawnAngle), Mathf.Max(_insertedAngle, _withdrawnAngle));
        }

        /// The angle that matches the reactor's dial as it stands.
        private float DialAngle() =>
            _reactor != null ? Mathf.Lerp(_insertedAngle, _withdrawnAngle, _reactor.TargetWithdrawal) : _insertedAngle;

        /// Pulses the grabbing hand, harder the faster the lever is moving, and not at all while it is still.
        private void Buzz()
        {
            float speed = Mathf.Abs(_velocity);
            if (speed < _stillBelowDegreesPerSecond || Time.time < _nextPulse) return;
            _nextPulse = Time.time + _pulseSeconds * 0.8f;

            Haptics.Pulse(_grabber, _maxAmplitude * Mathf.Clamp01(speed / _speedForFullBuzz), _pulseSeconds);
        }

        private void Apply()
        {
            if (_pivot != null) _pivot.localRotation = Quaternion.Euler(_angle, 0f, 0f);
        }

        private void OnValidate()
        {
            if (_reactor == null) _reactor = GetComponentInParent<ReactorBehaviourModule>();
            if (_handle == null) _handle = GetComponentInChildren<XRBaseInteractable>();
        }
    }
}
