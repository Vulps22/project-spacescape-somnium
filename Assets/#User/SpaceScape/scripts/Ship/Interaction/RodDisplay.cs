using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Shows how far a reactor's rods are in, 100 being fully inserted, with the dial's setting beneath.
    public sealed class RodDisplay : MonoBehaviour
    {
        [SerializeField] private ReactorBehaviourModule _reactor;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private float _interval = 0.1f;

        private float _next;

        private void Update()
        {
            if (_reactor == null || _text == null || Time.time < _next) return;
            _next = Time.time + _interval;

            double rods = _reactor.RodInsertion * 100.0;
            double set = (1.0 - _reactor.TargetWithdrawal) * 100.0;
            _text.SetText($"RODS {rods:0}\nset {set:0}");
        }

        private void OnValidate()
        {
            if (_reactor == null) _reactor = GetComponentInParent<ReactorBehaviourModule>();
            if (_text == null) _text = GetComponent<TMP_Text>();
        }
    }
}
