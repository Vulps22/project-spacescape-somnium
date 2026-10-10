using System.IO;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEditor;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.EditorTools
{
    /// Builds Prefabs/Components/Reactor Draft: the S5 reactor's new model (Blender/Reactors/Reactor.py,
    /// docs/component-models.md) as a slotted S5 component with its ports and seven levers, and nothing else working.
    /// It is for looking at the model in the editor; the Reactor prefab is untouched. Menu: SpaceScape > Build Reactor
    /// Draft. It also runs when Temp/build-reactor-draft exists, so it can be asked for from outside the editor.
    [InitializeOnLoad]
    public static class ReactorDraftBuilder
    {
        private const string ModelPath = "Assets/#User/SpaceScape/Meshes/Reactor.fbx";
        private const string ReactorPath = "Assets/#User/SpaceScape/Prefabs/Components/Reactor.prefab";
        private const string OutPath = "Assets/#User/SpaceScape/Prefabs/Components/Reactor Draft.prefab";
        private const string MaterialFolder = "Assets/#User/SpaceScape/Materials/";
        private const string Request = "Temp/build-reactor-draft";

        // The deck the levers stand on, as in Reactor.py: front edge and back edge (z, y).
        private static readonly Vector2 DeckFront = new Vector2(2.38f, -1.6f);
        private static readonly Vector2 DeckBack = new Vector2(2.02061f, -1.38f);
        // Six chamber levers, then the master (Reactor.py LEVERS_X).
        private static readonly float[] LeversX = { 0.78f, 0.54f, 0.30f, 0.06f, -0.18f, -0.42f, -0.78f };
        // The lever's base sits this far along its own x from its root (the Reactor prefab's RodLever).
        private const float LeverBaseOffset = 0.217f;

        // The six chambers' item ports on the top face, and the I/O panel on the back: In, Data, Out.
        private static readonly Vector2Int[] TopPorts =
            { new(7, 5), new(5, 7), new(3, 6), new(2, 4), new(4, 2), new(6, 3) };

        static ReactorDraftBuilder()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            Build();
        }

        [MenuItem("SpaceScape/Build Reactor Draft")]
        public static void Build()
        {
            RemapMaterials();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var reactor = AssetDatabase.LoadAssetAtPath<GameObject>(ReactorPath);
            if (model == null || reactor == null)
            {
                Debug.LogError($"Reactor Draft: missing {(model == null ? ModelPath : ReactorPath)}");
                return;
            }

            var root = new GameObject("Reactor Draft");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Model";

                var slotted = new SerializedObject(root.AddComponent<SlottedComponent>());
                slotted.FindProperty("_size").intValue = (int)SlotSize.S5;
                slotted.ApplyModifiedPropertiesWithoutUndo();

                var edge = root.AddComponent<ComponentEdgeModule>();
                var from = new SerializedObject(reactor.GetComponent<ComponentEdgeModule>());
                var to = new SerializedObject(edge);
                to.FindProperty("_portPrefab").objectReferenceValue = from.FindProperty("_portPrefab").objectReferenceValue;
                to.FindProperty("_itemPortPrefab").objectReferenceValue = from.FindProperty("_itemPortPrefab").objectReferenceValue;
                to.ApplyModifiedPropertiesWithoutUndo();
                var top = new ComponentEdgeModule.Port[TopPorts.Length];
                for (int i = 0; i < top.Length; i++) top[i] = new ComponentEdgeModule.Port { Cell = TopPorts[i], Flow = FlowDirection.Item };
                edge.SetPorts(GridDirection.YPlus, top);
                edge.SetPorts(GridDirection.ZMinus, new[]
                {
                    new ComponentEdgeModule.Port { Cell = new Vector2Int(3, 0), Flow = FlowDirection.In },
                    new ComponentEdgeModule.Port { Cell = new Vector2Int(4, 0), Flow = FlowDirection.Data },
                    new ComponentEdgeModule.Port { Cell = new Vector2Int(5, 0), Flow = FlowDirection.Out },
                });

                AddLevers(root.transform, reactor.transform);
                PrefabUtility.SaveAsPrefabAsset(root, OutPath);
                Debug.Log($"Reactor Draft: built {OutPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// Seven copies of the Reactor prefab's RodLever on the deck, without its rod display or mounting block, and
        /// with no target: they move under a hand and set nothing.
        private static void AddLevers(Transform root, Transform reactor)
        {
            var source = Find(reactor, "RodLever");
            if (source == null)
            {
                Debug.LogError("Reactor Draft: no RodLever in the Reactor prefab");
                return;
            }
            float tilt = Mathf.Atan2(DeckBack.y - DeckFront.y, DeckFront.x - DeckBack.x) * Mathf.Rad2Deg;
            var middle = (DeckFront + DeckBack) / 2f;
            for (int i = 0; i < LeversX.Length; i++)
            {
                var lever = Object.Instantiate(source.gameObject, root);
                lever.name = i < 6 ? $"Lever {i + 1}" : "Master Lever";
                foreach (var name in new[] { "RodDisplay", "Cube" })
                {
                    var part = lever.transform.Find(name);
                    if (part != null) Object.DestroyImmediate(part.gameObject);
                }
                var rod = new SerializedObject(lever.GetComponent<RodLever>());
                rod.FindProperty("_target").objectReferenceValue = null;
                rod.ApplyModifiedPropertiesWithoutUndo();
                lever.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
                lever.transform.localPosition = new Vector3(LeversX[i] - LeverBaseOffset, middle.y, middle.x);
            }
        }

        /// The FBX's materials onto the project's: the loader's set, the item conduit's glass and the fuel pack's glow.
        private static void RemapMaterials()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (importer == null) return;
            void Remap(string name, string material) =>
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name),
                    AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + material + ".mat"));
            Remap("FuelPackMetal", "FuelPackMetal");
            Remap("FuelPackDark", "FuelPackDark");
            Remap("ComponentItem", "ComponentItem");
            Remap("ReactorGlass", "ItemConduitGlass");
            Remap("ReactorCore", "FuelPackGlow");
            importer.SaveAndReimport();
        }

        private static Transform Find(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
