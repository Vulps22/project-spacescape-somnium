using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Player
{
    /// Keeps this object's colliders from touching the local player's body, while leaving them solid so a
    /// hand can still grab them. For things that follow a hand, like hologram handles: solid and moved
    /// into the player's upright capsule, they shove the player back, and the handle goes with them.
    /// Works by pairs of colliders, so it does not care what layer Somnium puts the avatar on. Switched off,
    /// it lets the pairs it ignored collide again, so a carried component is solid once it is put down.
    public sealed class IgnoresPlayerBody : MonoBehaviour
    {
        [Tooltip("Seconds between re-applying. Unity forgets an ignored pair whenever either collider is switched off, and parts of a hologram switch on and off as it is used.")]
        [SerializeField] private float _reapplySeconds = 0.25f;

        private readonly List<Collider> _body = new List<Collider>();
        private readonly List<Collider> _own = new List<Collider>();
        private readonly List<(Collider own, Collider body)> _ignored = new List<(Collider, Collider)>();
        private float _next;

        private void OnEnable() => _next = 0f;

        private void OnDisable()
        {
            foreach (var (own, body) in _ignored)
                if (own != null && body != null) Physics.IgnoreCollision(own, body, false);
            _ignored.Clear();
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + _reapplySeconds;

            PlayerHands.BodyColliders(_body);
            if (_body.Count == 0) return;
            GetComponentsInChildren(true, _own);

            foreach (var own in _own)
            {
                if (own == null || own.isTrigger || !own.enabled || !own.gameObject.activeInHierarchy) continue;
                foreach (var body in _body)
                {
                    if (body == null) continue;
                    Physics.IgnoreCollision(own, body, true);
                    if (!_ignored.Contains((own, body))) _ignored.Add((own, body));
                }
            }
        }
    }
}
