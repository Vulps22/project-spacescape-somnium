using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The hologram of one conduit segment: a tip handle and a flow arrow, designed pointing up (+Y) from
    /// the conduit's centre. Turned to point at its face, or parked with its handle at the centre.
    public sealed class SegmentHologram : MonoBehaviour
    {
        [Tooltip("The handle at the segment's tip. Sits on the face, or at the centre while parked.")]
        [SerializeField] private Transform _tip;
        [Tooltip("The flow arrow. Points up (outward) for Out and is turned round for In; hidden while parked.")]
        [SerializeField] private Transform _arrow;
        [Tooltip("What a hand grabs to move the segment to another face, or into the middle to park it.")]
        [SerializeField] private XRBaseInteractable _tipGrab;
        [Tooltip("What a hand grabs and slides along the segment to set its flow In or Out.")]
        [SerializeField] private XRBaseInteractable _arrowGrab;
        [Tooltip("Distance from the conduit's centre to its face at scale 1, in metres.")]
        [SerializeField] private float _faceDistance = 0.5f;
        [Tooltip("How far out along the segment the arrow sits, in metres at scale 1.")]
        [SerializeField] private float _arrowAlong = 0.3f;
        [Tooltip("How far the arrow floats above the segment, off its axis, in metres at scale 1.")]
        [SerializeField] private float _arrowAbove = 0.12f;
        [Tooltip("Degrees the arrow rolls about the direction it points while rising out of the cable as the hologram opens.")]
        [SerializeField] private float _rollDegrees = 540f;

        private Vector3 _side = Vector3.forward;
        private bool _outward;
        private bool _parked;
        private bool _sliding;
        private float _slideSpeed;

        /// The handle at the segment's tip.
        public Transform Tip => _tip;

        /// What a hand grabs to move the segment.
        public XRBaseInteractable TipGrab => _tipGrab;

        /// What a hand grabs to set the segment's flow.
        public XRBaseInteractable ArrowGrab => _arrowGrab;

        /// Distance from the centre to a face at scale 1, in metres.
        public float FaceDistance => _faceDistance;

        /// True while a hand is moving the handle, so posing leaves it where the hand put it.
        public bool TipHeld { get; set; }

        /// Points the segment at a face, with its arrow floating on the given "above" side showing which
        /// way power crosses it. The arrow belongs to the segment, so moving the handle does not move it.
        public void PlaceOnFace(Vector3 direction, Vector3 above, bool outward)
        {
            transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            Vector3 side = transform.InverseTransformDirection(above);
            side.y = 0f;
            _side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.forward;
            _outward = outward;
            _parked = false;
            if (_arrow != null) _arrow.gameObject.SetActive(true);
        }

        /// Lets go of the handle where the hand left it, and glides it home from there over the given time.
        public void SlideHome(Vector3 fromWorld, float seconds)
        {
            TipHeld = false;
            if (_tip == null) return;
            _tip.position = fromWorld;
            _sliding = true;
            _slideSpeed = seconds > 0f ? _faceDistance / seconds : float.MaxValue;
        }

        /// Turns the arrow round without moving the segment, for previewing a flow change.
        public void SetOutward(bool outward) => _outward = outward;

        /// Pulls the handle into the centre and hides the arrow, since a parked segment carries nothing.
        public void Park()
        {
            transform.localRotation = Quaternion.identity;
            _parked = true;
            if (_arrow != null) _arrow.gameObject.SetActive(false);
        }

        /// Poses the segment partway open: the handle slides out from the centre and the arrow rises out of
        /// the cable, rolling about the direction it points. 0 is closed, 1 fully open.
        public void Pose(float open)
        {
            if (_tip != null && !TipHeld)
            {
                Vector3 home = _parked ? Vector3.zero : Vector3.up * (_faceDistance * open);
                if (_sliding)
                {
                    _tip.localPosition = Vector3.MoveTowards(_tip.localPosition, home, _slideSpeed * Time.deltaTime);
                    _sliding = (_tip.localPosition - home).sqrMagnitude > 1e-8f;
                }
                else _tip.localPosition = home;
            }
            if (_parked || _arrow == null) return;

            _arrow.localPosition = Vector3.up * _arrowAlong + _side * (_arrowAbove * open);
            var rest = Quaternion.LookRotation(_side, _outward ? Vector3.up : Vector3.down);
            _arrow.localRotation = Quaternion.AngleAxis((1f - open) * _rollDegrees, Vector3.up) * rest;
        }
    }
}
