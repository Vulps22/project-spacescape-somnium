using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEditor;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.EditorTools
{
    /// Draws the ship's conduits in the Scene view. Select the object carrying the ConduitLayout, pick a
    /// mode in its Inspector, then click or drag in the Scene view. Alt and the other mouse buttons still
    /// move the camera.
    [CustomEditor(typeof(ConduitLayout))]
    public sealed class ConduitLayoutEditor : Editor
    {
        private enum Mode { Off, Draw, Erase, Select }
        private enum PlaneAxis { Floor, WallAcrossZ, WallAcrossX }

        private static readonly GridDirection[] Faces =
        {
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
            GridDirection.ZPlus, GridDirection.ZMinus,
        };

        private static readonly GridDirection[] FacingPreference =
        {
            GridDirection.ZPlus, GridDirection.ZMinus,
            GridDirection.XPlus, GridDirection.XMinus,
            GridDirection.YPlus, GridDirection.YMinus,
        };

        // A hair past the half cell, so a surface lying exactly on a cell's centre still picks a side.
        private const float Reach = GridCell.Half + GridCell.Size * 0.02f;

        private static Mode _mode;
        private static PlaneAxis _plane;
        private static int _planeCell;
        private static GridDirection _selectedNormal;

        private Vector3Int? _last;
        private int _undoGroup;

        private ConduitLayout Layout => (ConduitLayout)target;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndo;
            SceneView.duringSceneGui += OnScene;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            SceneView.duringSceneGui -= OnScene;
            Tools.hidden = false;
        }

        private void OnUndo()
        {
            if (target == null) return;
            Layout.Invalidate();
            Changed();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "_cells");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{Layout.Count} conduits drawn", EditorStyles.boldLabel);
            _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Off", "Draw", "Erase", "Select" });
            EditorGUILayout.HelpBox(Help(), MessageType.None);

            _plane = (PlaneAxis)EditorGUILayout.EnumPopup(new GUIContent("Working plane",
                "Where drawing lands when the pointer is over nothing, or while Shift is held: a floor (flat), or a wall across Z or across X."), _plane);
            _planeCell = EditorGUILayout.IntField(new GUIContent("Plane at cell",
                $"Which layer of cells the working plane runs through. Cell n is at n x {GridCell.Size} m."), _planeCell);
            EditorGUILayout.LabelField(" ", $"= {_planeCell * GridCell.Size} m");

            SelectedPanel();
        }

        private static string Help()
        {
            switch (_mode)
            {
                case Mode.Draw: return "Drag to lay a run. Power flows the way you drag: each cell sends Out to the next, which takes it In. Start on an existing conduit to branch. Clicking a wall puts the cable in the cell behind it; clicking a placed tile puts it in the cell in front. Shift draws on the working plane only.";
                case Mode.Erase: return "Click or drag to rub out conduits. Neighbours stop pointing at them.";
                case Mode.Select: return "Click a conduit to set its faces and its addon below. An addon added here faces the surface you clicked.";
                default: return "Pick a mode to draw in the Scene view.";
            }
        }

        /// The selected conduit's faces and addon, editable.
        private void SelectedPanel()
        {
            var selected = Layout.Selected;
            if (!selected.HasValue) return;
            if (!Layout.TryGet(selected.Value, out var code)) { Layout.Selected = null; return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected conduit", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Cell", $"{selected.Value}   at {GridCell.ToWorld(selected.Value)} m");

            EditorGUI.BeginChangeCheck();
            foreach (var face in Faces)
                code.SetFace(face, (FlowDirection)EditorGUILayout.EnumPopup(
                    new GUIContent(face.ToString(), "In takes power from the neighbour on this side, Out sends it there, None is sealed."),
                    code.GetFace(face)));

            var names = new string[Layout.Addons.Count + 1];
            names[0] = "None";
            for (int i = 0; i < Layout.Addons.Count; i++)
                names[i + 1] = Layout.Addons[i] != null ? Layout.Addons[i].name : $"(missing {i + 1})";
            int addon = EditorGUILayout.Popup(new GUIContent("Addon", "Installed on this conduit when the world starts. The list is the layout's Addons."),
                Mathf.Clamp(code.Addon, 0, names.Length - 1), names);

            if (addon != 0)
            {
                if (code.Addon == 0) code.AddonFacing = DefaultFacing(code);
                code.AddonFacing = (GridDirection)EditorGUILayout.EnumPopup(
                    new GUIContent("Addon facing", "The side of the conduit it faces. Should be one no cable uses."), code.AddonFacing);
                code.AddonTurns = EditorGUILayout.IntSlider(
                    new GUIContent("Addon quarter turns", "Turns about the way it faces. 0 is upright on a wall; on a floor or ceiling, its top is toward +Z."),
                    code.AddonTurns, 0, 3);
                code.AddonParked = EditorGUILayout.Toggle(
                    new GUIContent("Addon parked", "Tucked into the cable and off; the conduit is plain cable until a hand pulls it out."), code.AddonParked);
                if (code.AddonFacing == GridDirection.None || code.GetFace(code.AddonFacing) != FlowDirection.None)
                    EditorGUILayout.HelpBox("The addon faces a side a cable uses, or no side at all.", MessageType.Warning);
            }
            code.Addon = addon;

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(Layout, "Edit conduit");
                Layout.Set(code);
                Changed();
            }

            if (GUILayout.Button("Rub out this conduit"))
            {
                Undo.RecordObject(Layout, "Rub out conduit");
                Layout.Remove(selected.Value);
                Layout.Selected = null;
                Changed();
            }
        }

        /// The side a newly added addon faces: the surface that was clicked if no cable uses it, else the first free side.
        private static GridDirection DefaultFacing(ConduitCode code)
        {
            if (_selectedNormal != GridDirection.None && code.GetFace(_selectedNormal) == FlowDirection.None) return _selectedNormal;
            foreach (var face in FacingPreference)
                if (code.GetFace(face) == FlowDirection.None) return face;
            return GridDirection.ZPlus;
        }

        /// Hooked straight into the Scene view rather than through OnSceneGUI, which Unity skips while the
        /// component is folded in the Inspector.
        private void OnScene(SceneView view)
        {
            if (target == null) return;
            Tools.hidden = _mode != Mode.Off;
            if (_mode == Mode.Off) return;
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            // Claimed at distance 0, not as a default control: the rectangle select also asks to be the
            // default, and whichever asks last would win.
            if (e.type == EventType.Layout) HandleUtility.AddControl(id, 0f);

            bool picked = Pick(e.mousePosition, e.shift, out var cell, out var normal);
            if (picked && e.type == EventType.Repaint)
            {
                Handles.color = _mode == Mode.Erase ? new Color(1f, 0.3f, 0.3f) : _mode == Mode.Select ? Color.white : new Color(1f, 0.85f, 0.3f);
                Handles.DrawWireCube(GridCell.ToWorld(cell), Vector3.one * GridCell.Size);
            }
            if (e.type == EventType.MouseMove) SceneView.RepaintAll();
            if (e.alt || e.button != 0) return;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (!picked) break;
                    GUIUtility.hotControl = id;
                    Undo.IncrementCurrentGroup();
                    _undoGroup = Undo.GetCurrentGroup();
                    Undo.RecordObject(Layout, _mode + " conduits");
                    if (_mode == Mode.Draw)
                    {
                        if (!Layout.TryGet(cell, out _)) Layout.Set(new ConduitCode { Cell = cell });
                        _last = cell;
                    }
                    else if (_mode == Mode.Erase) Erase(cell);
                    else
                    {
                        Layout.Selected = Layout.TryGet(cell, out _) ? cell : (Vector3Int?)null;
                        _selectedNormal = normal;
                    }
                    Changed();
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id || !picked) break;
                    Undo.RecordObject(Layout, _mode + " conduits");
                    if (_mode == Mode.Draw && _last.HasValue) DrawTo(cell);
                    else if (_mode == Mode.Erase) Erase(cell);
                    Changed();
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    _last = null;
                    Undo.CollapseUndoOperations(_undoGroup);
                    e.Use();
                    break;
            }
        }

        /// Walks from the last cell to the pointer's, one axis step at a time, joining each step the way it went.
        private void DrawTo(Vector3Int target)
        {
            var at = _last.Value;
            for (int guard = 0; at != target && guard < 256; guard++)
            {
                var d = target - at;
                var step = Mathf.Abs(d.x) >= Mathf.Abs(d.y) && Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? new Vector3Int(System.Math.Sign(d.x), 0, 0)
                    : Mathf.Abs(d.y) >= Mathf.Abs(d.z) ? new Vector3Int(0, System.Math.Sign(d.y), 0)
                    : new Vector3Int(0, 0, System.Math.Sign(d.z));
                var next = at + step;
                Join(at, next, FaceOf(step));
                at = next;
            }
            _last = at;
        }

        /// Sends power from one cell to the next across a face: Out on the first, In on the second.
        private void Join(Vector3Int from, Vector3Int to, GridDirection face)
        {
            if (!Layout.TryGet(from, out var a)) a = new ConduitCode { Cell = from };
            a.SetFace(face, FlowDirection.Out);
            Layout.Set(a);
            if (!Layout.TryGet(to, out var b)) b = new ConduitCode { Cell = to };
            b.SetFace(GridNode.Opposite(face), FlowDirection.In);
            Layout.Set(b);
        }

        private void Erase(Vector3Int cell)
        {
            Layout.Remove(cell);
            if (Layout.Selected == cell) Layout.Selected = null;
        }

        /// The cell under the pointer. Over a surface: the cell behind it, or in front of it for a placed tile.
        /// Over nothing, or with Shift held: the cell on the working plane.
        private static bool Pick(Vector2 mouse, bool planeOnly, out Vector3Int cell, out GridDirection normal)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            if (!planeOnly)
            {
                Physics.SyncTransforms();
                if (Physics.Raycast(ray, out var hit, 1000f))
                {
                    normal = Nearest(hit.normal);

                    // A container wall: straight into its channel, whichever skin or face was hit, so a wall
                    // with one side hidden still lands the cable in the middle rather than behind it.
                    var container = hit.collider.GetComponentInParent<ConduitContainer>();
                    if (container != null)
                    {
                        var across = container.transform.forward;
                        var inChannel = hit.point - across * Vector3.Dot(hit.point - container.transform.position, across);
                        cell = GridCell.ToCell(inChannel);
                        // The side facing whoever is drawing, which a new addon faces.
                        normal = Nearest(Vector3.Dot(ray.direction, across) < 0f ? across : -across);
                        return true;
                    }

                    bool tile = hit.collider.GetComponentInParent<GridNode>() != null;
                    cell = GridCell.ToCell(hit.point + normal.Vector() * (tile ? Reach : -Reach));
                    return true;
                }
            }

            normal = GridDirection.None;
            float at = _planeCell * GridCell.Size;
            var plane = _plane == PlaneAxis.Floor ? new Plane(Vector3.up, new Vector3(0f, at, 0f))
                : _plane == PlaneAxis.WallAcrossZ ? new Plane(Vector3.forward, new Vector3(0f, 0f, at))
                : new Plane(Vector3.right, new Vector3(at, 0f, 0f));
            if (plane.Raycast(ray, out float distance))
            {
                cell = GridCell.ToCell(ray.GetPoint(distance));
                return true;
            }
            cell = default;
            return false;
        }

        /// The grid face nearest a direction.
        private static GridDirection Nearest(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            if (x >= y && x >= z) return v.x >= 0f ? GridDirection.XPlus : GridDirection.XMinus;
            if (y >= z) return v.y >= 0f ? GridDirection.YPlus : GridDirection.YMinus;
            return v.z >= 0f ? GridDirection.ZPlus : GridDirection.ZMinus;
        }

        private static GridDirection FaceOf(Vector3Int step)
        {
            if (step.x > 0) return GridDirection.XPlus;
            if (step.x < 0) return GridDirection.XMinus;
            if (step.y > 0) return GridDirection.YPlus;
            if (step.y < 0) return GridDirection.YMinus;
            return step.z > 0 ? GridDirection.ZPlus : GridDirection.ZMinus;
        }

        private void Changed()
        {
            EditorUtility.SetDirty(Layout);
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
