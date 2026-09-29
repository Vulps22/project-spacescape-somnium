using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Shows what the grid is doing to its tiles: sparks from every tile that has blown, smoke from every
    /// tile with integrity once it is damaged. Spawns the emitters itself, so no tile needs wiring, and looks
    /// for new tiles once a second, since components are spawned over the network after the world starts.
    public sealed class TileEffects : MonoBehaviour
    {
        [Tooltip("Emitter shown on any tile that has blown: severed, or wrecked with no integrity left.")]
        [SerializeField] private ParticleSystem _sparksPrefab;
        [Tooltip("Emitter shown on tiles with an Integrity Module once they are damaged.")]
        [SerializeField] private ParticleSystem _smokePrefab;
        [Tooltip("Smoke puffs per second from the first scratch of damage, however much condition is left.")]
        [SerializeField] private float _minSmokePerSecond = 6f;
        [Tooltip("Smoke puffs per second at zero condition. Rises from Min Smoke Per Second as condition falls.")]
        [SerializeField] private float _maxSmokePerSecond = 12f;

        private readonly List<(GridNode tile, ParticleSystem sparks)> _sparks = new List<(GridNode, ParticleSystem)>();
        private readonly List<(IntegrityModule integrity, ParticleSystem smoke)> _smoke = new List<(IntegrityModule, ParticleSystem)>();
        private readonly HashSet<GridNode> _seen = new HashSet<GridNode>();
        private const float RescanSeconds = 1f;
        private float _nextScan;

        /// Gives every tile not seen yet its emitters.
        private void Scan()
        {
            foreach (var tile in FindObjectsByType<GridNode>(FindObjectsSortMode.None))
            {
                if (!_seen.Add(tile)) continue;
                if (_sparksPrefab != null) _sparks.Add((tile, Spawn(_sparksPrefab, tile)));
                if (_smokePrefab != null && tile.TryGetComponent<IntegrityModule>(out var integrity))
                    _smoke.Add((integrity, Spawn(_smokePrefab, tile)));
            }
        }

        private void Update()
        {
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + RescanSeconds;
                // A destroyed tile takes its emitters with it.
                _seen.RemoveWhere(tile => tile == null);
                _sparks.RemoveAll(pair => pair.tile == null || pair.sparks == null);
                _smoke.RemoveAll(pair => pair.integrity == null || pair.smoke == null);
                Scan();
            }

            foreach (var (tile, sparks) in _sparks)
            {
                var node = tile != null ? tile.Node : null;
                Run(sparks, node != null && (node.IsPopped || node.IsWrecked));
            }

            foreach (var (integrity, smoke) in _smoke)
            {
                float damage = integrity != null ? 1f - (float)integrity.Integrity.Fraction : 0f;
                var emission = smoke.emission;
                emission.rateOverTime = Mathf.Lerp(_minSmokePerSecond, _maxSmokePerSecond, damage);
                Run(smoke, damage > 0f);
            }
        }

        /// Puts an emitter at the centre of a tile's footprint, parented to it so it moves with it.
        private static ParticleSystem Spawn(ParticleSystem prefab, GridNode tile)
        {
            var centre = GridCell.Centre(tile.Min, tile.Max);
            var effect = Instantiate(prefab, centre, Quaternion.identity, tile.transform);
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return effect;
        }

        /// Starts or stops an emitter, letting what is already in the air finish.
        private static void Run(ParticleSystem effect, bool on)
        {
            if (effect == null) return;
            if (on && !effect.isEmitting) effect.Play(true);
            else if (!on && effect.isEmitting) effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
