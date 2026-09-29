using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A slot's grab handle. Pulled far enough out, it asks its slot to unlock; let go, it slides home. Red
    /// while the slot is locked, green while it is unlocked.
    public sealed class SlotHandle : MonoBehaviour
    {
        [Tooltip("The slot this handle unlocks. Found in the parents when empty.")]
        [SerializeField] private ComponentSlot _slot;
        [Tooltip("The bar a hand grabs: an XRGrabInteractable with a SlideGrabTransformer.")]
        [SerializeField] private XRGrabInteractable _bar;
        [Tooltip("The bar's renderer, coloured by the slot's lock.")]
        [SerializeField] private Renderer _barRenderer;
        [Tooltip("The bar's material while the slot is locked.")]
        [SerializeField] private Material _lockedMaterial;
        [Tooltip("The bar's material while the slot is unlocked.")]
        [SerializeField] private Material _unlockedMaterial;
        [Tooltip("Share of the bar's travel it must be pulled to unlock, 0 to 1.")]
        [SerializeField, Range(0.1f, 1f)] private float _pullShare = 0.8f;
        [Tooltip("Seconds the bar takes to slide home once let go.")]
        [SerializeField] private float _returnSeconds = 0.15f;

        private SlideGrabTransformer _slide;
        private bool _pulledThisGrab;
        private bool _shownUnlocked;

        private void Awake()
        {
            if (_slot == null) _slot = GetComponentInParent<ComponentSlot>();
            if (_bar != null) _slide = _bar.GetComponent<SlideGrabTransformer>();
            Show(_slot != null && _slot.Unlocked);
        }

        private void Update()
        {
            if (_slot == null || _bar == null || _slide == null) return;

            if (_bar.isSelected)
            {
                if (!_pulledThisGrab && _slide.Pulled >= _slide.Travel * _pullShare)
                {
                    _pulledThisGrab = true;
                    _slot.Pull();
                }
            }
            else
            {
                _pulledThisGrab = false;
                // Slide home. XRI is not moving it now, so nothing else writes it.
                var t = _bar.transform;
                if (t.localPosition != _slide.RestPosition)
                    t.localPosition = Vector3.MoveTowards(t.localPosition, _slide.RestPosition,
                        _slide.Travel / Mathf.Max(0.01f, _returnSeconds) * Time.deltaTime);
            }

            if (_slot.Unlocked != _shownUnlocked) Show(_slot.Unlocked);
        }

        private void Show(bool unlocked)
        {
            _shownUnlocked = unlocked;
            if (_barRenderer == null) return;
            var material = unlocked ? _unlockedMaterial : _lockedMaterial;
            if (material != null) _barRenderer.sharedMaterial = material;
        }
    }
}
