using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// How much punishment a tile has left, and which model shows how damaged it is.
    public sealed class IntegrityModule : Module
    {
        [SerializeField] private double _max = 100.0;
        [SerializeField, Range(0f, 1f)] private float _wornBelowFraction = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _failureChanceWhenSpent = 0.5f;

        [Header("Damage models")]
        [SerializeField] private float[] _damageThresholds = new float[0];
        [SerializeField] private GameObject[] _damageModels = new GameObject[0];

        private Integrity _integrity;
        private int _shown = -2;

        /// The sim object behind this module, built on first use so Awake order cannot matter.
        public Integrity Integrity
        {
            get
            {
                if (_integrity == null)
                {
                    _integrity = new Integrity(_max)
                    {
                        WornBelowFraction = _wornBelowFraction,
                        FailureChanceWhenSpent = _failureChanceWhenSpent,
                    };
                }
                return _integrity;
            }
        }

        private void Update() => ShowDamage();

        /// Shows the model for the lowest threshold the condition has fallen below, and hides the rest.
        private void ShowDamage()
        {
            int count = Mathf.Min(_damageThresholds.Length, _damageModels.Length);
            if (count == 0) return;

            float fraction = _integrity != null ? (float)_integrity.Fraction : 1f;
            int pick = -1;
            for (int i = 0; i < count; i++)
                if (fraction < _damageThresholds[i] && (pick < 0 || _damageThresholds[i] < _damageThresholds[pick]))
                    pick = i;

            if (pick == _shown) return;
            _shown = pick;
            for (int i = 0; i < count; i++)
                if (_damageModels[i] != null) _damageModels[i].SetActive(i == pick);
        }

        private void OnValidate()
        {
            if (_integrity == null) return;
            _integrity.Max = _max;
            _integrity.WornBelowFraction = _wornBelowFraction;
            _integrity.FailureChanceWhenSpent = _failureChanceWhenSpent;
        }
    }
}
