using SomniumSpace.Worlds.SpaceScape.Ship;
using UnityEditor;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.EditorTools
{
    /// Plays the disruptor's sounds in the Editor, for setting its loop by ear: the charged sound (intro, then
    /// the loop until stopped), the loop alone, and the shot. Changing Loop Start or Loop Length while a preview
    /// plays restarts it with the new cut. The players are hidden objects that are never saved.
    [CustomEditor(typeof(DisruptorBehaviourModule))]
    public sealed class DisruptorEditor : Editor
    {
        private enum Playing { Nothing, Charged, Loop, Fire }

        private GameObject _player;
        private AudioSource _intro;
        private AudioSource _loop;
        private Playing _playing;

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();

            var clip = serializedObject.FindProperty("_chargedClip").objectReferenceValue as AudioClip;
            float start = serializedObject.FindProperty("_loopStart").floatValue;
            float length = serializedObject.FindProperty("_loopLength").floatValue;
            var fire = serializedObject.FindProperty("_fireClip").objectReferenceValue as AudioClip;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            if (clip != null)
            {
                var (intro, loop) = DisruptorBehaviourModule.Cut(clip, start, length);
                if (loop != null)
                    EditorGUILayout.HelpBox($"Cut at {Seconds(intro)} s; loop {Seconds(loop)} s long, from {Seconds(intro)} to {(intro.samples + loop.samples) / (float)clip.frequency:0.0000} s.", MessageType.None);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = clip != null;
                if (GUILayout.Button("Play Charged")) Play(Playing.Charged, clip, start, length, fire);
                if (GUILayout.Button("Play Loop")) Play(Playing.Loop, clip, start, length, fire);
                GUI.enabled = fire != null;
                if (GUILayout.Button("Play Fire")) Play(Playing.Fire, clip, start, length, fire);
                GUI.enabled = true;
                if (GUILayout.Button("Stop")) Stop();
            }

            // Hear a new cut straight away.
            if (changed && (_playing == Playing.Charged || _playing == Playing.Loop)) Play(_playing, clip, start, length, fire);
        }

        private static string Seconds(AudioClip clip) => (clip.samples / (float)clip.frequency).ToString("0.0000");

        private void Play(Playing what, AudioClip clip, float start, float length, AudioClip fire)
        {
            Stop();
            MakePlayer();
            _playing = what;
            if (what == Playing.Fire)
            {
                _intro.clip = fire;
                _intro.loop = false;
                _intro.Play();
                return;
            }

            var (intro, loop) = DisruptorBehaviourModule.Cut(clip, start, length);
            if (loop == null) return;
            _loop.clip = loop;
            _loop.loop = true;
            if (what == Playing.Loop)
            {
                _loop.Play();
                return;
            }
            double at = AudioSettings.dspTime + 0.05;
            _intro.clip = intro;
            _intro.loop = false;
            _intro.PlayScheduled(at);
            _loop.PlayScheduled(at + intro.samples / (double)intro.frequency);
        }

        private void Stop()
        {
            if (_intro != null) _intro.Stop();
            if (_loop != null) _loop.Stop();
            _playing = Playing.Nothing;
        }

        private void MakePlayer()
        {
            if (_player != null) return;
            _player = new GameObject("Disruptor sound preview") { hideFlags = HideFlags.HideAndDontSave };
            _intro = _player.AddComponent<AudioSource>();
            _loop = _player.AddComponent<AudioSource>();
            foreach (var source in new[] { _intro, _loop })
            {
                source.playOnAwake = false;
                source.spatialBlend = 0f;   // heard flat, wherever the Scene view is
            }
        }

        private void OnDisable()
        {
            if (_player != null) DestroyImmediate(_player);
            _player = null;
        }
    }
}
