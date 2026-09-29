using System;
using SomniumSpace.Worlds.SpaceScape.Community;
using SomniumSpace.Worlds.SpaceScape.Player;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SomniumSpace.Worlds.SpaceScape.EditorTools
{
    /// Gives every slotted component prefab what carrying needs (docs/grid.md, Moving a component): a box
    /// collider filling its size, a kinematic Rigidbody, a stock XRGrabInteractable, Fusion's
    /// NetworkRigidbody3D with NetworkGrabbable (as the addon items have), IgnoresPlayerBody, and
    /// CarriedComponent. Safe to run again: it only adds what is missing and resets the settings below.
    /// Fusion re-bakes each prefab's NetworkObject when it is saved.
    public static class CarriableComponentsTool
    {
        private const string PrefabFolder = "Assets/#User/SpaceScape/Prefabs";
        private const string NetworkRigidbodyType = "Fusion.Addons.Physics.NetworkRigidbody3D, Fusion.Addons.Physics";
        private const int GrabLayers = 8;   // the interaction layers the addon items and handle bar use

        [MenuItem("SpaceScape/Make Slotted Components Carriable")]
        private static void Run()
        {
            var networkRigidbody = Type.GetType(NetworkRigidbodyType);
            if (networkRigidbody == null)
            {
                Debug.LogError($"CarriableComponentsTool: cannot find {NetworkRigidbodyType}");
                return;
            }

            int done = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<SlottedComponent>() == null) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Make(root, networkRigidbody);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                    Debug.Log($"CarriableComponentsTool: '{path}' is carriable");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"CarriableComponentsTool: {done} slotted component prefab(s) made carriable");
        }

        private static void Make(GameObject root, Type networkRigidbodyType)
        {
            var slotted = root.GetComponent<SlottedComponent>();

            // Fills its size's volume, as a component's model does (grid.md, Components).
            var box = root.GetComponent<BoxCollider>() ?? root.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = (Vector3)slotted.LocalCells * GridCell.Size;
            box.isTrigger = false;

            // Kinematic from the start: it is spawned into a slot and must not fall before it is installed.
            var body = root.GetComponent<Rigidbody>() ?? root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var grab = root.GetComponent<XRGrabInteractable>() ?? root.AddComponent<XRGrabInteractable>();
            grab.interactionLayers = GrabLayers;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.trackPosition = true;
            grab.trackRotation = true;
            grab.trackScale = false;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;      // held where the hand took it, not by its centre
            grab.retainTransformParent = true;
            // Only its own box: the reactor's lever and dial have colliders of their own interactables.
            grab.colliders.Clear();
            grab.colliders.Add(box);

            var networked = root.GetComponent(networkRigidbodyType) ?? root.AddComponent(networkRigidbodyType);

            var grabbable = root.GetComponent<NetworkGrabbable>() ?? root.AddComponent<NetworkGrabbable>();
            var wiring = new SerializedObject(grabbable);
            wiring.FindProperty("_grabInteracable").objectReferenceValue = grab;
            wiring.FindProperty("_networkRigidbody").objectReferenceValue = networked;
            wiring.FindProperty("_rigidbody").objectReferenceValue = body;
            wiring.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<IgnoresPlayerBody>() == null) root.AddComponent<IgnoresPlayerBody>();
            if (root.GetComponent<CarriedComponent>() == null) root.AddComponent<CarriedComponent>();
        }
    }
}
