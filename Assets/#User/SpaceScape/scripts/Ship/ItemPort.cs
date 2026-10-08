using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A component's item port: an amber square filling one cell face, where loadable items go in (loading.md).
    ///
    /// Its doors open while an item one of the component's LoadingModules would take is inside the intake (a box in
    /// front of the face), and close when there is none. Every client works that out from where it sees the item,
    /// so nothing is sent for the doors, nor for their sounds (one as they start to open, one as they land shut). When a hand on this client lets go of such an item in the intake, this
    /// client (which holds the item, so moves it for everyone) lines it up with the port and draws it in, loads
    /// it into the LoadingModule, waits for the doors to shut, and removes it. Once a conduit is wired to the port
    /// the intake is turned off and items arrive along the conduit instead (not built).
    public sealed class ItemPort : MonoBehaviour
    {
        [Tooltip("The trigger in front of the face where an item is noticed.")]
        [SerializeField] private Collider _intake;
        [Tooltip("The two door halves, each scaled across from its outer edge to open.")]
        [SerializeField] private Transform _doorA;
        [SerializeField] private Transform _doorB;
        [Tooltip("Seconds the doors take to open or close.")]
        [SerializeField] private float _doorSeconds = 0.5f;
        [Tooltip("How much of its width a door keeps when open.")]
        [SerializeField] private float _openWidth = 0.05f;
        [Tooltip("Seconds a let-go item takes to line up with the port.")]
        [SerializeField] private float _alignSeconds = 0.75f;
        [Tooltip("Seconds a lined-up item takes to be drawn all the way in.")]
        [SerializeField] private float _drawInSeconds = 5f;
        [Tooltip("Seconds between looks inside the intake.")]
        [SerializeField] private float _checkSeconds = 0.1f;

        [Header("Sound")]
        [Tooltip("Played as the doors start to open.")]
        [SerializeField] private AudioClip _openClip;
        [Tooltip("Played as the doors finish closing.")]
        [SerializeField] private AudioClip _closeClip;
        [Tooltip("Plays the door sounds. A child object on the Environment layer, so the player's volume controls apply.")]
        [SerializeField] private AudioSource _sound;

        private static readonly Collider[] Found = new Collider[16];
        private readonly HashSet<LoadableItem> _watched = new HashSet<LoadableItem>();
        private readonly List<LoadableItem> _inside = new List<LoadableItem>();
        private LoadingModule[] _loading;
        private LoadableItem _drawing;
        private float _open;
        private bool _opening;
        private float _nextCheck;

        /// The trigger in front of the face where an item is noticed.
        public Collider Intake => _intake;

        private void Start() => _loading = GetComponentsInParent<LoadingModule>();

        private void OnDisable()
        {
            foreach (var item in _watched) if (item != null) GrabOf(item).selectExited.RemoveListener(OnLetGo);
            _watched.Clear();
        }

        private void Update()
        {
            if (_intake != null && _intake.enabled && Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + _checkSeconds;
                Look();
            }

            bool wanted = _drawing != null;
            foreach (var item in _inside) wanted |= item != null && LoaderFor(item) != null;
            // The open sound as they start to open; the close sound as they land shut, not if they reopen first.
            if (wanted && !_opening) Play(_openClip);
            _opening = wanted;
            float was = _open;
            _open = Mathf.MoveTowards(_open, wanted ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, _doorSeconds));
            if (was > 0f && _open <= 0f) Play(_closeClip);
            float width = Mathf.Lerp(1f, _openWidth, _open);
            SetWidth(_doorA, width);
            SetWidth(_doorB, width);
        }

        /// Finds the loadable items in the intake, and listens for a hand letting go of each.
        private void Look()
        {
            _inside.Clear();
            var box = _intake as BoxCollider;
            var t = _intake.transform;
            Vector3 centre = box != null ? t.TransformPoint(box.center) : _intake.bounds.center;
            Vector3 half = box != null ? Vector3.Scale(box.size * 0.5f, t.lossyScale) : _intake.bounds.extents;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            int count = Physics.OverlapBoxNonAlloc(centre, half, Found, t.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var item = Found[i] != null ? Found[i].GetComponentInParent<LoadableItem>() : null;
                if (item == null || item.Picture || item == _drawing || _inside.Contains(item)) continue;
                _inside.Add(item);
                if (_watched.Add(item)) GrabOf(item).selectExited.AddListener(OnLetGo);
            }
        }

        /// A hand on this client let go of an item this port has seen: draws it in, if it is still in the intake
        /// and wanted.
        private void OnLetGo(SelectExitEventArgs args)
        {
            var item = args.interactableObject.transform.GetComponentInParent<LoadableItem>();
            if (item == null || _drawing != null || GrabOf(item).isSelected) return;
            Look();
            if (!_inside.Contains(item)) return;
            var loading = LoaderFor(item);
            if (loading != null) StartCoroutine(DrawIn(item, loading));
        }

        /// The LoadingModule that would take this item now: one accepting its type, holding none.
        private LoadingModule LoaderFor(LoadableItem item)
        {
            if (_loading == null) return null;
            foreach (var loading in _loading)
                if (loading.Accepts(item) && !loading.HasItem) return loading;
            return null;
        }

        /// Lines the item up with the port, draws it in end first, loads it, and removes it once the doors shut.
        private IEnumerator DrawIn(LoadableItem item, LoadingModule loading)
        {
            _drawing = item;
            var grab = GrabOf(item);
            grab.enabled = false;
            var body = item.Body;
            if (body != null) body.isKinematic = true;
            foreach (var c in item.GetComponentsInChildren<Collider>()) c.enabled = false;

            var port = transform;
            float half = HalfLength(item);
            var lined = Quaternion.LookRotation(port.up, -port.forward);   // its long axis pointing into the port
            var mouth = port.position + port.forward * (half + 0.02f);
            var gone = port.position - port.forward * (half + 0.05f);

            var fromAt = item.transform.position;
            var fromTurn = item.transform.rotation;
            for (float t = 0f; t < 1f && item != null; t += Time.fixedDeltaTime / Mathf.Max(0.01f, _alignSeconds))
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                Move(item, body, Vector3.Lerp(fromAt, mouth, k), Quaternion.Slerp(fromTurn, lined, k));
                yield return new WaitForFixedUpdate();
            }
            for (float t = 0f; t < 1f && item != null; t += Time.fixedDeltaTime / Mathf.Max(0.01f, _drawInSeconds))
            {
                Move(item, body, Vector3.Lerp(mouth, gone, t), lined);
                yield return new WaitForFixedUpdate();
            }
            if (item == null) { _drawing = null; yield break; }

            Move(item, body, gone, lined);
            loading.LoadByHand(item);
            _drawing = null;
            _inside.Remove(item);
            yield return new WaitForSeconds(_doorSeconds);

            _watched.Remove(item);
            if (item == null) yield break;
            grab.selectExited.RemoveListener(OnLetGo);
            item.RemoveForEveryone();
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && _sound != null) _sound.PlayOneShot(clip);
        }

        private static void Move(LoadableItem item, Rigidbody body, Vector3 at, Quaternion turn)
        {
            if (item == null) return;
            if (body != null) { body.MovePosition(at); body.MoveRotation(turn); }
            else item.transform.SetPositionAndRotation(at, turn);
        }

        /// Half the item's length along its own up, from its box collider; half a metre without one.
        private static float HalfLength(LoadableItem item) =>
            item.TryGetComponent<BoxCollider>(out var box) ? box.size.y * 0.5f * Mathf.Abs(item.transform.lossyScale.y) : 0.5f;

        private static XRGrabInteractable GrabOf(LoadableItem item) => item.Grab;

        private static void SetWidth(Transform door, float width)
        {
            if (door == null) return;
            var s = door.localScale;
            if (!Mathf.Approximately(s.x, width)) door.localScale = new Vector3(width, s.y, s.z);
        }
    }
}
