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
            SlotLocked,      // that slot holding a component that is not wrecked, locked
            ConduitOnFace,   // a conduit installed in Target's cell, with a segment on Face (any, for None)
            SegmentIn,       // the conduit in Target's cell with an In segment on Face
            Powered,         // the conduit in Target's cell carrying power
            RunReaches,      // conduits joined from From's cell (in front of a port) to a cell beside Target's, with a segment toward it; any arrows
            RunFlows,        // that run with every arrow pointing from the port toward Target
            FacesRun,        // the conduit in Target's cell with a segment toward where the last run line's run ends
            FacesRunIn,      // and that segment In
        }

        /// One line, put together at runtime from the flat arrays below. Never serialized itself: a Somnium world
        /// build drops a list of a world's own classes (it arrives empty), so the lines are kept as plain arrays.
        public sealed class Step
        {
            [Tooltip("The line, as spoken. For reading in the Inspector; the voice is the sound file.")]
            [TextArea(1, 4)] public string Line;
            [Tooltip("What to do, shown in a box once the line finishes, until its wait is met. Empty: no box.")]
            public string Task;
            public Wait Wait;
            [Tooltip("What the wait reads: the thing to see, a marker in a conduit's cell, the panel, slot or reactor.")]
            public Transform Target;
            [Tooltip("ConduitOnFace and SegmentIn: the face of Target's cell. RunReaches and RunFlows: the face of Target's cell that leads on into what it feeds; the cell there is not the end of the run.")]
            public GridDirection Face = GridDirection.None;
            [Tooltip("AddonHeld and AddonInstalled: the addon wanted. Empty: any.")]
            public ConduitAddon Addon;
            [Tooltip("Pause: seconds after the line finishes.")]
            public float Seconds = 2f;
            [Tooltip("Seen: degrees from where the head points that count as looking at it. 0: Seen Degrees.")]
            public float SeenDegrees;
            [Tooltip("Turned on as this line starts.")]
            public GameObject Reveal;
            [Tooltip("RunReaches and RunFlows: a marker in the cell in front of the port the run starts from.")]
            public Transform From;
            [Tooltip("Not played when its wait is already met as it comes up: something the player may have done early.")]
            public bool SkipIfDone;
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

        [Header("HUD")]
        [Tooltip("Shows the line as a subtitle, then the task in a box, and an arrow toward the target while it is out of view.")]
        [SerializeField] private SurgeHud _hud;

        [Header("Seen")]
        [SerializeField] private float _seenDegrees = 15f;
        [SerializeField] private float _seenSeconds = 0.5f;
        [Tooltip("Metres: anything further is not seen.")]
        [SerializeField] private float _seenReach = 15f;

        [Header("Highlights")]
        [Tooltip("An unlit white material for the boxes and lasers.")]
        [SerializeField] private Material _highlightMaterial;
        [Tooltip("The component boxes' line width, in metres.")]
        [SerializeField] private float _lineWidth = 0.015f;
        [Tooltip("The lasers' width, in metres: a little thicker than a conduit's 3 cm cable.")]
        [SerializeField] private float _laserWidth = 0.04f;
        [Tooltip("Boxes and lasers flash between these two colours.")]
        [SerializeField] private Color _flashA = Color.white;
        [SerializeField] private Color _flashB = new Color(0.3f, 1f, 0.4f);
        [Tooltip("Flashes a second (one white and one green each).")]
        [SerializeField] private float _flashesPerSecond = 2f;
        [Tooltip("Metres the laser stands above what it marks.")]
        [SerializeField] private float _laserHeight = 2f;

        [Header("The surge")]
        [Tooltip("Breaks the bay as line Surge At Line starts, and delivers the battery as line Deliver At Line starts.")]
        [SerializeField] private SurgeBreaks _breaks;
        [SerializeField] private int _surgeAtLine = 1;
        [SerializeField] private int _deliverAtLine = 13;

        [Header("The starter battery")]
        [Tooltip("The slot holding the battery that feeds the reactor's cold start. Its handle cannot be grabbed, and its battery never runs down from the cube until the reactor runs.")]
        [SerializeField] private ComponentSlot _starterSlot;
        [Tooltip("The reactor's slot: once its reactor runs, the starter battery runs down as usual again.")]
        [SerializeField] private ComponentSlot _reactorSlot;

        [Header("Side to side")]
        [Tooltip("Markers in cells whose handles may only land left or right of the player while the tutorial runs: the sparked conduit.")]
        [SerializeField] private Transform[] _sideToSideCells = Array.Empty<Transform>();

        [Header("Kept shut")]
        [Tooltip("Markers in cells whose conduit does not open for a hand from the cube until line Open At Line: the knocked-aside feed end, so it is not mended before the line to it is laid.")]
        [SerializeField] private Transform[] _keptShutCells = Array.Empty<Transform>();
        [SerializeField] private int _openAtLine = 20;

        [Header("Lines (one entry per line, in order; plain arrays so a world build keeps them)")]
        [SerializeField, TextArea(1, 3)] private string[] _lines = Array.Empty<string>();
        [SerializeField] private string[] _tasks = Array.Empty<string>();
        [SerializeField] private int[] _waits = Array.Empty<int>();          // a Wait each
        [SerializeField] private Transform[] _targets = Array.Empty<Transform>();
        [SerializeField] private int[] _faces = Array.Empty<int>();          // a GridDirection each
        [SerializeField] private ConduitAddon[] _addons = Array.Empty<ConduitAddon>();
        [SerializeField] private float[] _seconds = Array.Empty<float>();
        [SerializeField] private float[] _seenDegreesPerLine = Array.Empty<float>();
        [SerializeField] private GameObject[] _reveals = Array.Empty<GameObject>();
        [SerializeField] private Transform[] _froms = Array.Empty<Transform>();
        [SerializeField] private bool[] _skipIfDone = Array.Empty<bool>();
        [Header("Highlights (each with the line it belongs to, from 1)")]
        [SerializeField] private Transform[] _boxTargets = Array.Empty<Transform>();
        [SerializeField] private int[] _boxLines = Array.Empty<int>();
        [SerializeField] private Transform[] _laserTargets = Array.Empty<Transform>();
        [SerializeField] private int[] _laserLines = Array.Empty<int>();

        private List<Step> _steps = new List<Step>();

        private static readonly Vector3[] Hands = new Vector3[8];
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private int _at = -1;          // the line playing or waiting; -1 before the cube, Count when finished
        private BatteryBehaviourModule _starter;   // the starter battery while it is endless
        private float _lineEnds;
        private float _seenFor;
        private bool _warned;
        private bool _tasked;          // the line has finished and its task is up
        private readonly List<LineRenderer> _boxes = new List<LineRenderer>();
        private readonly List<LineRenderer> _lasers = new List<LineRenderer>();
        private LineRenderer _runLaser;   // on the run's open end, or the cell to fix, for the run waits
        private Transform _runMark;       // where that is, for the laser and the HUD's arrow

        private static readonly GridDirection[] Faces =
        {
            GridDirection.XPlus, GridDirection.XMinus, GridDirection.YPlus,
            GridDirection.YMinus, GridDirection.ZPlus, GridDirection.ZMinus,
        };
        private readonly Dictionary<Vector3Int, Vector3Int> _runFrom = new Dictionary<Vector3Int, Vector3Int>();
        private readonly Queue<Vector3Int> _runQueue = new Queue<Vector3Int>();
        private readonly List<Vector3Int> _runPath = new List<Vector3Int>();
        private Step _runStep;            // the last run line to start, which FacesRun follows
        private GridDirection _runToward; // from its Target's cell toward where its run ends; None while it does not reach

        /// The line playing or waiting, from 1; 0 before the cube is hit.
        public int Line => _at < 0 ? 0 : Mathf.Min(_at + 1, _steps.Count);

        private void Awake() => _steps = BuildSteps();

        /// The lines as Steps, from the flat arrays.
        private List<Step> BuildSteps()
        {
            var steps = new List<Step>();
            for (int i = 0; i < _lines.Length; i++)
            {
                var boxes = new List<Transform>();
                var lasers = new List<Transform>();
                for (int b = 0; b < _boxTargets.Length && b < _boxLines.Length; b++) if (_boxLines[b] == i + 1 && _boxTargets[b] != null) boxes.Add(_boxTargets[b]);
                for (int l = 0; l < _laserTargets.Length && l < _laserLines.Length; l++) if (_laserLines[l] == i + 1 && _laserTargets[l] != null) lasers.Add(_laserTargets[l]);
                steps.Add(new Step
                {
                    Line = _lines[i],
                    Task = i < _tasks.Length ? _tasks[i] : "",
                    Wait = i < _waits.Length ? (Wait)_waits[i] : Wait.None,
                    Target = i < _targets.Length ? _targets[i] : null,
                    Face = i < _faces.Length ? (GridDirection)_faces[i] : GridDirection.None,
                    Addon = i < _addons.Length ? _addons[i] : null,
                    Seconds = i < _seconds.Length ? _seconds[i] : 2f,
                    SeenDegrees = i < _seenDegreesPerLine.Length ? _seenDegreesPerLine[i] : 0f,
                    Reveal = i < _reveals.Length ? _reveals[i] : null,
                    From = i < _froms.Length ? _froms[i] : null,
                    SkipIfDone = i < _skipIfDone.Length && _skipIfDone[i],
                    Boxes = boxes.ToArray(),
                    Lasers = lasers.ToArray(),
                });
            }
            return steps;
        }

        // The slot's handle is turned off once, here: a player who unlocked the starter did not know how to put it back.
        private void Start()
        {
            var handle = _starterSlot != null ? _starterSlot.GetComponentInChildren<SlotHandle>(true) : null;
            if (handle != null) handle.Grabbable = false;
            else if (_starterSlot != null) Debug.LogWarning("[SpaceScape] Surge: the starter slot has no handle", this);
        }

        private void Update()
        {
            EndStarterOnceReactorRuns();
            if (_at < 0)
            {
                if (HandInCube()) Begin(0);
                return;
            }
            if (_at >= _steps.Count) return;

            var step = _steps[_at];
            UpdateHighlights(step);
            if (Time.time < _lineEnds) return;
            if (Met(step)) Begin(_at + 1);
            else if (!_tasked)
            {
                _tasked = true;
                if (_hud != null) _hud.Task(step.Task);
            }
        }

        /// A player taking their time must not find the starter flat: endless from the cube until the reactor runs.
        private void StartStarter()
        {
            _starter = _starterSlot != null && _starterSlot.Component != null
                ? _starterSlot.Component.GetComponentInChildren<BatteryBehaviourModule>()
                : null;
            if (_starter == null)
            {
                Debug.LogWarning("[SpaceScape] Surge: no battery in the starter slot to keep charged", this);
                return;
            }
            _starter.Infinite = true;
        }

        /// Endless no longer once the reactor runs, so it cannot be farmed.
        private void EndStarterOnceReactorRuns()
        {
            if (_starter == null) return;
            var reactor = _reactorSlot != null && _reactorSlot.Component != null
                ? _reactorSlot.Component.GetComponentInChildren<ReactorBehaviourModule>()
                : null;
            if (reactor == null || !reactor.Reactor.Core.Running) return;
            _starter.Infinite = false;
            _starter = null;
            Debug.Log("[SpaceScape] Surge: reactor running; the starter battery runs down again", this);
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
            _tasked = false;
            if (index == 0 && _cube != null) _cube.gameObject.SetActive(false);
            if (index == 0)
            {
                StartStarter();
                foreach (var t in _sideToSideCells) if (t != null) ConduitHologram.SideToSide.Add(GridCell.ToCell(t.position));
                foreach (var t in _keptShutCells) if (t != null) ConduitHologram.KeptShut.Add(GridCell.ToCell(t.position));
            }
            if (index + 1 >= _openAtLine) foreach (var t in _keptShutCells) if (t != null) ConduitHologram.KeptShut.Remove(GridCell.ToCell(t.position));
            if (index >= _steps.Count)
            {
                Debug.Log("[SpaceScape] Surge: finished", this);
                if (_hud != null) _hud.Clear();
                foreach (var t in _sideToSideCells) if (t != null) ConduitHologram.SideToSide.Remove(GridCell.ToCell(t.position));
                foreach (var t in _keptShutCells) if (t != null) ConduitHologram.KeptShut.Remove(GridCell.ToCell(t.position));
                return;
            }

            var step = _steps[index];
            if (IsRun(step)) _runStep = step;
            if (step.SkipIfDone && Met(step))
            {
                Debug.Log($"[SpaceScape] Surge: step{index + 1} already done; skipped", this);
                Begin(index + 1);
                return;
            }
            if (step.Reveal != null) step.Reveal.SetActive(true);
            if (_breaks != null && index + 1 == _surgeAtLine) _breaks.Surge();
            if (_breaks != null && index + 1 == _deliverAtLine) _breaks.Deliver();
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
            if (_hud != null)
            {
                _hud.Say(step.Line);
                _hud.Point(step.Wait == Wait.None || step.Wait == Wait.Pause ? null : IsRun(step) ? RunMark : PointAt(step));
            }
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
                case Wait.Seen: return Seen(step.Target, step.SeenDegrees > 0f ? step.SeenDegrees : _seenDegrees);
                case Wait.PanelOpen: return Find<AccessPanel>(step)?.Open ?? false;
                case Wait.HologramOpen: return !Missing(step, cells) && cells.IsOpen(cell);
                case Wait.CellMended: return !Missing(step, cells) && c != null && !c.Popped;
                case Wait.ReactorRunning: return Find<ReactorBehaviourModule>(step)?.Reactor.Core.Running ?? false;
                case Wait.AddonHeld:
                    foreach (var item in AddonItem.All)
                        if (item != null && item.Grab != null && item.Grab.isSelected && (step.Addon == null || item.Installs == step.Addon))
                            return true;
                    return false;
                case Wait.AddonInstalled:
                    return !Missing(step, cells) && c != null && c.Addon != 0 && (step.Addon == null || cells.AddonPrefab(c.Addon) == step.Addon);
                case Wait.SlotUnlocked: return Find<ComponentSlot>(step)?.Unlocked ?? false;
                case Wait.SlotEmpty:
                {
                    var slot = Find<ComponentSlot>(step);
                    return slot != null && slot.Component == null;
                }
                case Wait.SlotLocked:
                {
                    var slot = Find<ComponentSlot>(step);
                    if (slot == null || slot.Component == null || slot.Unlocked) return false;
                    var integrity = slot.Component.GetComponentInChildren<IntegrityModule>();
                    return integrity == null || !integrity.Integrity.IsDestroyed;   // the wrecked one put back does not count
                }
                case Wait.ConduitOnFace: return !Missing(step, cells) && c != null && (bit == 0 || (c.Used & bit) != 0);
                case Wait.SegmentIn: return !Missing(step, cells) && c != null && (c.Used & bit) != 0 && (c.Out & bit) == 0;
                case Wait.Powered:
                {
                    var node = PowerGrid.Current != null && step.Target != null ? PowerGrid.Current.TileAt(cell)?.Node : null;
                    return !Missing(step, cells) && node != null && node.Inflow > 0.0;
                }
                case Wait.RunReaches:
                case Wait.RunFlows:
                {
                    if (Missing(step, cells)) return false;
                    if (step.From == null) { Warn("has no From; holding"); return false; }
                    FollowRun(step, cells, out bool reaches, out bool flows, out _);
                    return step.Wait == Wait.RunReaches ? reaches : flows;
                }
                case Wait.FacesRun:
                case Wait.FacesRunIn:
                {
                    if (Missing(step, cells)) return false;
                    if (_runStep == null) { Warn("has no run line before it; holding"); return false; }
                    FollowRun(_runStep, cells, out _, out _, out _);
                    int toward = ConduitCells.Bit(_runToward);
                    if (c == null || toward == 0 || (c.Used & toward) == 0) return false;
                    return step.Wait == Wait.FacesRun || (c.Out & toward) == 0;
                }
            }
            return true;
        }

        private static bool IsRun(Step step) => (step.Wait == Wait.RunReaches || step.Wait == Wait.RunFlows) && step.From != null;

        private Transform RunMark
        {
            get
            {
                if (_runMark == null)
                {
                    _runMark = new GameObject("Run Mark").transform;
                    _runMark.SetParent(transform, false);
                }
                return _runMark;
            }
        }

        /// A continuity test: follows the power conduits joined to From's cell, whichever way their arrows point, never
        /// through Target's cell. Reaches: the run comes to a cell beside Target's (any but the one on Face, where
        /// Target leads on), with a segment toward it. Flows: it reaches, and every arrow along it points from the port
        /// toward Target. Where, the cell to mark: the first along the run whose arrow points back toward the port;
        /// while it does not reach, the run's cell nearest Target (From's cell while it has no conduit).
        private void FollowRun(Step step, ConduitCells cells, out bool reaches, out bool flows, out Vector3Int where)
        {
            var start = GridCell.ToCell(step.From.position);
            var target = GridCell.ToCell(step.Target.position);
            var onward = target + step.Face.Offset();
            reaches = flows = false;
            where = start;
            _runToward = GridDirection.None;
            var first = cells.Get(start);
            if (first == null || first.Kind != ConduitCells.Kind.Power) return;

            _runFrom.Clear();
            _runQueue.Clear();
            _runFrom[start] = start;
            _runQueue.Enqueue(start);
            int nearest = Distance(start, target);
            var end = start;
            var toTarget = GridDirection.None;
            while (_runQueue.Count > 0)
            {
                var cell = _runQueue.Dequeue();
                int d = Distance(cell, target);
                if (d <= nearest) { nearest = d; where = cell; }   // a tie goes to the cell further along the run
                var c = cells.Get(cell);
                var face0 = FaceBetween(cell, target);
                if (d == 1 && cell != onward && (c.Used & ConduitCells.Bit(face0)) != 0)
                {
                    end = cell;
                    toTarget = face0;
                    break;
                }
                foreach (var face in Faces)
                {
                    if ((c.Used & ConduitCells.Bit(face)) == 0) continue;
                    var next = cell + face.Offset();
                    if (next == target) continue;
                    var n = cells.Get(next);
                    if (n == null || n.Kind != ConduitCells.Kind.Power || _runFrom.ContainsKey(next)) continue;
                    if ((n.Used & ConduitCells.Bit(GridNode.Opposite(face))) == 0) continue;
                    _runFrom[next] = cell;
                    _runQueue.Enqueue(next);
                }
            }
            if (toTarget == GridDirection.None) return;
            var last = cells.Get(end);
            reaches = true;
            _runToward = GridNode.Opposite(toTarget);

            _runPath.Clear();
            for (var cell = end; ; cell = _runFrom[cell])
            {
                _runPath.Add(cell);
                if (cell == start) break;
            }
            _runPath.Reverse();
            // Power comes in from the port, goes out of each cell toward the next, and in at the next.
            foreach (var port in cells.Ports)
                if (port.Front == start && (first.Out & ConduitCells.Bit(port.Into)) != 0) { where = start; return; }
            for (int i = 0; i + 1 < _runPath.Count; i++)
            {
                var a = _runPath[i];
                var b = _runPath[i + 1];
                var face = FaceBetween(a, b);
                if ((cells.Get(a).Out & ConduitCells.Bit(face)) == 0) { where = a; return; }
                if ((cells.Get(b).Out & ConduitCells.Bit(GridNode.Opposite(face))) != 0) { where = b; return; }
            }
            where = end;
            flows = (last.Out & ConduitCells.Bit(toTarget)) != 0;
        }

        private static int Distance(Vector3Int a, Vector3Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);

        private static GridDirection FaceBetween(Vector3Int a, Vector3Int b)
        {
            foreach (var face in Faces) if (a + face.Offset() == b) return face;
            return GridDirection.None;
        }

        /// True when a wait has nothing to read: it is then never met (a skipped line hides a broken one), and says so once.
        private bool Missing(Step step, ConduitCells cells)
        {
            if (step.Target != null && cells != null && PowerGrid.Current != null) return false;
            Warn("has nothing to read; holding");
            return true;
        }

        /// A slot stands for the component in it, when there is one: what is looked at, boxed and read is the component.
        private static Transform Resolve(Transform t)
        {
            if (t != null && t.TryGetComponent<ComponentSlot>(out var slot) && slot.Component != null) return slot.Component.transform;
            return t;
        }

        /// What the arrow points at: the first highlight, which marks what the player has to find, else the target.
        /// A target can sit well away from it (a panel's container has its pivot on the floor, metres from its cube).
        private static Transform PointAt(Step step)
        {
            foreach (var t in step.Lasers) if (t != null) return t;
            foreach (var t in step.Boxes) if (t != null) return Resolve(t);
            return Resolve(step.Target);
        }

        private T Find<T>(Step step) where T : Component
        {
            // The target itself first (a slot is asked as a slot), then the component in it (a reactor in its slot).
            var found = step.Target != null ? step.Target.GetComponentInParent<T>() : null;
            var inside = Resolve(step.Target);
            if (found == null && inside != null && inside != step.Target) found = inside.GetComponentInChildren<T>();
            if (found == null) Warn($"has no {typeof(T).Name} on or above its target; holding");
            return found;
        }

        private void Warn(string what)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[SpaceScape] Surge: step{_at + 1} ({_steps[_at].Wait}) {what}", this);
        }

        private bool Seen(Transform target, float degrees)
        {
            if (target == null || !PlayerHands.TryHeadPose(out var head, out var forward)) return true;
            var to = target.position - head;
            float distance = to.magnitude;
            bool seen = distance <= _seenReach && Vector3.Angle(forward, to) <= degrees && Clear(head, to, distance, Resolve(target));
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
            foreach (var t in step.Boxes) if (t != null) _boxes.Add(MakeLine("Highlight Box", 16, _lineWidth));
            foreach (var t in step.Lasers) if (t != null) _lasers.Add(MakeLine("Highlight Laser", 2, _laserWidth));
            if (IsRun(step)) _runLaser = MakeLine("Highlight Laser (run)", 2, _laserWidth);
            UpdateHighlights(step);
        }

        private LineRenderer MakeLine(string name, int points, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.sharedMaterial = _highlightMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private MaterialPropertyBlock _flash;

        private void UpdateHighlights(Step step)
        {
            if (_flash == null) _flash = new MaterialPropertyBlock();
            _flash.SetColor("_BaseColor", Mathf.Repeat(Time.time * _flashesPerSecond, 1f) < 0.5f ? _flashA : _flashB);
            foreach (var line in _boxes) if (line != null) line.SetPropertyBlock(_flash);
            foreach (var line in _lasers) if (line != null) line.SetPropertyBlock(_flash);
            if (_runLaser != null && ConduitCells.Current != null && step.Target != null)
            {
                FollowRun(step, ConduitCells.Current, out _, out _, out var where);
                var at = GridCell.ToWorld(where);
                RunMark.position = at;
                _runLaser.SetPropertyBlock(_flash);
                _runLaser.SetPosition(0, at);
                _runLaser.SetPosition(1, at + Vector3.up * _laserHeight);
            }
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
            t = Resolve(t);
            var bounds = new Bounds(t.position, Vector3.one * GridCell.Size);
            bool any = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                // The model only: not particles (a reactor's smoke sits at the origin), lines, or anything hidden.
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || r.forceRenderingOff || r.bounds.size.sqrMagnitude <= 0f) continue;
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
            if (_runLaser != null) Destroy(_runLaser.gameObject);
            _runLaser = null;
        }

        /// For the Editor tools (SurgeScriptMenu): the sounds folder, the lines, and setting them.
        public string SoundsFolder => _soundsFolder;
        public int StepCount => _lines.Length;
        public void SetClips(AudioClip[] clips) => _clips = clips;
        public List<Step> GetSteps() => BuildSteps();

        /// Stores the lines as the flat arrays a world build keeps.
        public void SetSteps(List<Step> steps)
        {
            int n = steps.Count;
            _lines = new string[n]; _tasks = new string[n]; _waits = new int[n]; _targets = new Transform[n]; _faces = new int[n];
            _addons = new ConduitAddon[n]; _seconds = new float[n]; _seenDegreesPerLine = new float[n]; _reveals = new GameObject[n];
            _froms = new Transform[n]; _skipIfDone = new bool[n];
            var boxes = new List<Transform>(); var boxLines = new List<int>();
            var lasers = new List<Transform>(); var laserLines = new List<int>();
            for (int i = 0; i < n; i++)
            {
                var s = steps[i];
                _lines[i] = s.Line; _tasks[i] = s.Task; _waits[i] = (int)s.Wait; _targets[i] = s.Target; _faces[i] = (int)s.Face;
                _addons[i] = s.Addon; _seconds[i] = s.Seconds; _seenDegreesPerLine[i] = s.SeenDegrees; _reveals[i] = s.Reveal;
                _froms[i] = s.From; _skipIfDone[i] = s.SkipIfDone;
                foreach (var t in s.Boxes) if (t != null) { boxes.Add(t); boxLines.Add(i + 1); }
                foreach (var t in s.Lasers) if (t != null) { lasers.Add(t); laserLines.Add(i + 1); }
            }
            _boxTargets = boxes.ToArray(); _boxLines = boxLines.ToArray();
            _laserTargets = lasers.ToArray(); _laserLines = laserLines.ToArray();
            _steps = BuildSteps();
        }

        public void SetBreaks(SurgeBreaks breaks) => _breaks = breaks;

        public void SetHud(SurgeHud hud) => _hud = hud;

        /// Sets the task lines alone, one per line, leaving everything else as set in the scene.
        public void SetTasks(string[] tasks)
        {
            _tasks = tasks;
            _steps = BuildSteps();
        }
    }
}
