using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Gives a conduit extra segments, shown by a small box on the cable. Its arrows make it a split, a merge,
    /// or anything else. One prefab covers every size: the count is the junction's own state, so it travels
    /// over the network with the conduit, and with the item when the junction is taken out.
    public sealed class JunctionAddon : ConduitAddon
    {
        public const int MinExtra = 1;
        public const int MaxExtra = 4;

        [Tooltip("Segments this adds to the conduit's own two: +1 to +4. The count a junction starts with when none is given, as for a drawn conduit or an item that sets none.")]
        [SerializeField, Range(MinExtra, MaxExtra)] private int _extraSegments = MinExtra;

        /// Segments this adds, +1 to +4.
        public int Count
        {
            get => _extraSegments;
            set
            {
                value = Mathf.Clamp(value, MinExtra, MaxExtra);
                if (_extraSegments == value) return;
                _extraSegments = value;
                RaiseChanged();
            }
        }

        protected override int ExtraSegments => _extraSegments;

        public override int NetworkState
        {
            get => _extraSegments;
            set => Count = value;
        }

        public override bool StateTravelsWithItem => true;

        private void OnValidate() => RaiseChanged();
    }
}
