using System;
using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Decides what the pet does next. Two independent timers:
    ///
    /// - A frequent proactive pass that scores the need-driven table and takes the most
    ///   urgent eligible activity (going to eat, going to bed, coming to find you).
    /// - A slower passive pass that picks a weighted-random ambient quirk, which is what
    ///   keeps the pet from looking like a state machine.
    ///
    /// The scheduler only chooses. Acting is the controller's job, which keeps selection
    /// pure and testable.
    /// </summary>
    public class PetBehaviorScheduler
    {
        /// <summary>How often the proactive table is scored.</summary>
        public float ProactiveInterval = 2.5f;

        /// <summary>Random delay between ambient quirks.</summary>
        public float PassiveIntervalMin = 7f;
        public float PassiveIntervalMax = 16f;

        /// <summary>Minimum proactive score worth interrupting for. Higher = calmer pet.</summary>
        public float ProactiveThreshold = 0.22f;

        public PetBehavior LastProactive { get; private set; }
        public PetBehavior LastPassive { get; private set; }
        public float LastDecisionAt { get; private set; }

        private readonly Dictionary<string, float> _readyAt = new Dictionary<string, float>();
        private readonly System.Random _rng;
        private float _proactiveTimer;
        private float _passiveTimer;

        public PetBehaviorScheduler(int seed = 0)
        {
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
            _proactiveTimer = ProactiveInterval;
            _passiveTimer = RandomPassiveDelay();
        }

        private float RandomPassiveDelay()
            => Mathf.Lerp(PassiveIntervalMin, PassiveIntervalMax, (float)_rng.NextDouble());

        public void Reset()
        {
            _readyAt.Clear();
            _proactiveTimer = ProactiveInterval;
            _passiveTimer = RandomPassiveDelay();
            LastProactive = null;
            LastPassive = null;
        }

        public float CooldownRemaining(PetBehavior behavior)
        {
            if (behavior == null) return 0f;
            if (!_readyAt.TryGetValue(behavior.Id, out float readyAt)) return 0f;
            return Mathf.Max(0f, readyAt - Time.time);
        }

        public bool IsReady(PetBehavior behavior)
            => CooldownRemaining(behavior) <= 0f;

        /// <summary>
        /// Advances both timers and returns at most one behaviour to run this frame.
        /// <paramref name="canAct"/> is false while the pet is already busy, so the timers
        /// do not burn down during a conversation or an in-flight activity.
        /// </summary>
        public PetBehavior Tick(float deltaSeconds, PetBehaviorContext ctx, List<PetBehavior> pool, bool canAct)
        {
            if (deltaSeconds <= 0f) return null;

            if (!canAct)
            {
                // Hold the timers steady rather than letting them expire silently.
                _proactiveTimer = Mathf.Max(_proactiveTimer, ProactiveInterval * 0.5f);
                _passiveTimer = Mathf.Max(_passiveTimer, 1f);
                return null;
            }

            _proactiveTimer -= deltaSeconds;
            _passiveTimer -= deltaSeconds;

            if (_proactiveTimer <= 0f)
            {
                _proactiveTimer = ProactiveInterval;
                var best = PickBestProactive(ctx, pool);
                if (best != null)
                {
                    MarkUsed(best);
                    LastProactive = best;
                    LastDecisionAt = Time.time;
                    return best;
                }
            }

            if (_passiveTimer <= 0f)
            {
                _passiveTimer = RandomPassiveDelay();
                var quirk = PickPassive(ctx, pool);
                if (quirk != null)
                {
                    MarkUsed(quirk);
                    LastPassive = quirk;
                    LastDecisionAt = Time.time;
                    return quirk;
                }
            }

            return null;
        }

        /// <summary>Highest-scoring eligible proactive row above the threshold.</summary>
        public PetBehavior PickBestProactive(PetBehaviorContext ctx, List<PetBehavior> pool)
        {
            PetBehavior best = null;
            float bestScore = Mathf.Max(0.01f, ProactiveThreshold);

            for (int i = 0; i < pool.Count; i++)
            {
                var behavior = pool[i];
                if (behavior.Drive != BehaviorDrive.Proactive) continue;
                if (!IsReady(behavior)) continue;

                float score = behavior.Score(ctx);
                if (score >= bestScore)
                {
                    bestScore = score;
                    best = behavior;
                }
            }

            return best;
        }

        /// <summary>Weighted-random ambient quirk. Never returns the one that just played.</summary>
        public PetBehavior PickPassive(PetBehaviorContext ctx, List<PetBehavior> pool)
        {
            // AmbientWeight, not Weight: the personality has to colour the quirks too, or a
            // lazy pet and a restless one would stretch and hop equally often.
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                var behavior = pool[i];
                if (behavior.Drive != BehaviorDrive.Passive) continue;
                if (!IsReady(behavior)) continue;
                if (!behavior.IsEligible(ctx)) continue;
                if (behavior == LastPassive && pool.Count > 2) continue; // avoid immediate repeats
                total += behavior.AmbientWeight(ctx);
            }

            if (total <= 0f) return null;

            double roll = _rng.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++)
            {
                var behavior = pool[i];
                if (behavior.Drive != BehaviorDrive.Passive) continue;
                if (!IsReady(behavior)) continue;
                if (!behavior.IsEligible(ctx)) continue;
                if (behavior == LastPassive && pool.Count > 2) continue;

                roll -= behavior.AmbientWeight(ctx);
                if (roll <= 0) return behavior;
            }

            return null;
        }

        private void MarkUsed(PetBehavior behavior)
        {
            _readyAt[behavior.Id] = Time.time + Mathf.Max(1f, behavior.Cooldown);
        }
    }
}
