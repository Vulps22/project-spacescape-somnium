using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace SomniumSpace.Worlds.SpaceScape.Player
{
    /// Lengthens the local player's grab laser to Reach, once their rig is in (PlayerManager.LocalPlayerJoined).
    /// Plain XRI: only the local player's rig has interactors on this client, so every one found is theirs. A ray
    /// interactor's reach and its drawn line are set apart, so both are set; a near-far interactor's line follows its
    /// caster. Curved rays (teleporting) are left alone, and nothing is ever shortened. What it finds is logged once,
    /// with what it was before.
    public sealed class GrabReach : MonoBehaviour
    {
        [Tooltip("How far the grab laser reaches, in metres. Only ever lengthens it.")]
        [SerializeField] private float _reach = 3f;

        private void OnEnable() => PlayerManager.LocalPlayerJoined += OnLocalPlayerJoined;

        private void OnDisable() => PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;

        private void Start()
        {
            if (PlayerManager.LocalPlayer.Exists) Lengthen();
        }

        private void OnLocalPlayerJoined(PlayerIdentity who) => Lengthen();

        private void Lengthen()
        {
            int found = 0;
            foreach (var ray in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ray.lineType != XRRayInteractor.LineType.StraightLine) continue;
                found++;
                float was = ray.maxRaycastDistance;
                if (was < _reach) ray.maxRaycastDistance = _reach;
                string line = "no line";
                if (ray.TryGetComponent(out XRInteractorLineVisual visual))
                {
                    line = $"line {visual.lineLength:0.##} m (own length {visual.overrideInteractorLineLength})";
                    if (visual.overrideInteractorLineLength && visual.lineLength < _reach) visual.lineLength = _reach;
                }
                Debug.Log($"GrabReach: ray interactor '{ray.name}' reached {was:0.##} m, {line}; now {ray.maxRaycastDistance:0.##} m", ray);
            }
            foreach (var nearFar in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                found++;
                if (nearFar.farInteractionCaster is CurveInteractionCaster caster)
                {
                    float was = caster.castDistance;
                    if (was < _reach) caster.castDistance = _reach;
                    Debug.Log($"GrabReach: near-far interactor '{nearFar.name}' reached {was:0.##} m; now {caster.castDistance:0.##} m", nearFar);
                }
                else
                    Debug.Log($"GrabReach: near-far interactor '{nearFar.name}' has a far caster of another kind ({nearFar.farInteractionCaster?.GetType().Name ?? "none"}); left alone", nearFar);
            }
            if (found == 0) Debug.LogWarning("GrabReach: the local player joined, but no ray or near-far interactor was found", this);
        }
    }
}
