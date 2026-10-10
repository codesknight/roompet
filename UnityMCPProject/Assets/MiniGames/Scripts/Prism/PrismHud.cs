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

            if (GUI.Button(new Rect(cx - 130f, y, 260f, 54f), "开始", _button))
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
            if (GUI.Button(rect, locked ? label + "（未解锁）" : label,
                selected ? SelectedStyle() : _button))
            {
                _selectedDifficulty = difficulty;
            }
            GUI.enabled = true;
        }

        private void DrawModeButton(Rect rect, string label, PrismMode mode)
        {
            bool selected = _selectedMode == mode;
            if (GUI.Button(rect, label, selected ? SelectedStyle() : _button))
            {
                _selectedMode = mode;
            }
        }

        private GUIStyle SelectedStyle()
        {
            var style = new GUIStyle(_button);
            style.normal.textColor = new Color(1f, 0.9f, 0.4f);
            return style;
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
            if (GUI.Button(new Rect(cx - 150f, y, 300f, 50f), "再来一次", _button))
            {
                _game.StartRun(_selectedDifficulty, _selectedMode);
            }
            if (GUI.Button(new Rect(cx - 150f, y + 60f, 300f, 50f), "换难度 / 模式", _button))
            {
                _game.BackToMenu();
            }
        }
    }
}
