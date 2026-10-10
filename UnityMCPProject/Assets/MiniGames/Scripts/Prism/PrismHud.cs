using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// 棱镜's interface: a menu that picks the difficulty and mode, a live score, and a result
    /// panel. IMGUI like every other screen in the project, scaled to the phone's safe area.
    /// </summary>
    public class PrismHud : MonoBehaviour
    {
        private PrismGame _game;
        private GUIStyle _title;
        private GUIStyle _big;
        private GUIStyle _small;
        private GUIStyle _button;
        private GUIStyle _centered;

        private PrismDifficulty _selectedDifficulty = PrismDifficulty.Rainbow;
        private PrismMode _selectedMode = PrismMode.Classic;

        /// <summary>True while the pointer is over a panel, so the game ignores that tap.</summary>
        public static bool PointerOverPanel { get; private set; }

        private void Awake() => _game = GetComponent<PrismGame>();

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.9f, 0.86f, 1f);
            _big = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big.normal.textColor = new Color(1f, 0.92f, 0.62f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _small.normal.textColor = new Color(0.92f, 0.92f, 0.98f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(14, 14, 8, 8) };
            _centered = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            _centered.normal.textColor = new Color(0.92f, 0.92f, 0.98f);
        }

        // ------------------------------------------------------------------ nicer buttons

        private static readonly System.Collections.Generic.Dictionary<Color, Texture2D> _roundCache =
            new System.Collections.Generic.Dictionary<Color, Texture2D>();

        /// <summary>A soft rounded-rect button background, shaded a touch lighter at the top.</summary>
        private static Texture2D Rounded(Color fill)
        {
            Texture2D cached;
            if (_roundCache.TryGetValue(fill, out cached) && cached != null) return cached;

            const int w = 96, h = 48;
            const float radius = 14f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - radius), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    if (alpha <= 0f) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                    float t = 1f - (float)y / h;
                    Color c = Color.Lerp(fill, Color.white, t * 0.25f);
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, alpha));
                }
            }
            tex.Apply();
            _roundCache[fill] = tex;
            return tex;
        }

        /// <summary>A coloured rounded button, tinted by <paramref name="fill"/>.</summary>
        private GUIStyle MakeButton(Color fill)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                padding = new RectOffset(14, 14, 10, 10),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            style.normal.background = Rounded(fill);
            style.hover.background = Rounded(Color.Lerp(fill, Color.white, 0.18f));
            style.active.background = Rounded(Color.Lerp(fill, Color.black, 0.18f));
            var text = new Color(0.07f, 0.05f, 0.12f);
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            return style;
        }

        private static Color DifficultyColor(PrismDifficulty difficulty)
        {
            switch (difficulty)
            {
                case PrismDifficulty.Spectrum: return new Color(0.30f, 0.75f, 0.92f);
                case PrismDifficulty.Prism: return new Color(0.72f, 0.45f, 0.95f);
                default: return new Color(0.98f, 0.72f, 0.22f);
            }
        }

        private static Color ModeColor(PrismMode mode)
            => mode == PrismMode.Rush ? new Color(0.95f, 0.45f, 0.36f) : new Color(0.38f, 0.82f, 0.50f);

        private void OnGUI()
        {
            if (_game == null) return;
            EnsureStyles();

            var safe = DshMobile.MobileUi.SafeArea;
            float scale = DshMobile.MobileUi.UseTouchControls ? DshMobile.MobileUi.UiScale : 1f;
            float width = Mathf.Max(320f, safe.width / scale);
            float height = Mathf.Max(240f, safe.height / scale);

            var previous = GUI.matrix;
            if (scale != 1f || safe.x != 0f || safe.y != 0f)
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, safe.y, 0f), Quaternion.identity,
                    new Vector3(scale, scale, 1f));
            }

            PointerOverPanel = false;

            var back = new Rect(width - 178f, 16f, 158f, 38f);
            if (GUI.Button(back, "回到宠物小屋", _button))
            {
                _game.ReturnToRoom();
            }
            PointerOverPanel |= back.Contains(Event.current.mousePosition);

            switch (_game.State)
            {
                case PrismGame.Phase.Menu: DrawMenu(width, height); break;
                case PrismGame.Phase.Playback: DrawPlaying(width, height, "看它亮起的顺序……"); break;
                case PrismGame.Phase.Input: DrawPlaying(width, height, "现在点出来"); break;
                case PrismGame.Phase.Feedback: DrawPlaying(width, height, ""); break;
                case PrismGame.Phase.Result: DrawResult(width, height); break;
            }

            GUI.matrix = previous;
        }

        private void DrawMenu(float width, float height)
        {
            float cx = width * 0.5f;
            GUI.Label(new Rect(cx - 220f, 30f, 440f, 60f), "棱镜 · 序列记忆", _title);
            GUI.Label(new Rect(cx - 220f, 92f, 440f, 24f), "看、听、复现。点错一个就结束。", Centered());

            float y = 130f;
            GUI.Label(new Rect(cx - 220f, y, 440f, 24f), "难度", Centered());
            y += 30f;

            int unlocked = (int)PrismRules.UnlockedFor(_game.Best);
            DrawDifficultyButton(new Rect(cx - 250f, y, 150f, 46f), "彩虹", PrismDifficulty.Rainbow, unlocked);
            DrawDifficultyButton(new Rect(cx - 80f, y, 150f, 46f), "光谱", PrismDifficulty.Spectrum, unlocked);
            DrawDifficultyButton(new Rect(cx + 90f, y, 150f, 46f), "棱镜", PrismDifficulty.Prism, unlocked);
            y += 58f;

            GUI.Label(new Rect(cx - 220f, y, 440f, 24f), "模式", Centered());
            y += 30f;
            DrawModeButton(new Rect(cx - 180f, y, 160f, 44f), "经典（无时限）", PrismMode.Classic);
            DrawModeButton(new Rect(cx + 20f, y, 160f, 44f), "竞速（限时）", PrismMode.Rush);
            y += 60f;

            if (GUI.Button(new Rect(cx - 130f, y, 260f, 54f), "开始", MakeButton(new Color(0.40f, 0.90f, 0.60f))))
            {
                _game.StartRun(_selectedDifficulty, _selectedMode);
            }

            GUI.Label(new Rect(cx - 220f, y + 70f, 440f, 24f),
                $"最高 {_game.Best}　·　🐾 {DshMobile.PetWallet.Coins:N0}", Centered());
            GUI.Label(new Rect(cx - 220f, y + 96f, 440f, 40f),
                "光谱需最高 8 · 棱镜需最高 15（达到后解锁）", Centered());
        }

        private void DrawDifficultyButton(Rect rect, string label, PrismDifficulty difficulty, int unlocked)
        {
            bool locked = (int)difficulty > unlocked;
            bool selected = _selectedDifficulty == difficulty;
            GUI.enabled = !locked;
            var style = MakeButton(selected
                ? Color.Lerp(DifficultyColor(difficulty), Color.white, 0.25f)
                : DifficultyColor(difficulty));
            if (GUI.Button(rect, locked ? label + "（未解锁）" : label, style))
            {
                _selectedDifficulty = difficulty;
            }
            GUI.enabled = true;
        }

        private void DrawModeButton(Rect rect, string label, PrismMode mode)
        {
            bool selected = _selectedMode == mode;
            var style = MakeButton(selected ? Color.Lerp(ModeColor(mode), Color.white, 0.25f) : ModeColor(mode));
            if (GUI.Button(rect, label, style))
            {
                _selectedMode = mode;
            }
        }

        private GUIStyle Centered()
        {
            _centered.alignment = TextAnchor.MiddleCenter;
            return _centered;
        }

        private void DrawPlaying(float width, float height, string hint)
        {
            float cx = width * 0.5f;
            GUI.Label(new Rect(cx - 200f, 12f, 400f, 54f), _game.CurrentLength.ToString(), _big);

            // A progress line: how many notes of this sequence are already correctly tapped.
            string progress = _game.State == PrismGame.Phase.Input || _game.State == PrismGame.Phase.Feedback
                ? "已对 " + _game.InputStep + " / " + _game.CurrentLength
                : hint;
            GUI.Label(new Rect(cx - 200f, 66f, 400f, 24f), progress, Centered());

            if (_game.Mode == PrismMode.Rush && _game.State == PrismGame.Phase.Input)
            {
                GUI.Label(new Rect(cx - 200f, 90f, 400f, 30f),
                    "剩余 " + Mathf.CeilToInt(_game.RushClock) + " 秒", Centered());
            }
        }

        private void DrawResult(float width, float height)
        {
            float cx = width * 0.5f;
            GUI.Label(new Rect(cx - 220f, 50f, 440f, 54f), "结束", _title);
            GUI.Label(new Rect(cx - 220f, 118f, 440f, 90f), _game.Score.ToString(), _big);
            GUI.Label(new Rect(cx - 220f, 210f, 440f, 24f), "坚持到的序列长度", Centered());
            GUI.Label(new Rect(cx - 220f, 240f, 440f, 24f),
                $"最高 {_game.Best}　·　本局赚了 {_game.RunCoins} 币", Centered());

            float y = 290f;
            if (GUI.Button(new Rect(cx - 150f, y, 300f, 50f), "再来一次", MakeButton(new Color(0.40f, 0.90f, 0.60f))))
            {
                _game.StartRun(_selectedDifficulty, _selectedMode);
            }
            if (GUI.Button(new Rect(cx - 150f, y + 60f, 300f, 50f), "换难度 / 模式", MakeButton(new Color(0.40f, 0.60f, 0.92f))))
            {
                _game.BackToMenu();
            }
        }
    }
}
