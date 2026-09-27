using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A readout that only exists for development. A meter is a plain label and carries none of this.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class DebugLabel : MonoBehaviour
    {
        [Tooltip("Ticked: shown only while the conduit's hologram is open, so the grid is not covered in numbers. Unticked: shown whenever debug visuals are on.")]
        [SerializeField] private bool _onlyInHologram;

        private TMP_Text _text;

        /// Set while its conduit's hologram is open, which is when a label that is only in the hologram shows.
        public bool Revealed { get; set; }

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void Update() => _text.enabled = DebugVisuals.UseDebuggingVisuals && (!_onlyInHologram || Revealed);
    }
}
