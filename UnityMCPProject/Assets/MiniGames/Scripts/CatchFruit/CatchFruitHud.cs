using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// 接果子's interface: a score, hearts, and two big movement pads.
    ///
    /// The pads are held buttons rather than taps — this is the one game in the collection with
    /// continuous input — and they sit on the *same* side of the screen as the falling fruit's
    /// spread, so a thumb can hold left and watch the whole width at once.
    /// </summary>
    public class CatchFruitHud : MonoBehaviour
    {
        private CatchFruitGame _game;
        private GUIStyle _title;
        private GUIStyle _small;
        private GUIStyle _big;

        /// <summary>True while the pointer is over a panel, so taps there do not start a run.</summary>
        public static bool PointerOverPanel { get; private set; }

        public const string LeftId = "catch.left";
        public const string RightId = "catch.right";

        private void Awake() => _game = GetComponent<CatchFruitGame>();

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.98f, 0.9f);

            _big = new GUIStyle(GUI.skin.label)
            {
                fontSize = 50,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _big.normal.textColor = new Color(1f, 0.97f, 0.82f);

            _small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _small.normal.textColor = new Color(0.96f, 0.97f, 1f);
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

            GUI.Label(new Rect(width * 0.5f - 110f, 18f, 220f, 58f), _game.Caught.ToString(), _big);

            // Lives as dots rather than hearts: "♥" is not in Unity's IMGUI font and came out as a
            // small triangle, which reads as a bug next to a score.
            string hearts = "";
            for (int i = 0; i < CatchRules.StartLives; i++)
            {
                hearts += i < _game.Lives ? "● " : "○ ";
            }
            var centred = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(width * 0.5f - 170f, 76f, 340f, 24f),
                hearts + $"最高 {_game.Best}　·　🐾 {DshMobile.PetWallet.Coins:N0}", centred);

            var back = new Rect(width - 178f, 16f, 158f, 38f);
            if (GUI.Button(back, "回到宠物小屋", ButtonStyle()))
            {
                _game.ReturnToRoom();
            }
            PointerOverPanel |= back.Contains(Event.current.mousePosition);

            if (_game.State == CatchFruitGame.Phase.Ready)
            {
                var panel = new Rect(width * 0.5f - 210f, height * 0.3f, 420f, 122f);
                DshMobile.UiSkin.Panel(panel, 16f, new Color(0.08f, 0.10f, 0.12f, 0.94f),
                    new Color(1f, 1f, 1f, 0.16f), 1.5f);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 12f, panel.width - 40f, 32f), "接果子", _title);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 46f, panel.width - 40f, 24f),
                    DshMobile.MobileUi.UseTouchControls
                        ? "按住屏幕左下 / 右下的箭头移动篮子"
                        : "A / D 或 ← → 移动篮子", _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 72f, panel.width - 40f, 24f),
                    "接到果子得分，掉三个就结束。", _small);
                PointerOverPanel |= panel.Contains(Event.current.mousePosition);
            }

            if (_game.State == CatchFruitGame.Phase.Dead)
            {
                var panel = new Rect(width * 0.5f - 200f, height * 0.3f, 400f, 216f);
                DshMobile.UiSkin.Panel(panel, 16f, new Color(0.08f, 0.10f, 0.12f, 0.94f),
                    new Color(1f, 1f, 1f, 0.16f), 1.5f);

                GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 34f), "果子掉光了", _title);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 56f, panel.width - 40f, 24f),
                    $"接到 {_game.Caught} 个　·　漏了 {_game.Missed} 个", _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 82f, panel.width - 40f, 24f),
                    CatchRules.RankFor(_game.Caught) + $"　·　赚了 {_game.RunCoins} 个宠物币", _small);

                var button = ButtonStyle();
                if (GUI.Button(new Rect(panel.x + 24f, panel.y + 136f, 168f, 46f), "再来一次", button))
                {
                    _game.ResetRun();
                }

                if (GUI.Button(new Rect(panel.xMax - 192f, panel.y + 136f, 168f, 46f), "回到宠物小屋", button))
                {
                    _game.ReturnToRoom();
                }

                PointerOverPanel = true;
            }

            // The movement pads: big, in the bottom corners, held rather than tapped.
            if (_game.State != CatchFruitGame.Phase.Dead)
            {
                float pad = Mathf.Min(150f, width * 0.24f);
                float bottom = height - pad - 26f;

                var left = new Rect(20f, bottom, pad, pad);
                var right = new Rect(width - pad - 20f, bottom, pad, pad);

                _game.MoveInput = 0f;
                if (Pad(LeftId, left, "◀")) _game.MoveInput = -1f;
                if (Pad(RightId, right, "▶")) _game.MoveInput = 1f;

                PointerOverPanel = true;
            }

            GUI.matrix = previous;
        }

        private static GUIStyle ButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(14, 14, 8, 8) };
            return style;
        }

        /// <summary>A held direction pad: true for every frame the finger is down.</summary>
        private static bool Pad(string id, Rect rect, string label)
        {
            DshMobile.MobileTouch.RegisterButton(id, DshMobile.MobileWidgets.ToScreen(rect), true, label);
            bool down = DshMobile.MobileTouch.Held(id) || DshMobile.MobileTouch.Pressed(id);

            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var circle = new Rect(rect.center.x - radius, rect.center.y - radius, radius * 2f, radius * 2f);
            DshMobile.UiSkin.Shadow(circle, radius, 4f, 0.4f);
            DshMobile.UiSkin.Panel(circle, radius,
                down ? new Color(0.34f, 0.62f, 0.40f, 0.85f) : new Color(0.12f, 0.16f, 0.14f, 0.55f),
                new Color(1f, 1f, 1f, down ? 0.6f : 0.3f), 3f);

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, down ? 1f : 0.8f);
            GUI.Label(circle, label, PadStyle());
            GUI.color = previous;

            return down;
        }

        private static GUIStyle _padStyle;

        private static GUIStyle PadStyle()
        {
            if (_padStyle == null)
            {
                _padStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 40,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _padStyle.normal.textColor = Color.white;
            }
            return _padStyle;
        }
    }
}
