using SomniumSpace.Bridge.Components;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Player
{
    /// Where the local player's hands are. The only script that names Somnium's player types; it reads
    /// the hand anchors and never writes to them.
    [RequireComponent(typeof(SomniumPlayersContainer))]
    public sealed class PlayerHands : MonoBehaviour
    {
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

        private void OnValidate()
        {
            if (_players == null) _players = GetComponent<SomniumPlayersContainer>();
        }
    }
}
