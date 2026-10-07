using System;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// The numbers one module writes on its own text, for whoever is standing there to read. Each module
    /// shows only its own stats, so a component's readout is laid out by placing the texts of the modules
    /// it wants. Redrawn when something it shows changed, never more than once per Interval, and only while
    /// a camera can see it; nothing runs per frame. A change it could not see waits, and is drawn on the
    /// first grid tick after it comes into view.
    public sealed class Readout
    {
        /// Fewest seconds between two redraws of the same text.
        public const float Interval = 0.1f;

        private readonly TMP_Text _text;
        private readonly Func<string> _compose;
        private readonly Func<bool> _changed;
        private readonly Renderer _renderer;
        private PowerGrid _grid;
        private float _drawn = float.NegativeInfinity;
        private bool _stale;
        private string _shown;

        /// Writes compose's text on text. changed is asked once per grid tick whether anything shown has
        /// moved; left out, every tick counts as a change, for numbers that follow the grid.
        public Readout(TMP_Text text, Func<string> compose, Func<bool> changed = null)
        {
            _text = text;
            _compose = compose;
            _changed = changed;
            _renderer = text != null ? text.GetComponent<Renderer>() : null;   // none on a canvas text: always drawn
        }

        /// Starts listening to the grid, and draws for the first time.
        public void Listen()
        {
            _grid = PowerGrid.Current;
            if (_grid != null) _grid.Ticked += OnTicked;
            Changed();
        }

        /// Stops listening, for a module being destroyed.
        public void Stop()
        {
            if (_grid != null) _grid.Ticked -= OnTicked;
            _grid = null;
        }

        /// Something it shows changed: redraws now, or on the first tick the interval allows.
        public void Changed()
        {
            _stale = true;
            Draw();
        }

        private void OnTicked()
        {
            if (_changed == null || _changed()) Changed();
            else Draw();   // a change that came too soon to draw is still waiting
        }

        private void Draw()
        {
            if (!_stale || _text == null || !_text.isActiveAndEnabled) return;
            // The first draw goes through regardless: an empty text has no mesh, so it could never be seen.
            if (_shown != null && _renderer != null && !_renderer.isVisible) return;
            if (Time.time < _drawn + Interval) return;
            _stale = false;
            _drawn = Time.time;

            string text = _compose();
            if (text == _shown) return;
            _shown = text;
            _text.SetText(text);
        }
    }
}
