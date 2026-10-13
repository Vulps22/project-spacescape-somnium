using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A part a hand opens with a tug and shuts with a push, then moves the rest of the way on its own: a pack's lid
    /// (packs.md → How a pack works), the S1 reactor's cartridge (reactor.md → S1: RTG).
    ///
    /// The grip is an XRGrabInteractable with a SlideGrabTransformer, a child of the track. A tug out past Tug Share
    /// of its slide on the shut part opens it, a push back on the open part shuts it, and either lets go of the grip,
    /// as the track moves off by itself: Distance along Direction over Seconds. Let go, the grip slides home in its
    /// track. Open or shut is state: the client whose hand changed it raises ChangedByHand for the networking to
    /// share, and every client runs the move, and its sounds, from that.
    public sealed class TugSlide : MonoBehaviour
    {
        [Tooltip("Moved to open: the grip and whatever opens with it hang under it.")]
        [SerializeField] private Transform _track;
        [Tooltip("What a hand tugs: an XRGrabInteractable with a SlideGrabTransformer that slides both ways, a child of the track.")]
        [SerializeField] private XRGrabInteractable _grip;
        [Tooltip("Which way the track moves to open, in its parent's space.")]
        [SerializeField] private Vector3 _direction = Vector3.up;
        [Tooltip("How far the track moves when open, in metres.")]
        [SerializeField] private float _distance = 1.08f;
        [Tooltip("Seconds the track takes to open or shut.")]
        [SerializeField] private float _seconds = 2f;
        [Tooltip("Share of the grip's slide a tug or push must reach to start it, 0 to 1.")]
        [SerializeField, Range(0.1f, 1f)] private float _tugShare = 0.8f;
        [Tooltip("Seconds the grip takes to slide home in its track once let go.")]
        [SerializeField] private float _returnSeconds = 0.15f;

        [Header("Sound")]
        [Tooltip("Played as it starts to open.")]
        [SerializeField] private AudioClip _openClip;
        [Tooltip("Played as it starts to shut.")]
        [SerializeField] private AudioClip _closeClip;
        [Tooltip("Plays the sounds. A child object on the Environment layer, so the player's volume controls apply.")]
        [SerializeField] private AudioSource _sound;

        private SlideGrabTransformer _slide;
        private Vector3 _closedAt;
        private bool _open;
        private float _raised;   // 0 shut to 1 open
        private bool _tugged;    // the held grip has already started a move this grab
        private bool _soundedOpen;

        /// Raised on the client whose hand opened or shut it.
        public event Action ChangedByHand;

        /// True while it is open or opening.
        public bool Open => _open;

        /// How far it has opened: 0 shut, 1 open.
        public float Raised => _raised;

        /// Takes the state the networking was sent.
        public void Apply(bool open) => _open = open;

        private void Awake()
        {
            if (_grip != null) _slide = _grip.GetComponent<SlideGrabTransformer>();
            if (_track != null) _closedAt = _track.localPosition;
        }

        private void Update()
        {
            UpdateGrip();

            // Each client plays the sounds from its own part, so nothing is sent for them.
            if (_open != _soundedOpen)
            {
                _soundedOpen = _open;
                var clip = _open ? _openClip : _closeClip;
                if (clip != null && _sound != null) _sound.PlayOneShot(clip);
            }

            float was = _raised;
            _raised = Mathf.MoveTowards(_raised, _open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, _seconds));
            if (_track != null && _raised != was)
                _track.localPosition = _closedAt + _direction.normalized * (_distance * Mathf.SmoothStep(0f, 1f, _raised));
        }

        /// A tug on the shut part opens it; a push on the open part shuts it. Either lets go of the grip, as the part
        /// moves off by itself. Let go, the grip slides home in its track.
        private void UpdateGrip()
        {
            if (_grip == null || _slide == null) return;
            bool settled = _raised <= 0f || _raised >= 1f;
            if (_grip.enabled != (settled || _grip.isSelected)) _grip.enabled = settled || _grip.isSelected;

            if (_grip.isSelected)
            {
                if (_tugged || !settled) return;
                bool tugged = !_open && _slide.Pulled >= _slide.Travel * _tugShare;
                bool pushed = _open && _slide.TravelDown > 0f && _slide.Pulled <= -_slide.TravelDown * _tugShare;
                if (!tugged && !pushed) return;
                _tugged = true;
                _open = tugged;
                ChangedByHand?.Invoke();
                LetGoOf(_grip);
                return;
            }

            _tugged = false;
            var t = _grip.transform;
            if (t.localPosition != _slide.RestPosition)
            {
                float most = Mathf.Max(_slide.Travel, _slide.TravelDown);
                t.localPosition = Vector3.MoveTowards(t.localPosition, _slide.RestPosition,
                    most / Mathf.Max(0.01f, _returnSeconds) * Time.deltaTime);
            }
        }

        /// Takes every hand off a grab.
        public static void LetGoOf(XRGrabInteractable grab)
        {
            var manager = grab.interactionManager;
            if (manager == null) return;
            for (int i = grab.interactorsSelecting.Count - 1; i >= 0; i--)
                manager.SelectExit(grab.interactorsSelecting[i], grab);
        }
    }
}
