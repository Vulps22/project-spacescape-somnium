using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// Test targets for the disruptor and radar, phase 1 (docs/radar.md): Count of them, dotted at random within
    /// the ranges, from a fixed Seed, so every client places them the same with nothing sent. Each carries a
    /// DetectableModule. Hit, a target disappears; it comes back where it was Down Seconds later.
    ///
    /// Whoever decides (the master, or anyone with no network) knocks targets out and brings them back; anyone
    /// else asks (HitRequested) and follows (SetDown). A gun's shot reports what it hit with Hit.
    public sealed class TargetField : MonoBehaviour
    {
        [Tooltip("The target to place. Needs a collider for shots to hit. Empty: a plain sphere.")]
        [SerializeField] private GameObject _targetPrefab;
        [Tooltip("How many targets.")]
        [SerializeField] private int _count = 20;
        [Tooltip("Fixes where they go. The same seed places them the same on every client.")]
        [SerializeField] private int _seed = 1234;
        [Tooltip("World x they are spread across, metres.")]
        [SerializeField] private Vector2 _xRange = new Vector2(-50f, 50f);
        [Tooltip("World height they are spread across, metres.")]
        [SerializeField] private Vector2 _yRange = new Vector2(0f, 50f);
        [Tooltip("World z they are spread across, metres.")]
        [SerializeField] private Vector2 _zRange = new Vector2(-500f, -100f);
        [Tooltip("Size of a plain sphere target, in metres across.")]
        [SerializeField] private float _sphereSize = 4f;
        [Tooltip("Material of a plain sphere target. Optional.")]
        [SerializeField] private Material _sphereMaterial;
        [Tooltip("Seconds a hit target stays gone before it comes back.")]
        [SerializeField] private float _downSeconds = 60f;

        private readonly List<GameObject> _targets = new List<GameObject>();
        private bool[] _down = new bool[0];
        private float[] _backAt = new float[0];

        /// The field in the world, or null.
        public static TargetField Current { get; private set; }

        /// How many targets there are.
        public int Count => _targets.Count;

        /// True where this client knocks targets out and brings them back. Set by the network.
        public bool Decides { get; set; } = true;

        /// Raised on a client that does not decide when a shot here hit a target, for the network to ask.
        public event Action<int> HitRequested;

        private void Awake()
        {
            Current = this;
            var random = new System.Random(_seed);
            for (int i = 0; i < _count; i++)
            {
                var at = new Vector3(Range(random, _xRange), Range(random, _yRange), Range(random, _zRange));
                GameObject target;
                if (_targetPrefab != null) target = Instantiate(_targetPrefab, at, Quaternion.identity, transform);
                else
                {
                    target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    target.transform.SetParent(transform, false);
                    target.transform.SetPositionAndRotation(at, Quaternion.identity);
                    target.transform.localScale = Vector3.one * _sphereSize;
                    if (_sphereMaterial != null) target.GetComponent<Renderer>().sharedMaterial = _sphereMaterial;
                }
                target.name = $"Target {i}";
                if (!target.TryGetComponent<DetectableModule>(out var detectable)) detectable = target.AddComponent<DetectableModule>();
                detectable.Id = i;
                _targets.Add(target);
            }
            _down = new bool[_count];
            _backAt = new float[_count];
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private static float Range(System.Random random, Vector2 range) =>
            Mathf.Lerp(range.x, range.y, (float)random.NextDouble());

        /// The target a collider belongs to, or -1.
        public int IndexOf(Collider collider)
        {
            if (collider == null) return -1;
            for (int i = 0; i < _targets.Count; i++)
                if (collider.transform.IsChildOf(_targets[i].transform)) return i;
            return -1;
        }

        /// A shot here hit a target: knocks it out where this client decides, or asks.
        public void Hit(int index)
        {
            if (index < 0 || index >= _targets.Count || _down[index]) return;
            if (Decides) SetDown(index, true);
            else HitRequested?.Invoke(index);
        }

        /// True while a target is knocked out.
        public bool IsDown(int index) => index >= 0 && index < _down.Length && _down[index];

        /// Knocks a target out or brings it back. Where this client decides; everyone else is told.
        public void SetDown(int index, bool down)
        {
            if (index < 0 || index >= _targets.Count || _down[index] == down) return;
            _down[index] = down;
            if (down) _backAt[index] = Time.time + _downSeconds;
            _targets[index].SetActive(!down);
        }

        private void Update()
        {
            if (!Decides) return;
            for (int i = 0; i < _down.Length; i++)
                if (_down[i] && Time.time >= _backAt[i]) SetDown(i, false);
        }
    }
}
