using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A rocker a hand pushes. Each half of the paddle is a solid box the size of that half. Every physics
    /// step, whatever part of the avatar is pushing into the raised half (fingers, palm, back of the hand,
    /// a fist) rocks the paddle in by exactly as far as it has gone in, on a rigid pivot. Pushed past the
    /// snap point it snaps over and throws the switch; let go first and it eases back. No joint and no
    /// spring, so nothing bounces or flexes. Faces this transform's forward.
    [SelectionBase]
    public sealed class RockerSwitch : MonoBehaviour
    {
        private const int MaxOverlaps = 16;

        [Tooltip("The switch addon this rocker throws. Found automatically on this object or above it.")]
        [SerializeField] private SwitchAddon _switch;
        [Tooltip("The part that tilts, about its X axis. Carries the two pads.")]
        [SerializeField] private Transform _paddle;
        [Tooltip("The solid box covering the top half of the paddle.")]
        [SerializeField] private BoxCollider _topPad;
        [Tooltip("The solid box covering the bottom half of the paddle.")]
        [SerializeField] private BoxCollider _bottomPad;
        [Tooltip("Ticked: pressing the top pad in closes the switch. Unticked: the bottom pad does.")]
        [SerializeField] private bool _topInMeansClosed = true;

        [Header("Feel")]
        [Tooltip("How far each end tips in or out at rest, in degrees.")]
        [SerializeField] private float _tiltDegrees = 12f;
        [Tooltip("How far through its travel a push must go before it snaps over. 0.5 is the midpoint; higher needs a deeper push.")]
        [SerializeField, Range(0.05f, 0.95f)] private float _snapAt = 0.5f;
        [Tooltip("How fast it eases back when let go before snapping, in degrees per second.")]
        [SerializeField] private float _returnDegreesPerSecond = 120f;
        [Tooltip("How square a push must be to count, 0 to 1: 1 is only straight into the face, 0 counts anything with any push inward at all. Kept low so a push at any natural angle works.")]
        [SerializeField, Range(0f, 1f)] private float _squareness = 0.05f;

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private bool _topIn;
        private float _angle;

        /// True while the top pad is pushed in.
        public bool TopIn => _topIn;

        private float RestAngle => _topIn ? -_tiltDegrees : _tiltDegrees;

        private float OtherRestAngle => _topIn ? _tiltDegrees : -_tiltDegrees;

        /// The half that stands proud, and so the one that can be pushed.
        private BoxCollider Raised => _topIn ? _bottomPad : _topPad;

        private void Start()
        {
            if (_switch != null) _topIn = _switch.Closed == _topInMeansClosed;
            _angle = RestAngle;
            Apply();
        }

        private void FixedUpdate()
        {
            FollowSwitch();
            float pushed = Push();

            if (pushed > 0f)
            {
                // Rock in by as far as the hand has gone in, never past the far stop.
                float toward = Mathf.Sign(OtherRestAngle - RestAngle);
                _angle = Mathf.Clamp(_angle + toward * pushed, -_tiltDegrees, _tiltDegrees);
                if ((_angle - RestAngle) / (OtherRestAngle - RestAngle) >= _snapAt) Snap();
            }
            else _angle = Mathf.MoveTowards(_angle, RestAngle, _returnDegreesPerSecond * Time.fixedDeltaTime);

            Apply();
        }

        /// How many degrees the avatar has pushed the raised half in by this step: the deepest push square
        /// into its face, turned into an angle at the point it touches.
        private float Push()
        {
            var pad = Raised;
            if (pad == null) return 0f;

            var t = pad.transform;
            Vector3 centre = t.TransformPoint(pad.center);
            Vector3 half = Vector3.Scale(pad.size, t.lossyScale) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(centre, half, _overlaps, t.rotation, ~0, QueryTriggerInteraction.Ignore);

            float deepest = 0f;
            Vector3 inward = -_paddle.forward;
            for (int i = 0; i < count; i++)
            {
                var other = _overlaps[i];
                if (other == _topPad || other == _bottomPad) continue;
                if (!PlayerHands.IsLocalPlayer(other)) continue;

                if (!Physics.ComputePenetration(pad, t.position, t.rotation, other, other.transform.position,
                        other.transform.rotation, out var direction, out var distance)) continue;

                // The way the pad must move to get clear: in, for a push square into its face.
                if (Vector3.Dot(direction, inward) < _squareness) continue;

                Vector3 touch = _paddle.InverseTransformPoint(other.ClosestPoint(centre));
                float arm = Mathf.Max(0.02f, Mathf.Abs(touch.y));
                float degrees = Mathf.Atan2(distance, arm) * Mathf.Rad2Deg;
                if (degrees > deepest) deepest = degrees;
            }
            return deepest;
        }

        /// Snaps over and throws the switch. The other half is free to push straight away.
        private void Snap()
        {
            _topIn = !_topIn;
            _angle = RestAngle;
            Apply();
            if (_switch != null) _switch.Press(_topIn == _topInMeansClosed);
        }

        /// Takes the switch's state when something other than a push changed it.
        private void FollowSwitch()
        {
            if (_switch == null) return;
            bool wantTopIn = _switch.Closed == _topInMeansClosed;
            if (wantTopIn == _topIn) return;
            _topIn = wantTopIn;
            _angle = RestAngle;
        }

        private void Apply()
        {
            if (_paddle != null) _paddle.localRotation = Quaternion.Euler(_angle, 0f, 0f);
        }

        private void OnValidate()
        {
            if (_switch == null) _switch = GetComponentInParent<SwitchAddon>();
        }
    }
}
