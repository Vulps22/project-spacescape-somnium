using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Holds the one switch that turns the development overlay on. Lives on SceneManager.
    public sealed class DebugVisuals : MonoBehaviour
    {
        [Tooltip("Shows every debug readout in the scene. Off hides them all.")]
        [SerializeField] private bool _useDebuggingVisuals = true;

        /// True while the grid should show its numbers. Read directly; nothing subscribes.
        public static bool UseDebuggingVisuals { get; private set; }

        private void Awake() => UseDebuggingVisuals = _useDebuggingVisuals;

        private void OnValidate() => UseDebuggingVisuals = _useDebuggingVisuals;
    }
}
