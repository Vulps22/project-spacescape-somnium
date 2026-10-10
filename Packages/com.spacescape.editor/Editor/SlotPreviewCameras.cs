using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SomniumSpace.Worlds.SpaceScape.EditorTools
{
    /// Keeps each slot's preview of its starting component to cameras showing the slot's own scene, so the scene's
    /// slots do not draw ghosts into a prefab open in Prefab Mode. Here rather than in ComponentSlot, since world
    /// scripts may not use UnityEditor (the uploader refuses them).
    [InitializeOnLoad]
    public static class SlotPreviewCameras
    {
        static SlotPreviewCameras()
        {
            ComponentSlot.PreviewShownTo = StageUtility.IsGameObjectRenderedByCamera;
        }
    }
}
