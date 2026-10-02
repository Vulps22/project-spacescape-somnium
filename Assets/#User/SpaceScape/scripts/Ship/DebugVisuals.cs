using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Holds the one switch that turns the development overlay on. Lives on SceneManager.
    public sealed class DebugVisuals : MonoBehaviour
    {
        [Tooltip("Shows every debug readout in the scene. Off hides them all.")]
        [SerializeField] private bool _useDebuggingVisuals = true;

        private static bool _on;

        /// True while the grid should show its numbers.
        public static bool UseDebuggingVisuals
        {
            get => _on;
            private set
            {
                if (_on == value) return;
                _on = value;
                Changed?.Invoke();
            }
        }

        /// Raised when the overlay is switched on or off, for every label to show or hide.
        public static event Action Changed;

        private void Awake() => UseDebuggingVisuals = _useDebuggingVisuals;

        private void OnValidate() => UseDebuggingVisuals = _useDebuggingVisuals;
    }
}
