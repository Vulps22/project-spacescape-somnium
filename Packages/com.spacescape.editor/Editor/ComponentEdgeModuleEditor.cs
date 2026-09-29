using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEditor;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.EditorTools
{
    /// Shows a component's ports face by face and edits them as ports, though they are stored as plain ints
    /// (see ComponentEdgeModule). Hides the whole-face settings it inherits from NodeEdgeModule.
    [CustomEditor(typeof(ComponentEdgeModule))]
    public sealed class ComponentEdgeModuleEditor : Editor
    {
        private static readonly (string field, string label)[] Faces =
        {
            ("_packedXPlus", "+X face"), ("_packedXMinus", "-X face"), ("_packedYPlus", "+Y face (top)"),
            ("_packedYMinus", "-Y face (bottom)"), ("_packedZPlus", "+Z face"), ("_packedZMinus", "-Z face"),
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "Faces and cells are the component's own and turn with it. A port's cell is counted from the face's " +
                "bottom left, looking at it from outside: x right, y up (on the top and bottom faces, up is toward " +
                "the front). One port per cell face. Blue gizmos are In, green Out, red is off its face.",
                MessageType.None);

            foreach (var (field, label) in Faces)
            {
                var list = serializedObject.FindProperty(field);
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                for (int i = 0; i < list.arraySize; i++)
                {
                    var element = list.GetArrayElementAtIndex(i);
                    var port = ComponentEdgeModule.Unpack(element.intValue);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUIUtility.labelWidth = 14f;
                    int x = EditorGUILayout.IntField("x", port.Cell.x, GUILayout.Width(60f));
                    int y = EditorGUILayout.IntField("y", port.Cell.y, GUILayout.Width(60f));
                    EditorGUIUtility.labelWidth = 0f;
                    var flow = (FlowDirection)EditorGUILayout.EnumPopup(port.Flow);
                    bool remove = GUILayout.Button("x", GUILayout.Width(22f));
                    EditorGUILayout.EndHorizontal();
                    if (remove) { list.DeleteArrayElementAtIndex(i); break; }
                    element.intValue = ComponentEdgeModule.Pack(new ComponentEdgeModule.Port
                    {
                        Cell = new Vector2Int(Mathf.Clamp(x, 0, 255), Mathf.Clamp(y, 0, 255)),
                        Flow = flow,
                    });
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add In")) Add(list, FlowDirection.In);
                if (GUILayout.Button("Add Out")) Add(list, FlowDirection.Out);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4f);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_portPrefab"));
            serializedObject.ApplyModifiedProperties();
        }

        private static void Add(SerializedProperty list, FlowDirection flow)
        {
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).intValue =
                ComponentEdgeModule.Pack(new ComponentEdgeModule.Port { Cell = Vector2Int.zero, Flow = flow });
        }
    }
}
