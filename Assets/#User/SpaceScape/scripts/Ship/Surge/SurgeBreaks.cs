using Fusion;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The surge itself (docs/tutorial-proposal.md, Plan D → What the surge breaks): when the cube is hit, breaks the
    /// bay the way the script expects, through the same edits a hand or a pop makes. The bay is a LocalZone, so these
    /// break this player's own copy only.
    public sealed class SurgeBreaks : MonoBehaviour
    {
        [Header("The reactor's feed")]
        [Tooltip("A marker in the conduit cell that pops; its switch comes off.")]
        [SerializeField] private Transform _popCell;
        [Tooltip("The reactor's slot: its levers go to zero, so it is cold.")]
        [SerializeField] private ComponentSlot _reactorSlot;

        [Header("The battery")]
        [Tooltip("The slot of the battery that is wrecked.")]
        [SerializeField] private ComponentSlot _wreckSlot;
        [Tooltip("Markers in the cells off the battery's Out port that are cut (fully parked).")]
        [SerializeField] private Transform[] _cutCells = new Transform[0];
        [Tooltip("A marker in the cell at the end of the line those cells joined: its segment is knocked aside.")]
        [SerializeField] private Transform _feedEnd;
        [Tooltip("The face of the feed end's segment that met the cut cells.")]
        [SerializeField] private GridDirection _feedEndFrom = GridDirection.YMinus;
        [Tooltip("The wrong face it is knocked round to, its arrow Out.")]
        [SerializeField] private GridDirection _feedEndTo = GridDirection.XMinus;

        [Header("The delivered battery")]
        [Tooltip("The replacement battery's prefab (a registered network prefab).")]
        [SerializeField] private NetworkObject _deliveredBattery;
        [Tooltip("Where it is delivered.")]
        [SerializeField] private Transform _deliverAt;

        private bool _surged;

        /// Breaks the bay, once.
        public void Surge()
        {
            if (_surged) return;
            var cells = ConduitCells.Current;
            if (cells == null) return;
            _surged = true;

            if (_popCell != null)
            {
                var cell = GridCell.ToCell(_popCell.position);
                cells.Break(ConduitCells.Edit.Addon, cell, 0, 0);
                cells.Blew(cell);
            }

            var reactor = _reactorSlot != null && _reactorSlot.Component != null
                ? _reactorSlot.Component.GetComponentInChildren<ReactorBehaviourModule>() : null;
            if (reactor != null)
                for (int axis = 0; axis <= reactor.ChamberCount; axis++) reactor.TurnDial(axis, 0f);

            var integrity = _wreckSlot != null && _wreckSlot.Component != null
                ? _wreckSlot.Component.GetComponentInChildren<IntegrityModule>() : null;
            if (integrity != null) integrity.Integrity.TakeDamage(integrity.Integrity.Current);

            if (_feedEnd != null)
            {
                var cell = GridCell.ToCell(_feedEnd.position);
                var c = cells.Get(cell);
                if (c != null)
                {
                    int from = ConduitCells.Bit(_feedEndFrom), to = ConduitCells.Bit(_feedEndTo);
                    cells.Break(ConduitCells.Edit.Faces, cell, (c.Used & ~from) | to, (c.Out & ~from) | to);
                }
            }
            foreach (var marker in _cutCells)
                if (marker != null) cells.Break(ConduitCells.Edit.Faces, GridCell.ToCell(marker.position), 0, 0);

            Debug.Log("[SpaceScape] Surge: the bay is broken", this);
        }

        /// Delivers the replacement battery, once: this player's own, never shared.
        public void Deliver()
        {
            if (!_surged || _deliveredBattery == null || _deliverAt == null) return;
            LocalZone.MakeLocal(_deliveredBattery.gameObject, _deliverAt.position, _deliverAt.rotation);
            _deliveredBattery = null;
        }
    }
}
