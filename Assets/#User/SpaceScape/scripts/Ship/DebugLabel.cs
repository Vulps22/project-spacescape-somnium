using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A readout that only exists for development. A meter is a plain label and carries none of this.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class DebugLabel : MonoBehaviour
    {
        private TMP_Text _text;

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void Update() => _text.enabled = DebugVisuals.UseDebuggingVisuals;
    }
}
