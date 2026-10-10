using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// 窒息隧道's interface: a menu (mode + theme + start), the single virtual stick, a score, and
    /// a result panel. The stick can live on the left or the right of the screen.
    /// </summary>
    public class TunnelHud : MonoBehaviour
    {
        private TunnelGame _game;
        private GUIStyle _title;
        private GUIStyle _big;
        private GUIStyle _small;
        private GUIStyle _button;
        private GUIStyle _centered;

        private TunnelMode _selectedMode = TunnelMode.Survival;
        private TunnelTheme _selectedTheme = TunnelTheme.Neon;

        /// <summary>The single stick input, -1..1 in each axis. Read by the game.</summary>
        public static Vector2 Stick { get; private set; }

        private static bool _dragging;
        private static bool _stickVisible;
        private static Vector2 _stickCentre;
        private const float StickRadius = 78f;

        private static Texture2D _circle;

        private void Awake() => _game = GetComponent<TunnelGame>();

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.8f, 0.95f, 1f);
            _big = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big.normal.textColor = new Color(1f, 0.92f, 0.62f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _small.normal.textColor = new Color(0.92f, 0.92f, 0.98f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(14, 14, 8, 8) };
            _centered = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            _centered.normal.textColor = new Color(0.92f, 0.92f, 0.98f);
        }

        private static Texture2D Circle()
        {
            if (_circle != null) return _circle;
            _circle = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            for (int y = 0; y < 96; y++)
            {
                for (int x = 0; x < 96; x++)
                {
                    float dx = (x - 47.5f) / 47.5f;
                    float dy = (y - 47.5f) / 47.5f;
                    float d = dx * dx + dy * dy;
                    _circle.SetPixel(x, y, d <= 1f ? new Color(1f, 1f, 1f, 0.35f) : new Color(0, 0, 0, 0));
                }
            }
            _circle.Apply();
            return _circle;
        }

        private void Update()
        {
            if (_game == null || _game.State != TunnelGame.Phase.Running)
            {
                _dragging = false;
                _stickVisible = false;
                Stick = Vector2.zero;
                return;
            }

            // Floating stick: wherever the finger lands (below the top bar), the stick appears
            // there and dragging moves the ship. Works on both halves of the screen.
            Vector2 mouse = Input.mousePosition;

            if (Input.GetMouseButtonDown(0) && mouse.y < Screen.height - 90f)
            {
                _dragging = true;
                _stickVisible = true;
                _stickCentre = mouse;
                Stick = Vector2.zero;
            }

            if (_dragging && Input.GetMouseButton(0))
            {
                Vector2 now = Input.mousePosition;
                Vector2 delta = now - _stickCentre;
                // The knob tracks the finger exactly within the rim (base stays put). This is the
                // "stick under the finger" feel: no re-centring, no lag, one-to-one.
                Stick = Vector2.ClampMagnitude(delta / StickRadius, 1f);
                if (Stick.magnitude < 0.08f) Stick = Vector2.zero;
            }

            if (!Input.GetMouseButton(0))
            {
                _dragging = false;
                _stickVisible = false;
                Stick = Vector2.zero;
            }
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

            var back = new Rect(width - 178f, 16f, 158f, 38f);
            if (GUI.Button(back, "回到宠物小屋", _button)) _game.ReturnToRoom();

            switch (_game.State)
            {
                case TunnelGame.Phase.Menu: DrawMenu(width, height); break;
                case TunnelGame.Phase.Running: DrawRunning(width, height); break;
                case TunnelGame.Phase.Dead: DrawResult(width, height); break;
            }

            GUI.matrix = previous;
        }

        private void DrawMenu(float width, float height)
        {
            float cx = width * 0.5f;
            GUI.Label(new Rect(cx - 220f, 24f, 440f, 56f), "窒息隧道", _title);
            GUI.Label(new Rect(cx - 220f, 80f, 440f, 24f), "一根摇杆上下左右，穿过门洞、躲开障碍。", Centered());

            float y = 116f;
            GUI.Label(new Rect(cx - 220f, y, 440f, 24f), "玩法", Centered());
            y += 30f;
            DrawModeButton(new Rect(cx - 180f, y, 160f, 44f), "生存 · 穿门洞", TunnelMode.Survival);
            DrawModeButton(new Rect(cx + 20f, y, 160f, 44f), "矿洞 · 躲障碍", TunnelMode.Mine);
            y += 58f;

            GUI.Label(new Rect(cx - 220f, y, 440f, 24f), "隧道主题", Centered());
            y += 30f;
            DrawThemeButton(new Rect(cx - 260f, y, 120f, 40f), "霓虹", TunnelTheme.Neon);
            DrawThemeButton(new Rect(cx - 130f, y, 120f, 40f), "冰窟", TunnelTheme.Ice);
            DrawThemeButton(new Rect(cx + 0f, y, 120f, 40f), "熔岩", TunnelTheme.Lava);
            DrawThemeButton(new Rect(cx + 130f, y, 120f, 40f), "绿林", TunnelTheme.Forest);
            y += 56f;

            // Sensitivity: how fast the ship answers the stick.
            GUI.Label(new Rect(cx - 220f, y, 440f, 24f), "灵敏度　" + Mathf.RoundToInt(_game.Sensitivity * 100f) + "%", Centered());
            y += 28f;
            float sens = GUI.HorizontalSlider(new Rect(cx - 180f, y, 360f, 22f), _game.Sensitivity, 0.5f, 2f);
            if (Mathf.Abs(sens - _game.Sensitivity) > 0.001f) _game.Sensitivity = sens;
            y += 36f;

            if (GUI.Button(new Rect(cx - 130f, y, 260f, 54f), "开始", _button))
            {
                _game.StartRun(_selectedMode, _selectedTheme);
            }

            GUI.Label(new Rect(cx - 220f, y + 70f, 440f, 24f),
                $"最高 {_game.Best}　·　🐾 {DshMobile.PetWallet.Coins:N0}", Centered());
        }

        private void DrawModeButton(Rect rect, string label, TunnelMode mode)
        {
            if (GUI.Button(rect, label, _selectedMode == mode ? SelectedStyle() : _button)) _selectedMode = mode;
        }

        private void DrawThemeButton(Rect rect, string label, TunnelTheme theme)
        {
            if (GUI.Button(rect, label, _selectedTheme == theme ? SelectedStyle() : _button)) _selectedTheme = theme;
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

        private void DrawRunning(float width, float height)
        {
            GUI.Label(new Rect(width * 0.5f - 200f, 10f, 400f, 52f), _game.Score.ToString(), _big);
            GUI.Label(new Rect(width * 0.5f - 200f, 62f, 400f, 24f), "按住屏幕任意处拖动，控制飞船上下左右", Centered());

            DrawStick();
        }

        private void DrawResult(float width, float height)
        {
            float cx = width * 0.5f;
            GUI.Label(new Rect(cx - 220f, 40f, 440f, 54f), "撞上了", _title);
            GUI.Label(new Rect(cx - 220f, 104f, 440f, 90f), _game.Score.ToString(), _big);
            GUI.Label(new Rect(cx - 220f, 196f, 440f, 24f),
                $"最高 {_game.Best}　·　本局赚了 {_game.RunCoins} 币", Centered());

            if (GUI.Button(new Rect(cx - 150f, 240f, 300f, 50f), "再来一次", _button))
            {
                _game.StartRun(_selectedMode, _selectedTheme);
            }
            if (GUI.Button(new Rect(cx - 150f, 300f, 300f, 50f), "换玩法 / 主题", _button))
            {
                _game.BackToMenu();
            }
        }

        private void DrawStick()
        {
            if (!_stickVisible) return;

            // The stick centre is in screen (bottom-left) coords; GUI draws in top-left coords,
            // so flip Y. Drawn with an identity matrix so it sits under the thumb, not the panel.
            GUI.matrix = Matrix4x4.identity;
            Vector2 centre = new Vector2(_stickCentre.x, Screen.height - _stickCentre.y);

            var baseRect = new Rect(centre.x - StickRadius, centre.y - StickRadius,
                StickRadius * 2f, StickRadius * 2f);
            GUI.color = new Color(1f, 1f, 1f, 0.28f);
            GUI.DrawTexture(baseRect, Circle());
            GUI.color = Color.white;

            // Knob sits exactly under the finger (clamped to the rim), at full radius.
            const float knobHalf = 28f;
            var knob = new Rect(centre.x + Stick.x * StickRadius - knobHalf,
                centre.y - Stick.y * StickRadius - knobHalf, knobHalf * 2f, knobHalf * 2f);
            GUI.color = new Color(1f, 0.9f, 0.4f, 0.9f);
            GUI.DrawTexture(knob, Circle());
            GUI.color = Color.white;
        }
    }
}
