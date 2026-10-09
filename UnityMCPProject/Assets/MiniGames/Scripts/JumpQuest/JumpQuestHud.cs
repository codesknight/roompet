using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// The platformer's interface: a coin and life count, a start hint, and touch controls.
    ///
    /// The on-screen controls exist because this is the first game in the collection that needs
    /// more than one input: left, right and jump. They are IMGUI buttons registered through
    /// <see cref="DshMobile.MobileTouch"/> so a thumb can hold left while tapping jump — which a
    /// single-pointer IMGUI layout cannot express on its own.
    /// </summary>
    public class JumpQuestHud : MonoBehaviour
    {
        private JumpQuestGame _game;
        private GUIStyle _title;
        private GUIStyle _small;
        private GUIStyle _button;
        private GUIStyle _pad;

        /// <summary>True while the pointer is over a panel, so taps do not also drive the player.</summary>
        public static bool PointerOverPanel { get; private set; }

        private void Awake() => _game = GetComponent<JumpQuestGame>();

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.98f, 0.9f);

            _small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _small.normal.textColor = new Color(0.96f, 0.97f, 1f);

            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                padding = new RectOffset(14, 14, 8, 8)
            };

            _pad = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Bold };
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

            GUI.Label(new Rect(18f, 14f, 320f, 30f),
                $"🪙 {_game.Coins}　·　❤ {_game.Lives}　·　🐾 {DshMobile.PetWallet.Coins:N0}", _small);

            if (GUI.Button(new Rect(width - 176f, 12f, 158f, 38f), "回到宠物小屋", _button))
            {
                _game.ReturnToRoom();
            }
            PointerOverPanel = true;

            if (_game.State == JumpQuestGame.Phase.Ready)
            {
                var panel = new Rect(width * 0.5f - 210f, height * 0.3f, 420f, 130f);
                DshMobile.UiSkin.Panel(panel, 16f, new Color(0.08f, 0.09f, 0.13f, 0.94f),
                    new Color(1f, 1f, 1f, 0.16f), 1.5f);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 12f, panel.width - 40f, 32f), "跳跃冒险", _title);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 48f, panel.width - 40f, 24f),
                    "捡金币、踩掉紫色小怪，跑到终点的旗子。", _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 74f, panel.width - 40f, 24f),
                    DshMobile.MobileUi.UseTouchControls
                        ? "左下角左右走，右下角跳（可以按住跳得更高）"
                        : "A/D 左右走，空格跳（按住跳得更高）", _small);
                PointerOverPanel = true;
            }

            if (_game.State == JumpQuestGame.Phase.Dead || _game.State == JumpQuestGame.Phase.Finished)
            {
                bool finished = _game.State == JumpQuestGame.Phase.Finished;
                var panel = new Rect(width * 0.5f - 200f, height * 0.3f, 400f, 220f);
                DshMobile.UiSkin.Panel(panel, 16f, new Color(0.08f, 0.09f, 0.13f, 0.94f),
                    new Color(1f, 1f, 1f, 0.16f), 1.5f);

                GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 32f),
                    finished ? "到终点了！" : "摔下来了", _title);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 54f, panel.width - 40f, 24f),
                    $"捡到 {_game.Coins} 个金币　·　赚了 {_game.RunReward} 个宠物币", _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 80f, panel.width - 40f, 24f),
                    JumpQuestRules.RankFor(finished, _game.Coins) + $"　·　最高 {_game.Best} 个", _small);

                if (GUI.Button(new Rect(panel.x + 24f, panel.y + 132f, 168f, 46f), "再来一次", _button))
                {
                    _game.ResetRun();
                }

                if (GUI.Button(new Rect(panel.xMax - 192f, panel.y + 132f, 168f, 46f), "回到宠物小屋", _button))
                {
                    _game.ReturnToRoom();
                }

                PointerOverPanel = true;
            }

            // Touch controls while playing: held buttons, because a platformer needs to keep
            // walking while jumping.
            if (_game.State == JumpQuestGame.Phase.Running ||
                _game.State == JumpQuestGame.Phase.Ready)
            {
                float padSize = 78f;
                float bottom = height - padSize - 24f;

                var left = new Rect(24f, bottom, padSize, padSize);
                var right = new Rect(24f + padSize + 14f, bottom, padSize, padSize);
                var jump = new Rect(width - padSize - 24f, bottom, padSize, padSize);

                _game.MoveInput = 0f;
                _game.JumpHeld = false;

                if (Held(LeftId, left, "◀", _pad)) _game.MoveInput = -1f;
                if (Held(RightId, right, "▶", _pad)) _game.MoveInput = 1f;

                if (Held(JumpId, jump, "跳", _pad))
                {
                    _game.JumpHeld = true;
                    _game.Jump();
                }

                PointerOverPanel = true;
            }

            GUI.matrix = previous;
        }

        /// <summary>Stable ids for the three control pads, so the touch layer can own them.</summary>
        private const string LeftId = "jq.left";
        private const string RightId = "jq.right";
        private const string JumpId = "jq.jump";

        /// <summary>
        /// A button that reports being held rather than clicked.
        ///
        /// <c>GUI.Button</c> fires on release, which is useless for "walk left": by the time it
        /// returns true the thumb is already up. These register their rect with the shared touch
        /// layer instead, which reports the state for every frame the finger is down and also
        /// lets two pads be held at once — the one thing IMGUI cannot do on its own.
        /// </summary>
        private static bool Held(string id, Rect rect, string label, GUIStyle style)
        {
            DshMobile.MobileTouch.RegisterButton(id, DshMobile.MobileWidgets.ToScreen(rect), true, label);
            bool down = DshMobile.MobileTouch.Held(id) || DshMobile.MobileTouch.Pressed(id);

            var skin = new GUIStyle(style);
            skin.normal.textColor = down ? new Color(1f, 0.95f, 0.7f) : Color.white;
            DshMobile.UiSkin.Panel(rect, 14f,
                down ? new Color(0.30f, 0.42f, 0.62f, 0.85f) : new Color(0.16f, 0.18f, 0.24f, 0.78f),
                new Color(1f, 1f, 1f, 0.22f), 1.5f);
            GUI.Label(rect, label, skin);

            return down;
        }
    }
}
