using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// What the tutorial displays share: stripping a copy of a real prefab down to its look, easing, and the way
    /// a ghost hand reaches in.
    internal static class TutorialParts
    {
        /// Takes out everything that could be grabbed, pressed, wired or collided with, leaving the look.
        public static GameObject Strip(GameObject go)
        {
            // Scripts first: some require the grab or the rigidbody, and would block their removal. Last added
            // first, since a script that requires another is usually added after it.
            var scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = scripts.Length - 1; i >= 0; i--)
            {
                var c = scripts[i];
                if (!(c is SegmentHologram) && !(c is TMP_Text) && !(c is UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable)) Object.DestroyImmediate(c);
            }
            foreach (var c in go.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return go;
        }

        /// 0 before a span of time, 1 after it, eased in between.
        public static float Ease(float t, float from, float to) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));

        /// A hand reaching toward the display from where the watcher stands: fingers pointing in, palm down.
        public static readonly Quaternion Reaching = Quaternion.LookRotation(Vector3.back, Vector3.up);
    }

    /// A see-through hand built from simple shapes: a palm, four fingers and a thumb, which curl to grip.
    internal sealed class GhostHand
    {
        private readonly Transform _root;
        private readonly Transform[] _fingers = new Transform[4];
        private readonly Transform _thumb;
        private readonly float _side;

        public GhostHand(Transform parent, Material material, bool left)
        {
            _root = new GameObject(left ? "GhostHandLeft" : "GhostHandRight").transform;
            _root.SetParent(parent, false);
            _side = left ? -1f : 1f;

            // The palm, fingers forward (+Z of the hand), back of the hand up.
            Part(PrimitiveType.Cube, _root, new Vector3(0f, 0f, -0.045f), new Vector3(0.08f, 0.022f, 0.09f), material);
            for (int i = 0; i < 4; i++)
            {
                var knuckle = new GameObject("Finger").transform;
                knuckle.SetParent(_root, false);
                knuckle.localPosition = new Vector3(_side * (-0.03f + i * 0.02f), 0f, 0f);
                Part(PrimitiveType.Capsule, knuckle, new Vector3(0f, 0f, 0.035f), new Vector3(0.017f, 0.035f, 0.017f), material, 90f);
                _fingers[i] = knuckle;
            }
            _thumb = new GameObject("Thumb").transform;
            _thumb.SetParent(_root, false);
            _thumb.localPosition = new Vector3(_side * -0.045f, -0.005f, -0.05f);
            Part(PrimitiveType.Capsule, _thumb, new Vector3(0f, 0f, 0.03f), new Vector3(0.019f, 0.03f, 0.019f), material, 90f);
        }

        /// Puts the hand's fingertips at a point, turned a way, with its fingers curled 0 (open) to 1 (gripping).
        public void Place(Vector3 fingertips, Quaternion turn, float grip)
        {
            _root.localRotation = turn;
            _root.localPosition = fingertips - turn * (Vector3.forward * 0.07f);
            for (int i = 0; i < 4; i++) _fingers[i].localRotation = Quaternion.Euler(grip * 80f, 0f, 0f);
            _thumb.localRotation = Quaternion.Euler(grip * 30f, _side * -40f * (1f - grip * 0.8f), 0f);
        }

        private static void Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, float pitch = 0f)
        {
            var part = GameObject.CreatePrimitive(type);
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            part.transform.localScale = scale;
            var r = part.GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
