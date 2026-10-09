using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// The flappy game's whole interface: a score, a hint, and a result panel.
    ///
    /// IMGUI, like every other screen in this project, and for the same reason — no Canvas, no
    /// font asset, no package. The design-pixel scaling comes from <see cref="DshMobile.MobileUi"/>
    /// so the panel is the same physical size on a phone as it is in the editor.
    /// </summary>
    public class FlyBirdHud : MonoBehaviour
    {
        private FlyBirdGame _game;
        private GUIStyle _title;
        private GUIStyle _big;
        private GUIStyle _small;
        private GUIStyle _button;

        /// <summary>
        /// True while the pointer is over a panel.
        ///
        /// The game reads the mouse directly, so without this a tap on "再来一次" would restart
        /// the run and flap the bird in the same frame — the player would die immediately on
        /// respawn, which reads as the button being broken.
        /// </summary>
        public static bool PointerOverPanel { get; private set; }

        private void Awake() => _game = GetComponent<FlyBirdGame>();

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.98f, 0.9f);

            _big = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _big.normal.textColor = new Color(1f, 0.95f, 0.72f);

            _small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _small.normal.textColor = new Color(0.94f, 0.95f, 0.98f);

            _button = new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(14, 14, 8, 8) };
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

            // ---- score ----
            GUI.Label(new Rect(width * 0.5f - 90f, 18f, 180f, 58f), _game.Score.ToString(), _big);
            GUI.Label(new Rect(width * 0.5f - 150f, 78f, 300f, 24f),
                $"最高 {_game.Best}　·　🐾 {DshMobile.PetWallet.Coins:N0}",
                Centered());

            // One "back to the room" button, drawn once. It used to be drawn twice — once inside
            // the flying branch, which also set PointerOverPanel = true unconditionally, and that
            // is why tapping the screen never made the bird flap: the game's "did the tap land on
            // the UI" test was answered by the HUD's own draw call rather than by the pointer's
            // position. Every panel now owns an explicit rect and the flag is computed from it.
            var back = new Rect(width - 178f, 16f, 158f, 38f);
            if (GUI.Button(back, "回到宠物小屋", _button))
            {
                _game.ReturnToRoom();
            }
            PointerOverPanel |= back.Contains(Event.current.mousePosition);

            if (_game.State == FlyBirdGame.Phase.Ready)
            {
                var intro = new Rect(width * 0.5f - 190f, height * 0.34f, 380f, 116f);
                Panel(intro);
                GUI.Label(new Rect(intro.x + 20f, intro.y + 12f, intro.width - 40f, 30f), "小鸟飞行", _title);
                GUI.Label(new Rect(intro.x + 20f, intro.y + 46f, intro.width - 40f, 24f),
                    DshMobile.MobileUi.UseTouchControls ? "点屏幕扇翅膀，钻过管子" : "空格 / 点击 扇翅膀，钻过管子", _small);
                GUI.Label(new Rect(intro.x + 20f, intro.y + 70f, intro.width - 40f, 24f),
                    "每过一根管子赚一个宠物币，撞到就结束。", _small);
                PointerOverPanel |= intro.Contains(Event.current.mousePosition);
            }

            if (_game.State == FlyBirdGame.Phase.Dead)
            {
                var panel = new Rect(width * 0.5f - 200f, height * 0.28f, 400f, 250f);
                Panel(panel);

                GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 34f), "撞到了", _title);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 52f, panel.width - 40f, 40f),
                    $"飞过 {_game.Score} 根管子", _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 76f, panel.width - 40f, 40f),
                    FlyBirdRules.RankFor(_game.Score) + $"　·　赚了 {_game.RunCoins} 个宠物币", _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 100f, panel.width - 40f, 40f),
                    $"最高 {_game.Best} 根", _small);

                float buttonY = panel.y + 148f;
                if (GUI.Button(new Rect(panel.x + 24f, buttonY, 168f, 44f), "再来一次", _button))
                {
                    _game.ResetRun();
                }

                if (GUI.Button(new Rect(panel.xMax - 192f, buttonY, 168f, 44f), "回到宠物小屋", _button))
                {
                    _game.ReturnToRoom();
                }

                // The result panel swallows taps everywhere on it, not just on its buttons: a tap
                // that lands on the panel and restarts the run *and* flaps is how a player dies
                // instantly on respawn.
                PointerOverPanel |= panel.Contains(Event.current.mousePosition);
            }

            // A quiet hint along the bottom, and the way home before you crash.
            if (_game.State == FlyBirdGame.Phase.Flying)
            {
                var style = Centered();
                style.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
                GUI.Label(new Rect(width * 0.5f - 150f, height - 46f, 300f, 24f),
                    DshMobile.MobileUi.UseTouchControls ? "点一下扇翅膀" : "空格扇翅膀", style);
            }

            // The tap target for the bird itself: the whole screen, minus whatever the HUD is
            // using. A phone's tap does not necessarily arrive as a mouse event, so relying on
            // Input.GetMouseButtonDown alone is how "点击屏幕不能飞" happens — the game now also
            // asks the shared touch layer, which is the thing that actually sees fingers.
            if (_game.State == FlyBirdGame.Phase.Ready || _game.State == FlyBirdGame.Phase.Flying)
            {
                if (!PointerOverPanel)
                {
                    DshMobile.MobileTouch.RegisterButton(TapId,
                        new Rect(safe.x, safe.y, safe.width, safe.height), true, "扇翅膀");
                }
            }

            GUI.matrix = previous;
        }

        /// <summary>Id of the full-screen tap target, so the touch layer can own it.</summary>
        public const string TapId = "fly.tap";

        private GUIStyle Centered()
        {
            var style = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            return style;
        }

        private static void Panel(Rect rect)
        {
            DshMobile.UiSkin.Panel(rect, 16f, new Color(0.08f, 0.09f, 0.13f, 0.94f),
                new Color(1f, 1f, 1f, 0.16f), 1.5f);
        }
    }
}
