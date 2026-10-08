using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Pets noticing each other.
    ///
    /// The interesting constraint is that this must cost nothing. Two pets exchanging a line is
    /// charming; two pets exchanging a line through the language model is a bill the player pays
    /// for every few seconds of watching their own room. So the exchanges are a local table,
    /// picked by the two temperaments involved — and the primary pet only joins in locally too,
    /// because it is the player's *conversations* that are worth a model call, not the ambient
    /// chatter between animals.
    ///
    /// Pure selection logic, so "a playful pair plays and a shy pair keeps its distance" is a
    /// test rather than a vibe.
    /// </summary>
    public static class PetChatter
    {
        /// <summary>One exchange: what the two pets do about each other.</summary>
        public enum Exchange
        {
            Greet,      // a sniff and a nod
            Play,       // chase and tumble
            Share,      // one gives way at the bowl
            Squabble,   // a hiss and a swipe
            Ignore,     // pointedly do not notice each other
            Cuddle      // flop down together
        }

        /// <summary>Minimum seconds between two exchanges in the same room.</summary>
        public const float CooldownMin = 22f;
        public const float CooldownMax = 46f;

        /// <summary>
        /// How close two pets have to be for them to notice each other.
        ///
        /// Well under the room's half-width on purpose: an exchange across the room would read
        /// as telepathy.
        /// </summary>
        public const float NoticeRange = 3.2f;

        /// <summary>
        /// Picks the exchange from the two temperaments. <paramref name="roll"/> keeps it
        /// testable.
        /// </summary>
        public static Exchange Choose(PetPersonality a, PetPersonality b, float roll)
        {
            a = a ?? new PetPersonality();
            b = b ?? new PetPersonality();

            float liveliness = (a.Liveliness + b.Liveliness) * 0.5f;
            float clinginess = (a.Clinginess + b.Clinginess) * 0.5f;
            float neatness = (a.Neatness + b.Neatness) * 0.5f;
            float curiosity = (a.Curiosity + b.Curiosity) * 0.5f;

            float wGreet = 0.6f + curiosity * 1.3f;
            float wPlay = 0.25f + liveliness * 2.0f;
            float wShare = 0.35f + clinginess * 1.2f + neatness * 0.3f;
            float wSquabble = 0.25f + (1f - clinginess) * 0.9f + (1f - liveliness) * 0.4f;
            float wIgnore = 0.55f + (1f - clinginess) * 1.1f;
            float wCuddle = 0.20f + clinginess * 2.0f + (1f - liveliness) * 0.5f;

            float total = wGreet + wPlay + wShare + wSquabble + wIgnore + wCuddle;
            float target = Mathf.Clamp01(roll) * total;

            target -= wGreet; if (target <= 0f) return Exchange.Greet;
            target -= wPlay; if (target <= 0f) return Exchange.Play;
            target -= wCuddle; if (target <= 0f) return Exchange.Cuddle;
            target -= wShare; if (target <= 0f) return Exchange.Share;
            target -= wSquabble; if (target <= 0f) return Exchange.Squabble;
            return Exchange.Ignore;
        }

        /// <summary>
        /// The lines for an exchange, as { first pet, second pet }.
        ///
        /// Stage directions rather than dialogue, because two pets that talk in full sentences
        /// stop reading as animals — and because the player's relationship is with *their* pet,
        /// which is the one that gets the language model.
        /// </summary>
        public static string[] Lines(Exchange exchange, string aName, string bName)
        {
            switch (exchange)
            {
                case Exchange.Greet:
                    return new[] { $"（{aName}凑过去闻了闻{bName}的鼻子）", $"（{bName}也回敬了一下）" };
                case Exchange.Play:
                    return new[] { $"（{aName}弓起背，{bName}立刻懂了，两只在屋里追成一团）", "" };
                case Exchange.Share:
                    return new[] { $"（{aName}把碗边让给了{bName}）", $"（{bName}吃得很香）" };
                case Exchange.Squabble:
                    return new[] { $"（{aName}炸了一下毛，{bName}不甘示弱地哼了一声）", "" };
                case Exchange.Cuddle:
                    return new[] { $"（{aName}挨着{bName}趴下了，尾巴搭在一起）", "" };
                default:
                    return new[] { $"（{aName}和{bName}互相看了一眼，然后各自走开）", "" };
            }
        }

        /// <summary>Joy for each pet, in the same order as the pair.</summary>
        public static float JoyDelta(Exchange exchange)
        {
            switch (exchange)
            {
                case Exchange.Play: return 0.05f;
                case Exchange.Cuddle: return 0.04f;
                case Exchange.Greet: return 0.02f;
                case Exchange.Share: return 0.015f;
                case Exchange.Squabble: return -0.01f;
                default: return 0f;
            }
        }

        /// <summary>Kind of journal entry this exchange deserves.</summary>
        public static string JournalTitle(Exchange exchange, string aName, string bName)
        {
            switch (exchange)
            {
                case Exchange.Play: return $"{aName}和{bName}玩起来了";
                case Exchange.Cuddle: return $"{aName}和{bName}挤在一起睡了";
                case Exchange.Share: return $"{aName}让{bName}先吃";
                case Exchange.Squabble: return $"{aName}和{bName}闹别扭";
                case Exchange.Greet: return $"{aName}和{bName}打了个招呼";
                default: return $"{aName}和{bName}各玩各的";
            }
        }

        /// <summary>The next gap between exchanges, so the room does not chatter constantly.</summary>
        public static float NextDelay(System.Random rng, float liveliness)
        {
            float t = rng != null ? (float)rng.NextDouble() : 0.5f;
            float baseDelay = Mathf.Lerp(CooldownMin, CooldownMax, t);

            // Lively households interact sooner; a room of sleepy cats mostly ignores itself.
            return baseDelay * Mathf.Lerp(1.25f, 0.75f, Mathf.Clamp01(liveliness));
        }
    }
}
