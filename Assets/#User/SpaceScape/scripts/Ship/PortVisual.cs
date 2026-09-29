using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A port on a component's face: a ring round a closed door. The ring says which way power goes.
    public sealed class PortVisual : MonoBehaviour
    {
        [Tooltip("The ring round the door, coloured by direction.")]
        [SerializeField] private Renderer _ring;
        [Tooltip("The ring's material on an input.")]
        [SerializeField] private Material _inMaterial;
        [Tooltip("The ring's material on an output.")]
        [SerializeField] private Material _outMaterial;

        /// The ring round the door.
        public Renderer Ring => _ring;

        /// The ring's material for the way power crosses here.
        public Material MaterialFor(FlowDirection flow) => flow == FlowDirection.Out ? _outMaterial : _inMaterial;

        /// Colours the ring for the way power crosses here.
        public void Show(FlowDirection flow)
        {
            if (_ring == null) return;
            var material = flow == FlowDirection.Out ? _outMaterial : _inMaterial;
            if (material != null) _ring.sharedMaterial = material;
        }
    }
}
