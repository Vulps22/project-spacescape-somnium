using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEditor;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.EditorTools
{
    /// The surge's Editor tools, on the component's menu (docs/tutorial-proposal.md, Plan D). Kept out of SurgeScript
    /// itself: the uploader rejects world scripts that mention the Editor.
    public static class SurgeScriptMenu
    {
        /// Fills Clips from the sounds folder: stepN (any audio file of that name) is clip N-1.
        [MenuItem("CONTEXT/SurgeScript/Find Step Sounds")]
        private static void FindClips(MenuCommand command)
        {
            var script = (SurgeScript)command.context;
            if (!AssetDatabase.IsValidFolder(script.SoundsFolder))
            {
                Debug.LogWarning($"[SpaceScape] Surge: no folder '{script.SoundsFolder}'", script);
                return;
            }
            var found = new AudioClip[script.StepCount];
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { script.SoundsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("step", StringComparison.OrdinalIgnoreCase) || !int.TryParse(name.Substring(4), out int n)) continue;
                if (n < 1 || n > found.Length) continue;
                found[n - 1] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                count++;
            }
            Undo.RecordObject(script, "Find Step Sounds");
            script.SetClips(found);
            EditorUtility.SetDirty(script);
            Debug.Log($"[SpaceScape] Surge: found {count} of {found.Length} step sounds in {script.SoundsFolder}", script);
        }

        /// Replaces the lines with the script as written, waits included, targets left to set in the scene.
        [MenuItem("CONTEXT/SurgeScript/Fill Lines From The Script")]
        private static void FillLines(MenuCommand command)
        {
            var script = (SurgeScript)command.context;
            var steps = new List<SurgeScript.Step>();
            void Add(string line, SurgeScript.Wait wait, float seconds = 2f) =>
                steps.Add(new SurgeScript.Step { Line = line, Wait = wait, Seconds = seconds });
            Add("A power surge has wreaked havoc on engineering. This is a great time to learn the fundamentals.", SurgeScript.Wait.None);
            Add("First things first: we need power flowing. Look at the reactor.", SurgeScript.Wait.Seen);
            Add("Good, it still has a fuel rod. So we just need to repair the conduit that feeds it for a cold start. Open the access panel: wave a hand through the glowing cube.", SurgeScript.Wait.PanelOpen);
            Add("See the sparks? That conduit was damaged. Put a hand on it to open it up.", SurgeScript.Wait.HologramOpen);
            Add("Drag the sphere from the centre out to fill the gap and complete the circuit.", SurgeScript.Wait.CellMended);
            Add("Excellent. You can tell a conduit is powered when it lights up like that. Now try the lever on the reactor. A little goes a long way with an S2.", SurgeScript.Wait.ReactorRunning);
            Add("Great. Now let's put the switch back on the conduit you just repaired.", SurgeScript.Wait.AddonHeld);
            Add("Open the conduit again and drop the switch in the blue square.", SurgeScript.Wait.AddonInstalled);
            Add("It goes in off. That's fine: a running reactor doesn't need its starter.", SurgeScript.Wait.Pause, 2f);
            Add("Bridge says they aren't getting any power. Let's check the battery banks for damage.", SurgeScript.Wait.Seen);
            Add("There. That S1 battery says it's wrecked. We'll have to replace it. Pull the red handle to unlock it.", SurgeScript.Wait.SlotUnlocked);
            Add("Now drag it out of its slot.", SurgeScript.Wait.SlotEmpty);
            Add("Our engineers have just delivered a replacement. Install it in the empty slot.", SurgeScript.Wait.SlotLocked);
            Add("Hmm. Something's not right. Check the ports on the back of the battery.", SurgeScript.Wait.Seen);
            Add("That explains it. Those three ports are on every component, at least one of them. Blue takes power in, red is data, and green sends power out.", SurgeScript.Wait.None);
            Add("Put a hand next to the green port and drag the spheres out, like you did to repair the conduit.", SurgeScript.Wait.ConduitOnFace);
            Add("Good. Any port or conduit with a dead end lets you install a new conduit like that, power or item.", SurgeScript.Wait.None);
            Add("Now lay a line from it to the end of the bridge feed.", SurgeScript.Wait.ConduitOnFace);
            Add("The end of the feed got knocked aside. Open it and drag its handle round to meet your line.", SurgeScript.Wait.ConduitOnFace);
            Add("Now its arrow. Slide it in, toward the centre, so power comes in from your line.", SurgeScript.Wait.SegmentIn);
            Add("There. Bridge has power.", SurgeScript.Wait.Powered);
            Undo.RecordObject(script, "Fill Lines From The Script");
            script.SetSteps(steps);
            EditorUtility.SetDirty(script);
        }
    }
}
