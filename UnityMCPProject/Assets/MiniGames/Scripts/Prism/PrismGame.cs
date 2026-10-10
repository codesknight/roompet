using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMiniGames
{
    /// <summary>
    /// A procedural tone, for the sequence-memory game. The pet game's synthesiser lives in a
    /// different assembly, so this small standalone version keeps the mini-games self-contained.
    /// </summary>
    public static class PrismAudio
    {
        public const int SampleRate = 44100;

        public static float[] Tone(float hz, float duration, float harmonic, float decay)
        {
            int count = Mathf.Max(16, Mathf.RoundToInt(duration * SampleRate));
            var samples = new float[count];
            float phase = 0f;
            float vibPhase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float freq = hz;
                if (harmonic > 1f)  // bells get a slow shimmer
                {
                    vibPhase += 2f * Mathf.PI * 6f / SampleRate;
                    freq *= 1f + Mathf.Sin(vibPhase) * 0.006f;
                }

                phase += 2f * Mathf.PI * freq / SampleRate;
                float v = Mathf.Sin(phase);
                if (harmonic > 0f) v += Mathf.Sin(phase * 2f) * harmonic;

                float attack = Mathf.Clamp01(t / 0.006f);
                float fall = Mathf.Exp(-t / Mathf.Max(0.02f, decay));
                samples[i] = Mathf.Clamp(v * attack * fall, -1f, 1f);
            }

            int fade = Mathf.Min(count / 2, Mathf.RoundToInt(0.006f * SampleRate));
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                samples[i] *= k;
                samples[count - 1 - i] *= k;
            }
            return samples;
        }

        public static AudioClip ToClip(string name, float[] samples)
        {
            if (samples == null || samples.Length == 0) samples = new float[64];
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }

    /// <summary>One clickable pad on the board.</summary>
    public class PrismPad : MonoBehaviour
    {
        public int Index;
        public PrismCombo Combo;
        public PrismGame Game;
        public Vector3 BaseScale = Vector3.one;

        private void OnMouseDown()
        {
            if (PrismHud.PointerOverPanel) return;
            if (Game != null) Game.TapPad(this);
        }
    }

    /// <summary>
    /// 棱镜: a sequence-memory game. Watch a sequence of coloured shapes light up and ring, then
    /// tap them back. One mistake ends the run; the score is the sequence length you reached.
    /// </summary>
    public class PrismGame : MonoBehaviour
    {
        public enum Phase { Menu, Playback, Input, Feedback, Result }

        public Phase State { get; private set; } = Phase.Menu;
        public PrismMode Mode { get; private set; } = PrismMode.Classic;
        public PrismDifficulty Difficulty { get; private set; } = PrismDifficulty.Rainbow;

        /// <summary>Best sequence length ever completed, persisted so difficulty tiers unlock.</summary>
        public int Best
        {
            get => PlayerPrefs.GetInt("dshprism.best", 0);
            private set => PlayerPrefs.SetInt("dshprism.best", value);
        }

        /// <summary>The score of the finished run (the last completed sequence length).</summary>
        public int Score { get; private set; }

        /// <summary>Coins the finished run earned.</summary>
        public int RunCoins => PrismRules.CoinsFor(Score);

        /// <summary>The length of the sequence being reproduced right now.</summary>
        public int CurrentLength { get; private set; }

        /// <summary>How many notes of the current sequence have been tapped correctly so far.</summary>
        public int InputStep { get; private set; }

        /// <summary>Seconds left in the Rush clock, 0 in Classic.</summary>
        public float RushClock { get; private set; }

        private Camera _camera;
        private AudioSource _source;
        private readonly List<PrismPad> _pads = new List<PrismPad>();
        private PrismCombo[] _sequence;
        private int _playIndex;
        private int _inputIndex;
        private float _timer;
        private int _runSeed;
        private int _completedLength;

        private float _feedbackTimer;
        private bool _feedbackRoundComplete;
        private PrismPad _flashPad;
        private float _flashTimer;
        private PrismPad _pulsePad;
        private float _pulseTimer;

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BuildCamera();
            BuildSource();
            State = Phase.Menu;
        }

        private void BuildCamera()
        {
            var existing = Camera.main;
            if (existing == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                existing = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            existing.orthographic = true;
            existing.transform.position = new Vector3(0f, 0f, -12f);
            existing.transform.rotation = Quaternion.identity;
            existing.clearFlags = CameraClearFlags.SolidColor;
            existing.backgroundColor = new Color(0.08f, 0.08f, 0.14f);
            existing.nearClipPlane = 0.1f;
            existing.farClipPlane = 60f;
            _camera = existing;

            if (FindObjectOfType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 1.05f;
                light.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.58f);
        }

        private void BuildSource()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        // ------------------------------------------------------------------ public controls

        public void StartRun(PrismDifficulty difficulty, PrismMode mode)
        {
            Difficulty = difficulty;
            Mode = mode;
            _runSeed = System.Environment.TickCount;
            _completedLength = 0;
            Score = 0;
            CurrentLength = PrismRules.StartLength;
            BuildBoard();
            StartRound();
        }

        public void TapPad(PrismPad pad)
        {
            if (State != Phase.Input) return;
            if (pad == null) return;

            PlayCombo(pad.Combo);

            if (_sequence == null || _inputIndex >= _sequence.Length) return;

            if (pad.Combo == _sequence[_inputIndex])
            {
                _inputIndex++;
                InputStep = _inputIndex;
                FlashPad(pad, new Color(0.35f, 1f, 0.5f));
                DshMobile.MobileHaptics.Light();

                if (_inputIndex >= _sequence.Length)
                {
                    _completedLength = CurrentLength;
                    Score = PrismRules.ScoreFor(_completedLength);
                    CurrentLength++;
                    _feedbackRoundComplete = true;
                    _feedbackTimer = 0.45f;
                    State = Phase.Feedback;
                }
            }
            else
            {
                FlashPad(pad, new Color(1f, 0.35f, 0.35f));
                _feedbackRoundComplete = false;
                _feedbackTimer = 0.6f;
                State = Phase.Feedback;
            }
        }

        public void ReturnToRoom()
        {
            DshMobile.SceneClock.Restore("leaving the prism game");
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }

        /// <summary>Back to the menu, clearing the board built for the finished run.</summary>
        public void BackToMenu()
        {
            State = Phase.Menu;
            foreach (var pad in _pads)
            {
                if (pad != null) Destroy(pad.gameObject);
            }
            _pads.Clear();
        }

        // ------------------------------------------------------------------ board

        private void BuildBoard()
        {
            foreach (var pad in _pads)
            {
                if (pad != null) Destroy(pad.gameObject);
            }
            _pads.Clear();

            int count = PrismRules.ComboCountFor(Difficulty);
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));   // near-square board, fits a phone
            int rows = Mathf.CeilToInt(count / (float)cols);
            float stepX = 1.25f;
            float stepY = 1.25f;
            float originX = -(cols - 1) * stepX * 0.5f;
            float originY = (rows - 1) * stepY * 0.5f;

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                var combo = PrismRules.ComboAt(Difficulty, i);
                var go = BuildShape(combo);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(originX + col * stepX, originY - row * stepY, 0f);

                var pad = go.AddComponent<PrismPad>();
                pad.Index = i;
                pad.Combo = combo;
                pad.Game = this;
                pad.BaseScale = go.transform.localScale;
                _pads.Add(pad);
            }

            FitCamera(rows, cols);
        }

        private GameObject BuildShape(PrismCombo combo)
        {
            GameObject go;
            switch (combo.Shape)
            {
                case PrismShape.Cube: go = GameObject.CreatePrimitive(PrimitiveType.Cube); break;
                case PrismShape.Cone: go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                case PrismShape.Cylinder: go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                case PrismShape.Capsule: go = GameObject.CreatePrimitive(PrimitiveType.Capsule); break;
                default: go = GameObject.CreatePrimitive(PrimitiveType.Sphere); break;
            }

            // Sphere, cylinder, cone and capsule read as "cone" only when squashed differently;
            // give each a distinct silhouette so the shapes are told apart at a glance.
            switch (combo.Shape)
            {
                case PrismShape.Cone: go.transform.localScale = new Vector3(0.68f, 0.28f, 0.68f); break; // puck
                case PrismShape.Cylinder: go.transform.localScale = new Vector3(0.44f, 0.74f, 0.44f); break; // tall
                case PrismShape.Capsule: go.transform.localScale = new Vector3(0.5f, 0.72f, 0.5f); break;
                case PrismShape.Sphere: go.transform.localScale = Vector3.one * 0.66f; break;
                default: go.transform.localScale = Vector3.one * 0.62f; break;
            }

            var color = ColorOf(combo.Color);
            var renderer = go.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Standard"))
            {
                name = "Prism_" + combo
            };
            mat.color = color;
            if (combo.Fill == PrismFill.Bell)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.55f);
            }
            renderer.sharedMaterial = mat;

            // Pads must be clickable.
            var collider = go.GetComponent<Collider>();
            if (collider == null) go.AddComponent<BoxCollider>();
            return go;
        }

        private void FitCamera(int rows, int cols)
        {
            // The board must fit whatever the screen's aspect is, portrait included. The camera
            // shows orthographicSize vertically; horizontally it shows that × aspect.
            float neededHalfW = (cols - 1) * 0.625f + 0.9f;
            float neededHalfH = (rows - 1) * 0.625f + 0.9f;
            float aspect = _camera != null ? _camera.aspect : 1.6f;
            _camera.orthographicSize = Mathf.Max(neededHalfH, neededHalfW / Mathf.Max(0.1f, aspect));
        }

        private static Color ColorOf(PrismColor color)
        {
            switch (color)
            {
                case PrismColor.Red: return new Color(0.92f, 0.28f, 0.28f);
                case PrismColor.Orange: return new Color(0.95f, 0.55f, 0.18f);
                case PrismColor.Yellow: return new Color(0.95f, 0.85f, 0.22f);
                case PrismColor.Green: return new Color(0.35f, 0.80f, 0.40f);
                case PrismColor.Blue: return new Color(0.30f, 0.55f, 0.92f);
                default: return new Color(0.62f, 0.38f, 0.92f);
            }
        }

        // ------------------------------------------------------------------ rounds

        private void StartRound()
        {
            _sequence = PrismRules.Sequence(_runSeed, CurrentLength, Difficulty);
            _playIndex = 0;
            _inputIndex = 0;
            InputStep = 0;
            _flashPad = null;
            _flashTimer = 0f;
            _pulsePad = null;
            _pulseTimer = 0f;
            _timer = 0.55f;
            State = Phase.Playback;
        }

        private void Update()
        {
            TickFlash();

            if (State == Phase.Feedback)
            {
                _feedbackTimer -= Time.deltaTime;
                if (_feedbackTimer <= 0f)
                {
                    if (_feedbackRoundComplete) StartRound();
                    else EndRun();
                }
                return;
            }

            if (State != Phase.Playback && State != Phase.Input) return;

            if (State == Phase.Playback)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    Highlight(_sequence[_playIndex]);
                    _playIndex++;
                    _timer = PrismRules.NoteSeconds + 0.26f;
                    if (_playIndex >= _sequence.Length)
                    {
                        _timer = 0.3f;
                        ResetPadScales();
                        State = Phase.Input;
                        _inputIndex = 0;
                        InputStep = 0;
                        RushClock = Mode == PrismMode.Rush ? PrismRules.StartLength * 1.4f + CurrentLength * 0.55f : 0f;
                    }
                }
            }
            else if (Mode == PrismMode.Rush && RushClock > 0f)
            {
                RushClock -= Time.deltaTime;
                if (RushClock <= 0f) EndRun();
            }
        }

        private void Highlight(PrismCombo combo)
        {
            ResetPadScales();
            foreach (var pad in _pads)
            {
                if (pad == null) continue;
                if (pad.Combo == combo)
                {
                    pad.transform.localScale = pad.BaseScale * 1.35f;
                    Brighten(pad, true);
                    // A short pulse: the pad lights up and then drops back, so when the SAME pad
                    // appears twice in a row the two flashes are clearly two blinks, not one long one.
                    _pulsePad = pad;
                    _pulseTimer = 0.30f;
                }
                else
                {
                    Brighten(pad, false);
                }
            }
            PlayCombo(combo);
        }

        /// <summary>Flashes a tapped pad green or red, then restores it after a moment.</summary>
        private void FlashPad(PrismPad pad, Color color)
        {
            if (pad == null) return;
            _flashPad = pad;
            _flashTimer = 0.30f;
            pad.transform.localScale = pad.BaseScale * 1.3f;

            var renderer = pad.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial.color = color;
                renderer.sharedMaterial.EnableKeyword("_EMISSION");
                renderer.sharedMaterial.SetColor("_EmissionColor", color * 0.9f);
            }
        }

        private void TickFlash()
        {
            // Playback pulse: drop a highlighted pad back to base after a short blink.
            if (_pulsePad != null)
            {
                _pulseTimer -= Time.deltaTime;
                if (_pulseTimer <= 0f)
                {
                    var pulse = _pulsePad;
                    _pulsePad = null;
                    if (pulse != null)
                    {
                        pulse.transform.localScale = pulse.BaseScale;
                        RestoreColor(pulse);
                    }
                }
            }

            // Tap feedback flash: green/red, then restore.
            if (_flashPad == null || _flashTimer <= 0f) return;
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                var pad = _flashPad;
                _flashPad = null;
                if (pad != null)
                {
                    pad.transform.localScale = pad.BaseScale;
                    RestoreColor(pad);
                }
            }
        }

        /// <summary>Briefly brightens a pad (its own colour, slightly whiter) during playback.</summary>
        private void Brighten(PrismPad pad, bool on)
        {
            var renderer = pad.GetComponent<Renderer>();
            if (renderer == null) return;
            if (on)
            {
                var baseColor = ColorOf(pad.Combo.Color);
                renderer.sharedMaterial.color = Color.Lerp(baseColor, Color.white, 0.45f);
                renderer.sharedMaterial.EnableKeyword("_EMISSION");
                renderer.sharedMaterial.SetColor("_EmissionColor", baseColor * 0.8f);
            }
            else
            {
                RestoreColor(pad);
            }
        }

        private void RestoreColor(PrismPad pad)
        {
            var renderer = pad.GetComponent<Renderer>();
            if (renderer == null) return;
            var baseColor = ColorOf(pad.Combo.Color);
            renderer.sharedMaterial.color = baseColor;
            if (pad.Combo.Fill == PrismFill.Bell)
            {
                renderer.sharedMaterial.EnableKeyword("_EMISSION");
                renderer.sharedMaterial.SetColor("_EmissionColor", baseColor * 0.55f);
            }
            else
            {
                renderer.sharedMaterial.DisableKeyword("_EMISSION");
            }
        }

        private void ResetPadScales()
        {
            foreach (var pad in _pads)
            {
                if (pad != null) pad.transform.localScale = pad.BaseScale;
            }
        }

        private void PlayCombo(PrismCombo combo)
        {
            float hz = PrismRules.FrequencyOf(combo);
            float harmonic = PrismRules.HarmonicOf(combo.Fill);
            float decay = PrismRules.DecayOf(combo.Fill);
            var clip = PrismAudio.ToClip("prism", PrismAudio.Tone(hz, PrismRules.NoteSeconds, harmonic, decay));
            if (_source != null)
            {
                _source.Stop();
                _source.PlayOneShot(clip);
            }
        }

        private void EndRun()
        {
            Score = PrismRules.ScoreFor(_completedLength);
            if (_completedLength > Best) Best = _completedLength;
            if (RunCoins > 0) DshMobile.PetWallet.Add(RunCoins);
            State = Phase.Result;
            DshMobile.MobileHaptics.Medium();
        }
    }
}
