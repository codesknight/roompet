using System;
using System.Collections.Generic;
using UnityEngine;

namespace DshRunner
{
    /// <summary>
    /// Timed power-ups. Effects are read by the rest of the game through the properties
    /// here (<see cref="ScoreMultiplier"/>, <see cref="MagnetActive"/>, the slow-motion
    /// speed scale) rather than by reaching into timers.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class PowerUpSystem : MonoBehaviour
    {
        [Header("Durations (seconds)")]
        public float ShieldDuration = 14f;
        public float MagnetDuration = 10f;
        public float DoubleScoreDuration = 12f;
        public float SlowMotionDuration = 6f;

        [Header("Strengths")]
        public float MagnetRadius = 6.5f;
        public float SlowMotionScale = 0.55f;
        public float DoubleScoreFactor = 2f;

        private readonly Dictionary<PowerUpKind, float> _timers = new Dictionary<PowerUpKind, float>();
        private readonly Dictionary<PowerUpKind, float> _durations = new Dictionary<PowerUpKind, float>();
        private int _shieldCharges;

        /// <summary>Counts activations; handy for tests and for the HUD's pickup flash.</summary>
        public readonly Dictionary<PowerUpKind, int> PickupCounts = new Dictionary<PowerUpKind, int>();

        public bool MagnetActive => Remaining(PowerUpKind.Magnet) > 0f;
        public bool DoubleScoreActive => Remaining(PowerUpKind.DoubleScore) > 0f;
        public bool SlowMotionActive => Remaining(PowerUpKind.SlowMotion) > 0f;
        public bool ShieldActive => _shieldCharges > 0;

        public float ScoreMultiplier => DoubleScoreActive ? DoubleScoreFactor : 1f;
        public float SpeedScale => SlowMotionActive ? SlowMotionScale : 1f;
        public int ShieldCharges => _shieldCharges;

        private void Awake()
        {
            _durations[PowerUpKind.Shield] = ShieldDuration;
            _durations[PowerUpKind.Magnet] = MagnetDuration;
            _durations[PowerUpKind.DoubleScore] = DoubleScoreDuration;
            _durations[PowerUpKind.SlowMotion] = SlowMotionDuration;

            foreach (PowerUpKind kind in Enum.GetValues(typeof(PowerUpKind)))
            {
                _timers[kind] = 0f;
                PickupCounts[kind] = 0;
            }
        }

        public float Duration(PowerUpKind kind) => _durations.TryGetValue(kind, out var value) ? value : 0f;

        public float Remaining(PowerUpKind kind) => _timers.TryGetValue(kind, out var value) ? value : 0f;

        public float Normalised(PowerUpKind kind)
        {
            float duration = Duration(kind);
            if (duration <= 0f) return 0f;
            return Mathf.Clamp01(Remaining(kind) / duration);
        }

        public void Activate(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Shield:
                    // Stacking a shield refreshes the charge rather than hoarding charges.
                    _shieldCharges = Mathf.Max(1, _shieldCharges);
                    _timers[kind] = Duration(kind);
                    break;
                default:
                    _timers[kind] = Duration(kind);
                    break;
            }

            PickupCounts[kind] = PickupCounts.TryGetValue(kind, out var count) ? count + 1 : 1;
            GameManager.Instance?.NotifyPowerUp(kind, Duration(kind));
        }

        /// <summary>Consumes a shield charge if one is up. Returns true when the hit was
        /// absorbed.</summary>
        public bool ConsumeShield()
        {
            if (_shieldCharges <= 0) return false;
            _shieldCharges--;
            _timers[PowerUpKind.Shield] = 0f;
            return true;
        }

        /// <summary>Advances timers. Called by the GameManager before it reads the
        /// resulting multipliers.</summary>
        public void Tick(float dt, float distance, float speed)
        {
            _ = distance;
            _ = speed;

            var keys = new List<PowerUpKind>(_timers.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var kind = keys[i];
                if (_timers[kind] <= 0f) continue;
                _timers[kind] = Mathf.Max(0f, _timers[kind] - dt);
                if (_timers[kind] <= 0f && kind == PowerUpKind.Shield)
                {
                    _shieldCharges = 0;
                }
            }

            var gm = GameManager.Instance;
            if (gm != null) gm.SpeedScale = SpeedScale;
        }

        public void ClearAll()
        {
            var keys = new List<PowerUpKind>(_timers.Keys);
            for (int i = 0; i < keys.Count; i++) _timers[keys[i]] = 0f;
            _shieldCharges = 0;

            var gm = GameManager.Instance;
            if (gm != null) gm.SpeedScale = 1f;
        }

        public string Describe(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Shield: return "护盾";
                case PowerUpKind.Magnet: return "磁铁";
                case PowerUpKind.DoubleScore: return "双倍分";
                case PowerUpKind.SlowMotion: return "减速";
                default: return kind.ToString();
            }
        }
    }
}
