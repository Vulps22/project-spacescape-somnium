using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A rocker a hand presses. The raised pad follows the hand in, snaps over once pushed past the
    /// snap point, and springs back if the hand leaves first. Faces this transform's forward.
    [SelectionBase]
    public sealed class RockerSwitch : MonoBehaviour
    {
        private const int MaxHands = 8;

        [Tooltip("The switch tile this rocker throws. Found automatically when placed under it.")]
        [SerializeField] private SwitchConduitBehaviourModule _switch;
        [Tooltip("The part that tilts.")]
        [SerializeField] private Transform _paddle;
        [Tooltip("Ticked: pressing the top pad in closes the switch. Unticked: the bottom pad does.")]
        [SerializeField] private bool _topInMeansClosed = true;

        [Header("Shape")]
        [Tooltip("Half the pad's width, in metres.")]
        [SerializeField] private float _halfWidth = 0.05f;
        [Tooltip("Distance from the pivot to one end of the pad, in metres.")]
        [SerializeField] private float _halfHeight = 0.09f;
        [Tooltip("How far each end tips in or out at rest, in degrees.")]
        [SerializeField] private float _tiltDegrees = 12f;

        [Header("Feel")]
        [Tooltip("How far through its travel a push must go before it snaps over. 0.5 is the midpoint; higher needs a deeper push.")]
        [SerializeField, Range(0.05f, 0.95f)] private float _snapAt = 0.5f;
        [Tooltip("How big a hand is treated as, in metres. Bigger touches the pad sooner.")]
        [SerializeField] private float _handRadius = 0.04f;
        [Tooltip("How far behind the pad a hand still counts, in metres. Stops a hand deep in the wall pressing it.")]
        [SerializeField] private float _reach = 0.08f;
        [Tooltip("How fast it springs back when let go before snapping, in degrees per second.")]
        [SerializeField] private float _returnDegreesPerSecond = 120f;

        private readonly Vector3[] _hands = new Vector3[MaxHands];
        private readonly bool[] _wasInFront = new bool[MaxHands];
        private readonly bool[] _engaged = new bool[MaxHands];
        private readonly bool[] _latched = new bool[MaxHands];
        private bool _topIn;
        private float _angle;

        /// True while the top pad is pushed in.
        public bool TopIn => _topIn;

        private float RestAngle => _topIn ? -_tiltDegrees : _tiltDegrees;

        private float OtherRestAngle => _topIn ? _tiltDegrees : -_tiltDegrees;

        private void Start()
        {
            if (_switch != null) _topIn = _switch.Closed == _topInMeansClosed;
            _angle = RestAngle;
            Apply();
        }

        private void Update()
        {
            int count = PlayerHands.Positions(_hands);
            float push = 0f;
            bool anyEngaged = false;

            for (int i = 0; i < MaxHands; i++)
            {
                if (i >= count) { _wasInFront[i] = _engaged[i] = _latched[i] = false; continue; }
                float progress = Press(i, transform.InverseTransformPoint(_hands[i]));
                if (!_engaged[i]) continue;
                anyEngaged = true;
                if (progress > push) push = progress;
            }

            if (push >= _snapAt) Snap();
            else if (anyEngaged) _angle = Mathf.Lerp(RestAngle, OtherRestAngle, push);
            else
            {
                FollowSwitch();
                _angle = Mathf.MoveTowards(_angle, RestAngle, _returnDegreesPerSecond * Time.deltaTime);
            }

            Apply();
        }

        /// Works out whether one hand is pressing the raised pad, and how far through the travel it has pushed it.
        private float Press(int i, Vector3 local)
        {
            float side = _topIn ? -1f : 1f;
            float along = local.y * side;
            bool overPad = Mathf.Abs(local.x) <= _halfWidth + _handRadius
                && along >= _halfHeight * 0.15f && along <= _halfHeight + _handRadius
                && local.z >= -_reach;

            float surface = local.y * Mathf.Sin(_angle * Mathf.Deg2Rad);
            bool inFront = local.z - _handRadius >= surface - 1e-4f;
            bool wasInFront = _wasInFront[i];
            _wasInFront[i] = overPad && inFront;

            if (_latched[i])
            {
                if (!overPad || inFront) _latched[i] = false;
                _engaged[i] = false;
                return 0f;
            }

            if (!overPad || (inFront && !_engaged[i])) { _engaged[i] = false; return 0f; }
            if (!_engaged[i] && !wasInFront) return 0f;
            _engaged[i] = true;

            float sin = Mathf.Clamp((local.z - _handRadius) / local.y, -1f, 1f);
            float handAngle = Mathf.Asin(sin) * Mathf.Rad2Deg;
            float travel = OtherRestAngle - RestAngle;
            float progress = (handAngle - RestAngle) / travel;
            if (progress <= 0f) _engaged[i] = false;
            return Mathf.Clamp01(progress);
        }

        /// Throws the rocker to its other state and sets the switch, ignoring the pressing hands until they leave.
        private void Snap()
        {
            _topIn = !_topIn;
            _angle = RestAngle;
            for (int i = 0; i < MaxHands; i++)
            {
                if (_engaged[i]) _latched[i] = true;
                _engaged[i] = false;
            }
            if (_switch != null) _switch.Closed = _topIn == _topInMeansClosed;
        }

        /// Takes the switch's state when something other than a hand changed it.
        private void FollowSwitch()
        {
            if (_switch == null) return;
            bool wantTopIn = _switch.Closed == _topInMeansClosed;
            if (wantTopIn != _topIn) _topIn = wantTopIn;
        }

        private void Apply()
        {
            if (_paddle != null) _paddle.localRotation = Quaternion.Euler(_angle, 0f, 0f);
        }

        private void OnValidate()
        {
            if (_switch == null) _switch = GetComponentInParent<SwitchConduitBehaviourModule>();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_halfWidth * 2f, _halfHeight * 2f, 0.001f));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * _reach);
        }
    }
}
