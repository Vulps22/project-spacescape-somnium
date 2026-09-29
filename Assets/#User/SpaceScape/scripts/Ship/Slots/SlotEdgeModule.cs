using System.Collections.Generic;
using System.Linq;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A slot's ports, which are its component's: every question goes to the component's edges, which answer
    /// from where the slot has seated it. An empty slot has none.
    public sealed class SlotEdgeModule : NodeEdgeModule
    {
        private NodeEdgeModule Inner
        {
            get
            {
                var component = TryGetComponent<ComponentSlot>(out var slot) ? slot.Component : null;
                return component != null ? component.Edges : null;
            }
        }

        public override FlowDirection GetFace(GridDirection face) =>
            Inner != null ? Inner.GetFace(face) : FlowDirection.None;

        public override IEnumerable<GridDirection> Outputs =>
            Inner != null ? Inner.Outputs : Enumerable.Empty<GridDirection>();

        public override IEnumerable<GridDirection> Inputs =>
            Inner != null ? Inner.Inputs : Enumerable.Empty<GridDirection>();

        public override bool SendsAt(GridDirection face, UnityEngine.Vector3Int cell) =>
            Inner != null && Inner.SendsAt(face, cell);

        public override bool AcceptsAt(GridDirection face, UnityEngine.Vector3Int cell) =>
            Inner != null && Inner.AcceptsAt(face, cell);
    }
}
