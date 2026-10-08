using System;
using UnityEngine;

namespace DshPet
{
    public enum NoiseColor { White, Pink, Brown }

    /// <summary>
    /// Procedural audio synthesis. Everything the pet game plays is generated here at
    /// runtime, so the project needs no sound assets, no network, and no licence — and the
    /// pet's voice can be parameterised by mood instead of being a fixed recorded clip.
    ///
    /// Pure sample maths with no Unity object creation, so it is unit testable.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        /// <summary>Clamp and remove the impulse a bad envelope can produce.</summary>
        public static float[] Normalize(float[] samples, float peak = 0.85f)
        {
            if (samples == null || samples.Length == 0) return samples;

            float max = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float value = Mathf.Abs(samples[i]);
                if (value > max) max = value;
            }

            if (max <= 0.0001f) return samples;

            float gain = peak / max;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
            }
            return samples;
        }

        /// <summary>Short fade at both ends so a clip never clicks.</summary>
        public static void ApplyEdgeFade(float[] samples, float seconds = 0.004f)
        {
            if (samples == null || samples.Length == 0) return;

            int fade = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            fade = Mathf.Min(fade, samples.Length / 2);

            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                samples[i] *= k;
                samples[samples.Length - 1 - i] *= k;
            }
        }

        public static AudioClip ToClip(string name, float[] samples, bool loop = false)
        {
            if (samples == null || samples.Length == 0) samples = new float[64];
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>Exponential-ish attack/decay envelope, 0..1.</summary>
        public static float Envelope(float t, float duration, float attack, float decay)
        {
            if (duration <= 0f) return 0f;
            float a = Mathf.Max(0.0005f, attack);
            float d = Mathf.Max(0.0005f, decay);

            float rise = t < a ? t / a : 1f;
            float fall = Mathf.Exp(-(t - a) / d);
            return Mathf.Clamp01(rise * fall);
        }

        public static float[] Tone(float startHz, float endHz, float duration,
            float attack = 0.005f, float decay = 0.08f, float harmonic = 0.25f, float vibratoHz = 0f,
            float vibratoDepth = 0f, float phaseOffset = 0f)
        {
            int count = Mathf.Max(8, Mathf.RoundToInt(duration * SampleRate));
            var samples = new float[count];
            float phase = phaseOffset;
            float vibPhase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float k = count <= 1 ? 0f : i / (float)(count - 1);

                float hz = Mathf.Lerp(startHz, endHz, k);
                if (vibratoHz > 0f && vibratoDepth > 0f)
                {
                    vibPhase += 2f * Mathf.PI * vibratoHz / SampleRate;
                    hz *= 1f + Mathf.Sin(vibPhase) * vibratoDepth;
                }

                phase += 2f * Mathf.PI * hz / SampleRate;
                float value = Mathf.Sin(phase);
                if (harmonic > 0f) value += Mathf.Sin(phase * 2f) * harmonic;

                samples[i] = value * Envelope(t, duration, attack, decay);
            }

            ApplyEdgeFade(samples);
            return samples;
        }

        public static float[] Noise(float duration, NoiseColor color = NoiseColor.White,
            float attack = 0.002f, float decay = 0.06f, int seed = 12345)
        {
            int count = Mathf.Max(8, Mathf.RoundToInt(duration * SampleRate));
            var samples = new float[count];
            var rng = new System.Random(seed);

            float white = 0f, brown = 0f, pinkA = 0f, pinkB = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);

                white = n;
                brown = Mathf.Clamp(brown + n * 0.02f, -1f, 1f) * 0.995f;
                pinkA = 0.99765f * pinkA + n * 0.0990460f;
                pinkB = 0.96300f * pinkB + n * 0.2965164f;

                float value;
                switch (color)
                {
                    case NoiseColor.Brown: value = brown * 3.5f; break;
                    case NoiseColor.Pink: value = (pinkA + pinkB + n * 0.1848f) * 0.4f; break;
                    default: value = white; break;
                }

                samples[i] = value * Envelope(t, duration, attack, decay);
            }

            ApplyEdgeFade(samples);
            return samples;
        }

        /// <summary>Mixes <paramref name="addition"/> into <paramref name="target"/> at an offset.</summary>
        public static void Mix(float[] target, float[] addition, float gain = 1f, int offsetSamples = 0)
        {
            if (target == null || addition == null) return;
            for (int i = 0; i < addition.Length; i++)
            {
                int index = i + offsetSamples;
                if (index < 0 || index >= target.Length) continue;
                target[index] += addition[i] * gain;
            }
        }

        public static float[] Concat(params float[][] parts)
        {
            int total = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null) total += parts[i].Length;
            }

            var result = new float[Mathf.Max(8, total)];
            int at = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                Array.Copy(parts[i], 0, result, at, parts[i].Length);
                at += parts[i].Length;
            }
            return result;
        }

        /// <summary>A short arpeggio, used for happy moments and level-ish feedback.</summary>
        public static float[] Arpeggio(float[] notesHz, float noteSeconds = 0.09f, float harmonic = 0.3f)
        {
            var parts = new float[notesHz.Length][];
            for (int i = 0; i < notesHz.Length; i++)
            {
                parts[i] = Tone(notesHz[i], notesHz[i] * 1.01f, noteSeconds, 0.004f, noteSeconds * 0.5f, harmonic);
            }
            return Concat(parts);
        }

        /// <summary>
        /// Overlays two sounds instead of playing them one after another.
        ///
        /// Concat is a sequence; some effects only work stacked — water is noise (the spray)
        /// with a falling tone inside it (the droplet), and either half on its own sounds like
        /// a synth patch rather than water.
        /// </summary>
        public static float[] Mix(float[] a, float[] b, float bGain = 1f)
        {
            int length = Mathf.Max(a != null ? a.Length : 0, b != null ? b.Length : 0);
            var result = new float[Mathf.Max(8, length)];

            for (int i = 0; i < result.Length; i++)
            {
                float value = 0f;
                if (a != null && i < a.Length) value += a[i];
                if (b != null && i < b.Length) value += b[i] * bGain;
                result[i] = Mathf.Clamp(value, -1f, 1f);
            }

            return result;
        }
    }
}
