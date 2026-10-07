using System;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A button a hand presses by pushing into it, as a hand pushes a switch: it presses once as the hand goes
    /// in, and again only after the hand has come out. The cap sinks while held in. If the object also has an
    /// interactable, grabbing it presses it too.
    ///
    /// Whether a hand is in is asked of the button where it rests, not where it has sunk to, so the cap moving
    /// away from the hand never reads as the hand coming out and pressing again.
    ///
    /// Only this client's hands press it; what a press does, and how others hear of it, is the owner's (Pressed).
    [RequireComponent(typeof(Collider))]
    public sealed class PushButton : MonoBehaviour
    {
        [Tooltip("The way the cap moves when pushed, in its own space: into whatever it sits on.")]
        [SerializeField] private Vector3 _pushDirection = Vector3.back;
        [Tooltip("How far the cap sinks while a hand is pushing it, in metres.")]
        [SerializeField] private float _travel = 0.015f;

        private static readonly Collider[] Touching = new Collider[16];

        private Collider _collider;
        private XRBaseInteractable _grab;
        private Vector3 _rest;
        private bool _pushed;

        /// Raised on this client when a hand here presses it, by pushing or by grabbing.
        public event Action Pressed;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _grab = GetComponent<XRBaseInteractable>();
            _rest = transform.localPosition;
        }

        private void OnEnable()
        {
            if (_grab != null) _grab.selectEntered.AddListener(OnGrabbed);
        }

        private void OnDisable()
        {
            if (_grab != null) _grab.selectEntered.RemoveListener(OnGrabbed);
        }

        private void OnGrabbed(SelectEnterEventArgs args) => Pressed?.Invoke();

        private void FixedUpdate()
        {
            bool pushed = HandIn();
            if (pushed == _pushed) return;
            _pushed = pushed;
            transform.localPosition = pushed ? _rest + transform.localRotation * _pushDirection.normalized * _travel : _rest;
            if (pushed) Pressed?.Invoke();
        }

        /// True while any part of the local player is in the button as it rests.
        private bool HandIn()
        {
            var t = transform;
            Vector3 restPosition = t.parent != null ? t.parent.TransformPoint(_rest) : _rest;
            Vector3 offset = restPosition - t.position;
            var bounds = _collider.bounds;
            int count = Physics.OverlapBoxNonAlloc(bounds.center + offset, bounds.extents, Touching, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = Touching[i];
                if (other == _collider || !PlayerHands.IsLocalPlayer(other)) continue;
                if (Physics.ComputePenetration(_collider, restPosition, t.rotation, other, other.transform.position,
                        other.transform.rotation, out _, out _)) return true;
            }
            return false;
        }
    }
}
