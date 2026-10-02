using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A readout that only exists for development. A meter is a plain label and carries none of this. Shows
    /// and hides when the overlay is switched or its hologram opens or closes; nothing runs per frame.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class DebugLabel : MonoBehaviour
    {
        [Tooltip("Ticked: shown only while the conduit's hologram is open, so the grid is not covered in numbers. Unticked: shown whenever debug visuals are on.")]
        [SerializeField] private bool _onlyInHologram;

        private TMP_Text _text;

        private bool _revealed;

        /// Set while its conduit's hologram is open, which is when a label that is only in the hologram shows.
        public bool Revealed
        {
            get => _revealed;
            set
            {
                if (_revealed == value) return;
                _revealed = value;
                Show();
            }
        }

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            DebugVisuals.Changed += Show;
            Show();
        }

        private void OnDestroy() => DebugVisuals.Changed -= Show;

        private void Show()
        {
            bool show = DebugVisuals.UseDebuggingVisuals && (!_onlyInHologram || _revealed);
            if (_text != null && _text.enabled != show) _text.enabled = show;
        }
    }
}
