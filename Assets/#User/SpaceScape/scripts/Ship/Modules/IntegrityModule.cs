using SomniumSpace.Worlds.SpaceScape.Power;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// How much punishment a tile has left, and which model shows how damaged it is.
    public sealed class IntegrityModule : Module, INetworkedState
    {
        [Tooltip("Condition when new. Each heat failure takes the grid's Pop Damage (25) off it; at 0 it is wrecked.")]
        [SerializeField] private double _max = 100.0;
        [Tooltip("Share of full condition below which it starts to misfire. 0.2 means below 20% of Max.")]
        [SerializeField, Range(0f, 1f)] private float _wornBelowFraction = 0.2f;
        [Tooltip("Chance of a misfire at 0 condition. Rises from nothing at the worn line to this.")]
        [SerializeField, Range(0f, 1f)] private float _failureChanceWhenSpent = 0.5f;

        [Header("Damage models")]
        [Tooltip("Condition shares (0-1), one per damage model in the same order. Below a threshold its model shows; the lowest passed wins.")]
        [SerializeField] private float[] _damageThresholds = new float[0];
        [Tooltip("Objects to show as it gets damaged, paired by order with the thresholds. The rest are hidden.")]
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

        /// What it knows, for a component's debug readout: condition, the worn line, and the chance it
        /// misfires now.
        public string DebugText
        {
            get
            {
                var integrity = Integrity;
                string state = integrity.IsDestroyed ? "WRECKED" : integrity.IsWorn ? "WORN" : "sound";
                return $"integrity {integrity.Current:0}/{integrity.Max:0} ({integrity.Fraction:P0}) {state}\n" +
                       $"worn below {integrity.WornBelowFraction:P0}, misfire {integrity.FailureChance:P0}";
            }
        }

        public int StateCount => 1;
        public void WriteState(float[] to, int at) => to[at] = (float)Integrity.Current;
        public void ReadState(float[] from, int at) => Integrity.Correct(from[at]);

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
