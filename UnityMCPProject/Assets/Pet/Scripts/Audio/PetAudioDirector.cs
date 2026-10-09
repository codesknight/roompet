using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    public enum SfxId
    {
        UiClick,
        Jump,
        Land,
        Eat,
        Drink,
        BallBounce,
        Throw,
        Brush,
        Door,
        Happy,
        Sleepy,
        Error,
        Bird,
        PickUp,
        Wag,
        Footstep,

        /// <summary>Water splashing: the bath, and the pet shaking itself dry.</summary>
        Splash,

        /// <summary>The small unhappy whine used for accidents and grumbling.</summary>
        Whine
    }

    /// <summary>
    /// Owns every sound the pet game makes. All clips are synthesised on first use, so the
    /// project ships with no audio assets at all.
    ///
    /// The pet's voice is the interesting part: instead of one recorded meow it builds a
    /// chirp from the pet's mood, which is what makes the "talking pet" feel connected to
    /// the simulation rather than pasted on top of it.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class PetAudioDirector : MonoBehaviour
    {
        public static PetAudioDirector Instance { get; private set; }

        [Header("Levels")]
        [Range(0f, 1f)] public float MasterVolume = 0.7f;
        [Range(0f, 1f)] public float SfxVolume = 0.8f;
        [Range(0f, 1f)] public float VoiceVolume = 0.9f;
        [Range(0f, 1f)] public float AmbientVolume = 0.35f;

        public bool Muted { get; private set; }

        /// <summary>
        /// The mute switch as a plain setting, without needing an audio director to exist.
        ///
        /// The director lives in the pet's room, and the front door is a different scene — but the
        /// setting is the player's, not the scene's, so it is read and written here through the same
        /// key the director loads at startup.
        /// </summary>
        public static bool MutedSetting
        {
            get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (Instance != null) Instance.SetMuted(value);
            }
        }

        /// <summary>Last sound played, shown in the HUD so the system is inspectable.</summary>
        public string LastPlayed { get; private set; } = "";

        private AudioSource _sfx;
        private AudioSource _voice;
        private AudioSource _ambient;
        private AudioSource _chirp;

        private readonly Dictionary<SfxId, AudioClip> _clips = new Dictionary<SfxId, AudioClip>();
        private readonly Dictionary<string, AudioClip> _voices = new Dictionary<string, AudioClip>();
        private AudioClip _ambientClip;

        private float _birdTimer;
        private readonly System.Random _rng = new System.Random();

        private const string MuteKey = "dshpet.audio.muted";
        private const string VolumeKey = "dshpet.audio.volume";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            MasterVolume = PlayerPrefs.GetFloat(VolumeKey, MasterVolume);

            _sfx = MakeSource("Sfx", SfxVolume);
            _voice = MakeSource("Voice", VoiceVolume);
            _chirp = MakeSource("Chirp", SfxVolume * 0.5f);
            _ambient = MakeSource("Ambient", AmbientVolume);
            _ambient.loop = true;

            _birdTimer = 6f;
        }

        /// <summary>
        /// Clears the singleton.
        ///
        /// Without this, a destroyed director stays reachable through <see cref="Instance"/> —
        /// its AudioSources are gone but the reference is not — and the next Play call throws
        /// on a destroyed object. It bites for real: the room scene unloads and reloads on every
        /// trip to a mini game.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private AudioSource MakeSource(string name, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // 2D: always audible, no listener maths
            source.volume = volume;
            return source;
        }

        private void Start()
        {
            _ambientClip = ProceduralAudio.ToClip("pet_ambient",
                BuildAmbient(4f), true);
            _ambient.clip = _ambientClip;
            if (!Muted) _ambient.Play();
        }

        private void Update()
        {
            if (Muted) return;

            // Occasional birdsong so the room is not dead air.
            _birdTimer -= Time.unscaledDeltaTime;
            if (_birdTimer <= 0f)
            {
                _birdTimer = Mathf.Lerp(9f, 26f, (float)_rng.NextDouble());
                var clip = Clip(SfxId.Bird);
                if (clip != null) _chirp.PlayOneShot(clip, SfxVolume * 0.35f * MasterVolume);
            }
        }

        // ------------------------------------------------------------------ playback

        public void Play(SfxId id, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (Muted) return;

            var clip = Clip(id);
            if (clip == null) return;

            LastPlayed = id.ToString();
            if (_sfx == null) return;   // destroyed with a previous scene; never throw over a beep
            _sfx.pitch = 1f + ((float)_rng.NextDouble() * 2f - 1f) * pitchJitter;
            _sfx.PlayOneShot(clip, Mathf.Clamp01(volume) * SfxVolume * MasterVolume);
        }

        /// <summary>The pet's voice, coloured by how it feels. Slight random detune per call
        /// keeps repeated lines from sounding looped.</summary>
        public void Speak(PetMood mood, float volume = 1f)
        {
            var gm = PetGameManager.Instance;
            Speak(gm != null ? gm.Species : null, gm != null ? gm.Personality : null, mood, volume);
        }

        /// <summary>
        /// The pet's voice, in its own register.
        ///
        /// Takes the species and temperament explicitly rather than reading them off the
        /// manager, so the companions walking around the room (which have no manager state of
        /// their own) can speak in their own voice too.
        /// </summary>
        public void Speak(PetSpecies species, PetPersonality personality, PetMood mood, float volume = 1f)
        {
            if (Muted || !PetVoice.Enabled) return;

            var clip = VoiceFor(species, personality, mood);
            if (clip == null) return;

            LastPlayed = "voice:" + mood;
            if (_voice == null) return;
            _voice.pitch = 0.94f + (float)_rng.NextDouble() * 0.14f;
            _voice.PlayOneShot(clip, Mathf.Clamp01(volume) * VoiceVolume * MasterVolume);
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();

            if (_ambient == null) return;
            if (muted) _ambient.Stop();
            else if (_ambientClip != null && !_ambient.isPlaying) _ambient.Play();
        }

        public void ToggleMute() => SetMuted(!Muted);

        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
            PlayerPrefs.Save();
        }

        // --------------------------------------------------------------------- clips

        private AudioClip Clip(SfxId id)
        {
            if (_clips.TryGetValue(id, out var cached) && cached != null) return cached;

            float[] samples;
            switch (id)
            {
                case SfxId.UiClick:
                    samples = ProceduralAudio.Tone(1800f, 1400f, 0.05f, 0.001f, 0.02f, 0.4f);
                    break;

                case SfxId.Jump:
                    samples = ProceduralAudio.Tone(320f, 760f, 0.16f, 0.004f, 0.09f, 0.35f);
                    break;

                case SfxId.Land:
                    samples = ProceduralAudio.Noise(0.11f, NoiseColor.Brown, 0.001f, 0.035f, 77);
                    ProceduralAudio.Mix(samples, ProceduralAudio.Tone(180f, 90f, 0.1f, 0.001f, 0.03f), 0.6f);
                    break;

                case SfxId.Eat:
                {
                    // Three crunchy bites.
                    var bite = ProceduralAudio.Noise(0.09f, NoiseColor.Pink, 0.002f, 0.03f, 21);
                    samples = ProceduralAudio.Concat(bite,
                        ProceduralAudio.Concat(new float[1800], bite),
                        ProceduralAudio.Concat(new float[2200], bite));
                    break;
                }

                case SfxId.Drink:
                {
                    var gulp = ProceduralAudio.Tone(240f, 150f, 0.09f, 0.006f, 0.035f, 0.5f);
                    samples = ProceduralAudio.Concat(gulp, ProceduralAudio.Concat(new float[2600], gulp),
                        ProceduralAudio.Concat(new float[2400], gulp));
                    break;
                }

                case SfxId.BallBounce:
                    samples = ProceduralAudio.Tone(520f, 190f, 0.13f, 0.001f, 0.045f, 0.45f);
                    break;

                case SfxId.Throw:
                    samples = ProceduralAudio.Noise(0.22f, NoiseColor.Pink, 0.05f, 0.09f, 909);
                    break;

                case SfxId.Brush:
                {
                    var swish = ProceduralAudio.Noise(0.14f, NoiseColor.Pink, 0.05f, 0.05f, 313);
                    samples = ProceduralAudio.Concat(swish, ProceduralAudio.Concat(new float[3000], swish));
                    break;
                }

                case SfxId.Door:
                {
                    var creak = ProceduralAudio.Tone(180f, 330f, 0.4f, 0.12f, 0.16f, 0.55f, 7f, 0.02f);
                    samples = ProceduralAudio.Concat(creak,
                        ProceduralAudio.Tone(900f, 500f, 0.08f, 0.001f, 0.03f, 0.4f));
                    break;
                }

                case SfxId.Happy:
                    samples = ProceduralAudio.Arpeggio(new[] { 523f, 659f, 784f }, 0.075f);
                    break;

                case SfxId.Sleepy:
                    samples = ProceduralAudio.Tone(360f, 180f, 0.55f, 0.08f, 0.24f, 0.18f, 4f, 0.01f);
                    break;

                case SfxId.Error:
                    samples = ProceduralAudio.Tone(200f, 150f, 0.24f, 0.004f, 0.1f, 0.75f);
                    break;

                case SfxId.PickUp:
                    samples = ProceduralAudio.Tone(700f, 1100f, 0.09f, 0.002f, 0.03f, 0.4f);
                    break;

                case SfxId.Footstep:
                    samples = ProceduralAudio.Noise(0.06f, NoiseColor.Brown, 0.001f, 0.018f, 5150);
                    break;

                case SfxId.Wag:
                    samples = ProceduralAudio.Arpeggio(new[] { 620f, 760f }, 0.06f, 0.22f);
                    break;

                case SfxId.Splash:
                {
                    // A downward chirp over pink noise: the chirp alone sounds like a synth
                    // blip, the noise alone like static, and together they read as water.
                    var spray = ProceduralAudio.Noise(0.34f, NoiseColor.Pink, 0.002f, 0.16f, 7411);
                    var drop = ProceduralAudio.Tone(1400f, 320f, 0.18f, 0.001f, 0.07f, 0.35f);
                    samples = ProceduralAudio.Mix(spray, drop, 0.55f);
                    break;
                }

                case SfxId.Whine:
                    samples = ProceduralAudio.Tone(520f, 300f, 0.42f, 0.05f, 0.2f, 0.28f, 5f, 0.03f);
                    break;

                case SfxId.Bird:
                {
                    var a = ProceduralAudio.Tone(2400f, 3100f, 0.07f, 0.004f, 0.025f, 0.15f, 40f, 0.04f);
                    var b = ProceduralAudio.Tone(2700f, 2100f, 0.09f, 0.004f, 0.03f, 0.15f, 34f, 0.04f);
                    samples = ProceduralAudio.Concat(a, b);
                    break;
                }

                default:
                    samples = ProceduralAudio.Tone(440f, 440f, 0.1f);
                    break;
            }

            ProceduralAudio.Normalize(samples, 0.8f);
            var clip = ProceduralAudio.ToClip("pet_" + id, samples);
            _clips[id] = clip;
            return clip;
        }

        /// <summary>
        /// The pet's chirp, built from its own voice profile.
        ///
        /// The profile carries everything the sound needs — register, slide, wobble, syllable
        /// count — so this is a synthesiser rather than a table of moods, and a cat and a bear
        /// are recognisable without any recorded audio. See <see cref="PetVoice"/> for where
        /// those numbers come from.
        /// </summary>
        public AudioClip VoiceFor(PetSpecies species, PetPersonality personality, PetMood mood)
        {
            if (!PetVoice.Enabled) return null;

            var profile = PetVoice.For(species, personality, mood);
            string key = VoiceKey(species, profile);
            if (_voices.TryGetValue(key, out var cached) && cached != null) return cached;

            var parts = new float[Mathf.Max(1, profile.Syllables)][];
            for (int i = 0; i < parts.Length; i++)
            {
                // Each syllable starts a little higher and lands on the slide, so a multi-part
                // call reads as speech rather than as the same blip repeated.
                float start = profile.BaseHz * (1f + i * 0.05f);
                float end = start + profile.SlideHz;
                parts[i] = ProceduralAudio.Tone(start, end, profile.SyllableSeconds,
                    0.02f, profile.SyllableSeconds * 0.5f, profile.Harmonic,
                    profile.VibratoHz, profile.VibratoDepth);
            }

            var samples = parts.Length == 1 ? parts[0] : ProceduralAudio.Concat(parts);
            ProceduralAudio.Normalize(samples, profile.Gain);

            var clip = ProceduralAudio.ToClip("pet_voice_" + key, samples);
            _voices[key] = clip;
            return clip;
        }

        private static string VoiceKey(PetSpecies species, PetVoiceProfile profile)
            => $"{(species != null ? species.Id : "x")}_{profile.BaseHz:F0}_{profile.SlideHz:F0}_" +
               $"{profile.Syllables}_{profile.SyllableSeconds:F2}_{profile.Harmonic:F2}";

        /// <summary>A quiet room bed: filtered noise with a slow swell, looped.</summary>
        private static float[] BuildAmbient(float seconds)
        {
            int count = Mathf.RoundToInt(seconds * ProceduralAudio.SampleRate);
            var samples = ProceduralAudio.Noise(seconds, NoiseColor.Brown, 0.35f, seconds * 0.9f, 4242);

            // A slow tremolo stops it reading as a static hiss.
            for (int i = 0; i < count && i < samples.Length; i++)
            {
                float t = i / (float)ProceduralAudio.SampleRate;
                float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / seconds);
                samples[i] *= swell * 0.35f;
            }

            ProceduralAudio.Normalize(samples, 0.5f);

            // Cross-fade the seam so the loop point is inaudible.
            int fade = Mathf.Min(count / 4, ProceduralAudio.SampleRate / 2);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                samples[i] = Mathf.Lerp(samples[count - fade + i], samples[i], k);
            }

            return samples;
        }
    }
}
