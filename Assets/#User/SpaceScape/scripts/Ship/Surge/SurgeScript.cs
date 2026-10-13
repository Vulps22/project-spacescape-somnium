using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The surge (docs/tutorial-proposal.md, Plan D): a cube in the bay; a hand through it starts a voice that walks
    /// the player through fixing the broken bay, one line at a time. Line N plays stepN from the sounds folder; the
    /// next line plays once this one has finished and its wait is met. Local to this client: nothing is sent.
    public sealed class SurgeScript : MonoBehaviour
    {
        /// What a line waits for before the next one plays. Each reads the step's Target (and Face or Addon).
        public enum Wait
        {
            None,            // the line finishing
            Pause,           // Seconds after the line finishes
            Seen,            // Target within Seen Degrees of where the head looks, nothing solid between, for Seen Seconds
            PanelOpen,       // the AccessPanel on or above Target open
            HologramOpen,    // the conduit in Target's cell open in a hologram
            CellMended,      // the conduit in Target's cell installed and not popped
            ReactorRunning,  // the reactor on or above Target producing
            AddonHeld,       // an addon item (of Addon, if set) in a hand
            AddonInstalled,  // an addon (Addon, if set) in the conduit in Target's cell
            SlotUnlocked,    // the slot on or above Target unlocked
            SlotEmpty,       // that slot holding nothing
            SlotLocked,      // that slot holding a component, locked
            ConduitOnFace,   // a conduit installed in Target's cell, with a segment on Face (any, for None)
            SegmentIn,       // the conduit in Target's cell with an In segment on Face
            Powered,         // the conduit in Target's cell carrying power
        }

        [Serializable]
        public sealed class Step
        {
            [Tooltip("The line, as spoken. For reading in the Inspector; the voice is the sound file.")]
            [TextArea(1, 4)] public string Line;
            public Wait Wait;
            [Tooltip("What the wait reads: the thing to see, a marker in a conduit's cell, the panel, slot or reactor.")]
            public Transform Target;
            [Tooltip("ConduitOnFace and SegmentIn: the face of Target's cell.")]
            public GridDirection Face = GridDirection.None;
            [Tooltip("AddonHeld and AddonInstalled: the addon wanted. Empty: any.")]
            public ConduitAddon Addon;
            [Tooltip("Pause: seconds after the line finishes.")]
            public float Seconds = 2f;
            [Tooltip("Turned on as this line starts: the delivered battery, say.")]
            public GameObject Reveal;
            [Tooltip("Marked with a white box round their renderers while this line waits: components.")]
            public Transform[] Boxes = Array.Empty<Transform>();
            [Tooltip("Marked with a white laser standing above them while this line waits: conduits, items, addons, ports.")]
            public Transform[] Lasers = Array.Empty<Transform>();
        }

        [Header("Start")]
        [Tooltip("The cube: a hand inside its collider's bounds starts the script, and the cube goes.")]
        [SerializeField] private Collider _cube;

        [Header("Voice")]
        [Tooltip("Plays the lines: an AudioSource on a child object on the Environment layer.")]
        [SerializeField] private AudioSource _voice;
        [Tooltip("Where step1, step2 ... are found, filled into Clips in the Editor.")]
        [SerializeField] private string _soundsFolder = "Assets/#User/SpaceScape/Sounds/Surge";
        [Tooltip("Clip N-1 is stepN. Filled from the sounds folder in the Editor.")]
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();

        [Header("Seen")]
        [SerializeField] private float _seenDegrees = 15f;
        [SerializeField] private float _seenSeconds = 0.5f;
        [Tooltip("Metres: anything further is not seen.")]
        [SerializeField] private float _seenReach = 15f;

        [Header("Highlights")]
        [Tooltip("An unlit white material for the boxes and lasers.")]
        [SerializeField] private Material _highlightMaterial;
        [SerializeField] private float _lineWidth = 0.015f;
        [Tooltip("Metres the laser stands above what it marks.")]
        [SerializeField] private float _laserHeight = 2f;

        [Header("Lines")]
        [SerializeField] private List<Step> _steps = new List<Step>();

        private static readonly Vector3[] Hands = new Vector3[8];
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private int _at = -1;          // the line playing or waiting; -1 before the cube, Count when finished
        private float _lineEnds;
        private float _seenFor;
        private bool _warned;
        private readonly List<LineRenderer> _boxes = new List<LineRenderer>();
        private readonly List<LineRenderer> _lasers = new List<LineRenderer>();

        /// The line playing or waiting, from 1; 0 before the cube is hit.
        public int Line => _at < 0 ? 0 : Mathf.Min(_at + 1, _steps.Count);

        private void Update()
        {
            if (_at < 0)
            {
                if (HandInCube()) Begin(0);
                return;
            }
            if (_at >= _steps.Count) return;

            var step = _steps[_at];
            UpdateHighlights(step);
            if (Time.time >= _lineEnds && Met(step)) Begin(_at + 1);
        }

        private bool HandInCube()
        {
            if (_cube == null || !_cube.gameObject.activeInHierarchy) return false;
            var bounds = _cube.bounds;
            int count = PlayerHands.Positions(Hands);
            for (int i = 0; i < count; i++)
                if (bounds.Contains(Hands[i])) return true;
            return false;
        }

        private void Begin(int index)
        {
            ClearHighlights();
            _at = index;
            _seenFor = 0f;
            _warned = false;
            if (index == 0 && _cube != null) _cube.gameObject.SetActive(false);
            if (index >= _steps.Count)
            {
                Debug.Log("[SpaceScape] Surge: finished", this);
                return;
            }

            var step = _steps[index];
            if (step.Reveal != null) step.Reveal.SetActive(true);
            var clip = index < _clips.Length ? _clips[index] : null;
            if (clip != null && _voice != null)
            {
                _voice.Stop();
                _voice.clip = clip;
                _voice.Play();
                _lineEnds = Time.time + clip.length;
            }
            else
            {
                _lineEnds = Time.time;
                Debug.LogWarning($"[SpaceScape] Surge: no sound for step{index + 1}{(_voice == null ? " (no voice source)" : "")}", this);
            }
            MakeHighlights(step);
            Debug.Log($"[SpaceScape] Surge: step{index + 1}, waits for {step.Wait}", this);
        }

        private bool Met(Step step)
        {
            var cells = ConduitCells.Current;
            var cell = step.Target != null ? GridCell.ToCell(step.Target.position) : default;
            var c = cells != null && step.Target != null ? cells.Get(cell) : null;
            int bit = ConduitCells.Bit(step.Face);
            switch (step.Wait)
            {
                case Wait.None: return true;
                case Wait.Pause: return Time.time >= _lineEnds + step.Seconds;
                case Wait.Seen: return Seen(step.Target);
                case Wait.PanelOpen: return Find<AccessPanel>(step)?.Open ?? true;
                case Wait.HologramOpen: return cells == null || step.Target == null || cells.IsOpen(cell);
                case Wait.CellMended: return c != null && !c.Popped || Missing(step, cells);
                case Wait.ReactorRunning: return Find<ReactorBehaviourModule>(step)?.Reactor.Core.Running ?? true;
                case Wait.AddonHeld:
                    foreach (var item in AddonItem.All)
                        if (item != null && item.Grab != null && item.Grab.isSelected && (step.Addon == null || item.Installs == step.Addon))
                            return true;
                    return false;
                case Wait.AddonInstalled:
                    return c != null && c.Addon != 0 && (step.Addon == null || cells.AddonPrefab(c.Addon) == step.Addon) || Missing(step, cells);
                case Wait.SlotUnlocked: return Find<ComponentSlot>(step)?.Unlocked ?? true;
                case Wait.SlotEmpty:
                {
                    var slot = Find<ComponentSlot>(step);
                    return slot == null || slot.Component == null;
                }
                case Wait.SlotLocked:
                {
                    var slot = Find<ComponentSlot>(step);
                    return slot == null || slot.Component != null && !slot.Unlocked;
                }
                case Wait.ConduitOnFace: return c != null && (bit == 0 || (c.Used & bit) != 0) || Missing(step, cells);
                case Wait.SegmentIn: return c != null && (c.Used & bit) != 0 && (c.Out & bit) == 0 || Missing(step, cells);
                case Wait.Powered:
                {
                    var node = PowerGrid.Current != null && step.Target != null ? PowerGrid.Current.TileAt(cell)?.Node : null;
                    return node != null ? node.Inflow > 0.0 : Missing(step, cells);
                }
            }
            return true;
        }

        /// A wait with nothing to read is met, so a missing marker never strands the player; it says so once.
        private bool Missing(Step step, ConduitCells cells)
        {
            if (step.Target != null && cells != null && PowerGrid.Current != null) return false;
            Warn($"has nothing to read; skipped");
            return true;
        }

        private T Find<T>(Step step) where T : Component
        {
            var found = step.Target != null ? step.Target.GetComponentInParent<T>() : null;
            if (found == null) Warn($"has no {typeof(T).Name} on or above its target; skipped");
            return found;
        }

        private void Warn(string what)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[SpaceScape] Surge: step{_at + 1} ({_steps[_at].Wait}) {what}", this);
        }

        private bool Seen(Transform target)
        {
            if (target == null || !PlayerHands.TryHeadPose(out var head, out var forward)) return true;
            var to = target.position - head;
            float distance = to.magnitude;
            bool seen = distance <= _seenReach && Vector3.Angle(forward, to) <= _seenDegrees && Clear(head, to, distance, target);
            _seenFor = seen ? _seenFor + Time.deltaTime : 0f;
            return _seenFor >= _seenSeconds;
        }

        /// Nothing solid between the head and the target but the target itself and the player's own avatar.
        private static bool Clear(Vector3 head, Vector3 to, float distance, Transform target)
        {
            int count = Physics.RaycastNonAlloc(head, to / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var t = Hits[i].collider.transform;
                if (t.IsChildOf(target) || target.IsChildOf(t) && t.root != t) continue;
                if (Vector3.Distance(Hits[i].point, target.position) < 0.75f) continue;
                if (PlayerHands.IsLocalPlayer(Hits[i].collider)) continue;
                return false;
            }
            return true;
        }

        private void MakeHighlights(Step step)
        {
            foreach (var t in step.Boxes) if (t != null) _boxes.Add(MakeLine("Highlight Box", 16));
            foreach (var t in step.Lasers) if (t != null) _lasers.Add(MakeLine("Highlight Laser", 2));
            UpdateHighlights(step);
        }

        private LineRenderer MakeLine(string name, int points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points;
            line.widthMultiplier = _lineWidth;
            line.sharedMaterial = _highlightMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void UpdateHighlights(Step step)
        {
            int b = 0, l = 0;
            foreach (var t in step.Boxes)
            {
                if (t == null) continue;
                if (b < _boxes.Count) DrawBox(_boxes[b++], BoundsOf(t));
            }
            foreach (var t in step.Lasers)
            {
                if (t == null) continue;
                if (l >= _lasers.Count) continue;
                var line = _lasers[l++];
                line.SetPosition(0, t.position);
                line.SetPosition(1, t.position + Vector3.up * _laserHeight);
            }
        }

        private static Bounds BoundsOf(Transform t)
        {
            var bounds = new Bounds(t.position, Vector3.one * GridCell.Size);
            bool any = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        /// The twelve edges of a box as one line: round the bottom, up, round the top, and back down the other three.
        private static void DrawBox(LineRenderer line, Bounds b)
        {
            Vector3 lo = b.min, hi = b.max;
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
            line.SetPosition(0, P(lo.x, lo.y, lo.z));
            line.SetPosition(1, P(hi.x, lo.y, lo.z));
            line.SetPosition(2, P(hi.x, lo.y, hi.z));
            line.SetPosition(3, P(lo.x, lo.y, hi.z));
            line.SetPosition(4, P(lo.x, lo.y, lo.z));
            line.SetPosition(5, P(lo.x, hi.y, lo.z));
            line.SetPosition(6, P(hi.x, hi.y, lo.z));
            line.SetPosition(7, P(hi.x, hi.y, hi.z));
            line.SetPosition(8, P(lo.x, hi.y, hi.z));
            line.SetPosition(9, P(lo.x, hi.y, lo.z));
            line.SetPosition(10, P(lo.x, hi.y, hi.z));
            line.SetPosition(11, P(lo.x, lo.y, hi.z));
            line.SetPosition(12, P(hi.x, lo.y, hi.z));
            line.SetPosition(13, P(hi.x, hi.y, hi.z));
            line.SetPosition(14, P(hi.x, hi.y, lo.z));
            line.SetPosition(15, P(hi.x, lo.y, lo.z));
        }

        private void ClearHighlights()
        {
            foreach (var line in _boxes) if (line != null) Destroy(line.gameObject);
            foreach (var line in _lasers) if (line != null) Destroy(line.gameObject);
            _boxes.Clear();
            _lasers.Clear();
        }

        /// For the Editor tools (SurgeScriptMenu): the sounds folder, the lines, and setting them.
        public string SoundsFolder => _soundsFolder;
        public int StepCount => _steps.Count;
        public void SetClips(AudioClip[] clips) => _clips = clips;
        public void SetSteps(List<Step> steps) => _steps = steps;
    }
}
