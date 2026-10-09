using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// The hop game's interface: the score, a charge bar, and a result panel.
    ///
    /// The input is the whole screen, held — which is why this HUD is mostly about *not* eating the
    /// touch. It registers one big hold target with the shared touch layer, and it stops doing that
    /// while the pointer is over a button or a panel: a screen where "hold to charge" also presses
    /// 回到宠物小屋 is a screen that cannot be played.
    /// </summary>
    public class JumpQuestHud : MonoBehaviour
    {
        private JumpQuestGame _game;
        private GUIStyle _title;
        private GUIStyle _small;
        private GUIStyle _big;
        private GUIStyle _button;

        /// <summary>True while the pointer is over a panel, so a hold there is not a charge.</summary>
        public static bool PointerOverPanel { get; private set; }

        /// <summary>Id of the full-screen hold target.</summary>
        public const string HoldId = "hop.hold";

        private void Awake() => _game = GetComponent<JumpQuestGame>();

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.98f, 0.9f);

            _big = new GUIStyle(GUI.skin.label)
            {
                fontSize = 54,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _big.normal.textColor = new Color(1f, 0.96f, 0.8f);

            _small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _small.normal.textColor = new Color(0.94f, 0.95f, 0.98f);

            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                padding = new RectOffset(14, 14, 8, 8)
            };
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
            GUI.Label(new Rect(width * 0.5f - 120f, 22f, 240f, 62f), _game.Score.ToString(), _big);
            var centred = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(width * 0.5f - 160f, 84f, 320f, 24f),
                $"最高 {_game.Best}　·　🐾 {DshMobile.PetWallet.Coins:N0}", centred);

            if (!string.IsNullOrEmpty(_game.LastPopup))
            {
                var popup = new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter };
                popup.normal.textColor = new Color(1f, 0.85f, 0.4f);
                GUI.Label(new Rect(width * 0.5f - 120f, height * 0.3f, 240f, 34f), _game.LastPopup, popup);
            }

            // ---- the way out ----
            var back = new Rect(width - 178f, 16f, 158f, 38f);
            if (GUI.Button(back, "回到宠物小屋", _button))
            {
                _game.ReturnToRoom();
            }
            PointerOverPanel |= back.Contains(Event.current.mousePosition);

            // ---- charge bar ----
            if (_game.State == JumpQuestGame.Phase.Charging)
            {
                float barWidth = Mathf.Min(420f, width - 80f);
                var bar = new Rect((width - barWidth) * 0.5f, height - 104f, barWidth, 18f);
                DshMobile.UiSkin.Panel(bar, 9f, new Color(0f, 0f, 0f, 0.45f),
                    new Color(1f, 1f, 1f, 0.18f), 1.5f);

                var fill = new Rect(bar.x + 3f, bar.y + 3f,
                    (bar.width - 6f) * _game.Charge, bar.height - 6f);
                GUI.color = Color.Lerp(new Color(0.55f, 0.9f, 0.6f), new Color(1f, 0.55f, 0.4f),
                    _game.Charge);
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
                GUI.color = Color.white;

                var hint = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(width * 0.5f - 200f, bar.yMax + 6f, 400f, 24f), "松开就跳", hint);
            }
            else if (_game.State == JumpQuestGame.Phase.Ready)
            {
                var hint = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                hint.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
                GUI.Label(new Rect(width * 0.5f - 220f, height - 96f, 440f, 26f),
                    DshMobile.MobileUi.UseTouchControls
                        ? "按住屏幕蓄力，松手跳出去（跳到正中间会 +2）"
                        : "按住空格蓄力，松手跳出去（跳到正中间会 +2）", hint);
            }

            if (_game.State == JumpQuestGame.Phase.Dead)
            {
                var panel = new Rect(width * 0.5f - 200f, height * 0.3f, 400f, 216f);
                DshMobile.UiSkin.Panel(panel, 16f, new Color(0.08f, 0.09f, 0.13f, 0.94f),
                    new Color(1f, 1f, 1f, 0.16f), 1.5f);

                GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 34f), "没跳上去", _title);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 56f, panel.width - 40f, 24f),
                    $"得分 {_game.Score}　·　赚了 {_game.RunCoins} 个宠物币", _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 82f, panel.width - 40f, 24f),
                    HopRules.RankFor(_game.Score) + $"　·　最高 {_game.Best}", _small);

                if (GUI.Button(new Rect(panel.x + 24f, panel.y + 136f, 168f, 46f), "再来一次", _button))
                {
                    _game.ResetRun();
                }

                if (GUI.Button(new Rect(panel.xMax - 192f, panel.y + 136f, 168f, 46f), "回到宠物小屋", _button))
                {
                    _game.ReturnToRoom();
                }

                PointerOverPanel = true;
            }

            // ---- the hold target ----
            // The whole screen, minus the UI. The shared touch layer is what actually sees fingers,
            // so charging behaves the same on a phone as it does with a mouse.
            if (!PointerOverPanel &&
                (_game.State == JumpQuestGame.Phase.Ready || _game.State == JumpQuestGame.Phase.Charging))
            {
                DshMobile.MobileTouch.RegisterButton(HoldId,
                    new Rect(safe.x, safe.y, safe.width, safe.height), true, "蓄力");

                _game.HoldInput = DshMobile.MobileTouch.Held(HoldId);
                _game.ReleaseInput = DshMobile.MobileTouch.Released(HoldId);
            }
            else
            {
                _game.HoldInput = false;
                _game.ReleaseInput = false;
            }

            GUI.matrix = previous;
        }
    }
}
