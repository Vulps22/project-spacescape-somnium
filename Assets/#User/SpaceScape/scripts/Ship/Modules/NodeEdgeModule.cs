using System.Collections.Generic;
using UnityEngine;

namespace SpaceScape.Ship
{
    /// Which faces of a tile power enters and leaves by. One value per face, so inputs and outputs
    /// cannot disagree. Faces are world directions until live rewiring makes them turn with the tile.
    public sealed class NodeEdgeModule : Module
    {
        [SerializeField] private FlowDirection _xPlus = FlowDirection.In;
        [SerializeField] private FlowDirection _xMinus = FlowDirection.In;
        [SerializeField] private FlowDirection _yPlus = FlowDirection.In;
        [SerializeField] private FlowDirection _yMinus = FlowDirection.In;
        [SerializeField] private FlowDirection _zPlus = FlowDirection.In;
        [SerializeField] private FlowDirection _zMinus = FlowDirection.In;

        private static readonly GridDirection[] AllFaces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        /// Which way power may cross a face.
        public FlowDirection GetFace(GridDirection face)
        {
            switch (face)
            {
                case GridDirection.XPlus: return _xPlus;
                case GridDirection.XMinus: return _xMinus;
                case GridDirection.YPlus: return _yPlus;
                case GridDirection.YMinus: return _yMinus;
                case GridDirection.ZPlus: return _zPlus;
                case GridDirection.ZMinus: return _zMinus;
                default: return FlowDirection.None;
            }
        }

        /// Sets which way power may cross a face.
        public void SetFace(GridDirection face, FlowDirection flow)
        {
            switch (face)
            {
                case GridDirection.XPlus: _xPlus = flow; break;
                case GridDirection.XMinus: _xMinus = flow; break;
                case GridDirection.YPlus: _yPlus = flow; break;
                case GridDirection.YMinus: _yMinus = flow; break;
                case GridDirection.ZPlus: _zPlus = flow; break;
                case GridDirection.ZMinus: _zMinus = flow; break;
            }
        }

        /// Every face power leaves by.
        public IEnumerable<GridDirection> Outputs => FacesSetTo(FlowDirection.Out);

        /// Every face power may arrive by.
        public IEnumerable<GridDirection> Inputs => FacesSetTo(FlowDirection.In);

        /// Whether power may enter by a given face.
        public bool AcceptsFrom(GridDirection face) => GetFace(face) == FlowDirection.In;

        /// Every face set to one direction.
        private IEnumerable<GridDirection> FacesSetTo(FlowDirection flow)
        {
            for (int i = 0; i < AllFaces.Length; i++)
                if (GetFace(AllFaces[i]) == flow) yield return AllFaces[i];
        }
    }
}
