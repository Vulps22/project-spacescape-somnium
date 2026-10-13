using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Pins the origin of this object and of its direct children to the grid while it is edited: each world
    /// position goes to the nearest whole multiple of GridCell.Size on every axis, however it is moved
    /// (dragged, typed, or carried by a parent). Only one layer down: a corridor segment pins its containers,
    /// not the art inside them. Does nothing in Play mode.
    ///
    /// Left alone: anything on the Exclude list; empties (nothing on them but their
    /// Transform, and no children), which mark a point, like a socket or an Origin; things that keep to the
    /// grid by rules of their own (an AccessPanel's Cover Origin, a ComponentSlot, whose centres sit half
    /// a cell off); and preview objects that are never saved.
    [ExecuteAlways]
    public sealed class GridPin : MonoBehaviour
    {
        [Tooltip("Direct children left where they are.")]
        [SerializeField] private Transform[] _exclude = new Transform[0];

        private readonly HashSet<Transform> _skip = new HashSet<Transform>();

        private void OnEnable() => transform.hasChanged = true;

        private void OnValidate() => transform.hasChanged = true;

        private void Update()
        {
            if (Application.isPlaying) return;

            bool changed = transform.hasChanged;
            for (int i = 0; i < transform.childCount && !changed; i++) changed = transform.GetChild(i).hasChanged;
            if (!changed) return;

            FindSkipped();
            Pin(transform);
            for (int i = 0; i < transform.childCount; i++) Pin(transform.GetChild(i));
            transform.hasChanged = false;
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).hasChanged = false;
        }

        private void FindSkipped()
        {
            _skip.Clear();
            foreach (var t in _exclude)
                if (t != null) _skip.Add(t);
            foreach (var panel in GetComponentsInChildren<AccessPanel>(true))
                if (panel.CoverOrigin != panel.transform) _skip.Add(panel.CoverOrigin);
            foreach (var slot in GetComponentsInChildren<ComponentSlot>(true)) _skip.Add(slot.transform);
        }

        /// Pins one transform, unless it is left alone.
        private void Pin(Transform t)
        {
            if (_skip.Contains(t) || IsPreview(t) || IsEmpty(t)) return;
            Vector3 position = t.position;
            var pinned = new Vector3(Snap(position.x), Snap(position.y), Snap(position.z));
            if ((pinned - position).sqrMagnitude > 1e-10f) t.position = pinned;
        }

        private static bool IsPreview(Transform t) => (t.gameObject.hideFlags & HideFlags.DontSave) != 0;

        /// True for an object with nothing on it but its Transform and no children of its own; a preview hung
        /// on it does not count.
        private static bool IsEmpty(Transform t)
        {
            if (t == null || t.GetComponents<Component>().Length > 1) return false;
            for (int i = 0; i < t.childCount; i++)
                if (!IsPreview(t.GetChild(i))) return false;
            return true;
        }

        private static float Snap(float v) => Mathf.Round(v / GridCell.Size) * GridCell.Size;
    }
}
