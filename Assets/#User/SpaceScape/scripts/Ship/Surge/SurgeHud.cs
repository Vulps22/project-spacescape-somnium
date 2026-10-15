using SomniumSpace.Worlds.SpaceScape.Player;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The surge's heads-up display (docs/tutorial-proposal.md, Plan D): the spoken line as a subtitle at the bottom of
    /// the view, then a white box saying what to do until it is done, and an arrow at the edge of the view toward the
    /// step's target while that is out of view. Told what to show by SurgeScript. Local: nothing is sent.
    public sealed class SurgeHud : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_FontAsset _font;
        [Tooltip("The font's material on an overlay shader, so walls in front of the view do not hide the text.")]
        [SerializeField] private Material _textMaterial;
        [SerializeField] private float _fontSize = 0.35f;
        [Tooltip("Metres: how wide a line runs before it wraps.")]
        [SerializeField] private float _width = 0.8f;

        [Header("Placement")]
        [Tooltip("Metres in front of the eyes.")]
        [SerializeField] private float _distance = 1.2f;
        [Tooltip("Degrees below the middle of the view.")]
        [SerializeField] private float _below = 20f;
        [Tooltip("How quickly the text catches up with the head: higher is stiffer.")]
        [SerializeField] private float _follow = 4f;

        [Header("Box and arrow")]
        [Tooltip("An unlit white material for the task box and the arrow.")]
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private float _lineWidth = 0.004f;
        [Tooltip("Metres between the task's text and its box.")]
        [SerializeField] private float _padding = 0.03f;
        [Tooltip("The arrow shows while the target is more than this many degrees from where the head points.")]
        [SerializeField] private float _arrowDegrees = 30f;
        [Tooltip("Degrees from the middle of the view the arrow sits, toward the target.")]
        [SerializeField] private float _arrowOut = 15f;
        [Tooltip("Metres from the arrow's tip to its tail.")]
        [SerializeField] private float _arrowSize = 0.12f;
        [Tooltip("The arrow's line width, in metres.")]
        [SerializeField] private float _arrowWidth = 0.012f;

        private Transform _root;
        private TextMeshPro _text;
        private LineRenderer _box;
        private LineRenderer _arrow;
        private Quaternion _facing = Quaternion.identity;
        private bool _placed;
        private Transform _target;
        private bool _boxed;

        private void Awake()
        {
            _root = new GameObject("HUD").transform;
            _root.SetParent(transform, false);
            _text = new GameObject("Text").AddComponent<TextMeshPro>();
            _text.transform.SetParent(_root, false);
            if (_font != null) _text.font = _font;
            if (_textMaterial != null) _text.fontSharedMaterial = _textMaterial;
            _text.fontSize = _fontSize;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.rectTransform.sizeDelta = new Vector2(_width, 0.3f);
            _box = MakeLine("Task Box", 5, _root);
            _box.loop = false;
            _arrow = MakeLine("Arrow", 3, transform);
            _arrow.widthMultiplier = _arrowWidth;
            Show(null, false);
            _arrow.enabled = false;
        }

        private LineRenderer MakeLine(string name, int points, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = points;
            line.widthMultiplier = _lineWidth;
            line.sharedMaterial = _lineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        /// The spoken line, as a subtitle with no box.
        public void Say(string line) => Show(line, false);

        /// What to do, in a box, until it is done.
        public void Task(string task) => Show(task, true);

        /// Points at target whenever it is out of view; null for no arrow.
        public void Point(Transform target) => _target = target;

        /// Nothing shown.
        public void Clear()
        {
            Show(null, false);
            _target = null;
        }

        private void Show(string text, bool boxed)
        {
            bool any = !string.IsNullOrEmpty(text);
            _boxed = boxed && any;
            _text.text = any ? text : "";
            _text.enabled = any;
            _box.enabled = _boxed;
            if (any) _text.ForceMeshUpdate();
        }

        private void LateUpdate()
        {
            if (!PlayerHands.TryHeadRotation(out var head, out var rotation))
            {
                _root.gameObject.SetActive(false);
                _arrow.enabled = false;
                return;
            }
            _root.gameObject.SetActive(true);

            // The text trails the head a little, so it does not judder with every small turn.
            _facing = _placed ? Quaternion.Slerp(_facing, rotation, 1f - Mathf.Exp(-_follow * Time.deltaTime)) : rotation;
            _placed = true;
            // Below the middle of the view, but square to the face, so the box does not look like a trapezoid.
            var down = _facing * Quaternion.Euler(_below, 0f, 0f);
            _root.SetPositionAndRotation(head + down * Vector3.forward * _distance, _facing);
            if (_boxed) DrawBox();

            DrawArrow(head, rotation);
        }

        /// A rectangle round the text as it is laid out, in the text's own plane.
        private void DrawBox()
        {
            var b = _text.textBounds;
            float hw = b.extents.x + _padding, hh = b.extents.y + _padding;
            var c = _text.transform.TransformPoint(b.center);
            var right = _root.right * hw;
            var up = _root.up * hh;
            _box.SetPosition(0, c - right - up);
            _box.SetPosition(1, c + right - up);
            _box.SetPosition(2, c + right + up);
            _box.SetPosition(3, c - right + up);
            _box.SetPosition(4, c - right - up);
        }

        /// A chevron near the middle of the view, on the side the target lies, pointing that way. Follows the head
        /// exactly: a trailing arrow would point wrong.
        private void DrawArrow(Vector3 head, Quaternion rotation)
        {
            var to = _target != null ? _target.position - head : Vector3.zero;
            if (_target == null || Vector3.Angle(rotation * Vector3.forward, to) <= _arrowDegrees)
            {
                _arrow.enabled = false;
                return;
            }
            var local = Quaternion.Inverse(rotation) * to;
            var flat = new Vector2(local.x, local.y);
            var d = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector2.right;   // straight behind: turn right
            var along = rotation * new Vector3(d.x, d.y, 0f);
            var across = rotation * new Vector3(-d.y, d.x, 0f);
            var centre = head + rotation * Vector3.forward * _distance + along * (_distance * Mathf.Tan(_arrowOut * Mathf.Deg2Rad));
            float s = _arrowSize * 0.5f;
            _arrow.SetPosition(0, centre - along * s + across * s);
            _arrow.SetPosition(1, centre + along * s);
            _arrow.SetPosition(2, centre - along * s - across * s);
            _arrow.enabled = true;
        }
    }
}
