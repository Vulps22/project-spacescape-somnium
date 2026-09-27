using System.Collections.Generic;
using SomniumSpace.Bridge.Components;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Player
{
    /// Where the local player's hands are. The only script that names Somnium's player types; it reads
    /// the hand anchors and never writes to them.
    [RequireComponent(typeof(SomniumPlayersContainer))]
    public sealed class PlayerHands : MonoBehaviour
    {
        [Tooltip("Somnium's list of players, filled at runtime. Found automatically on this object.")]
        [SerializeField] private SomniumPlayersContainer _players;

        [Tooltip("Stand-in hands for testing in the Editor, where Somnium has no player. Drag these about in Play mode.")]
        [SerializeField] private Transform[] _editorHands = new Transform[0];

        private static PlayerHands _instance;

        private void Awake() => _instance = this;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// Copies every hand position there is this frame into the buffer and returns how many.
        public static int Positions(Vector3[] buffer)
        {
            if (_instance == null || buffer == null) return 0;
            int count = 0;

            var body = _instance._players != null && _instance._players.LocalPlayer != null
                ? _instance._players.LocalPlayer.References?.Body
                : null;
            if (body != null)
            {
                if (body.LeftHand != null && count < buffer.Length) buffer[count++] = body.LeftHand.position;
                if (body.RightHand != null && count < buffer.Length) buffer[count++] = body.RightHand.position;
            }

            foreach (var hand in _instance._editorHands)
                if (hand != null && count < buffer.Length) buffer[count++] = hand.position;

            return count;
        }

        /// The solid colliders on the local player's avatar (the capsule that follows the head, and any
        /// others), into the list. Triggers are left out: a hand usually grabs through one. Empty in the
        /// Editor, where there is no avatar.
        public static void BodyColliders(List<Collider> into)
        {
            into.Clear();
            var body = _instance != null && _instance._players != null && _instance._players.LocalPlayer != null
                ? _instance._players.LocalPlayer.References?.Body
                : null;
            if (body == null || body.Root == null) return;
            foreach (var c in body.Root.root.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger) into.Add(c);
        }

        /// True when a collider belongs to the local player's avatar.
        public static bool IsLocalPlayer(Collider collider)
        {
            var body = _instance != null && _instance._players != null && _instance._players.LocalPlayer != null
                ? _instance._players.LocalPlayer.References?.Body
                : null;
            return body != null && body.Root != null && collider != null && collider.transform.IsChildOf(body.Root.root);
        }

        /// Where the local player's head is, falling back to the main camera in the Editor.
        public static bool TryHead(out Vector3 position)
        {
            position = default;
            var body = _instance != null && _instance._players != null && _instance._players.LocalPlayer != null
                ? _instance._players.LocalPlayer.References?.Body
                : null;
            if (body != null && body.Head != null) { position = body.Head.position; return true; }

            var camera = Camera.main;
            if (camera == null) return false;
            position = camera.transform.position;
            return true;
        }

        private void OnValidate()
        {
            if (_players == null) _players = GetComponent<SomniumPlayersContainer>();
        }
    }
}
