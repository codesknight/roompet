using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// 接果子's interface: a score, lives, and a whole screen you drag.
    ///
    /// The control is a horizontal drag, and the drag is read in <see cref="Update"/> rather than
    /// in <see cref="OnGUI"/> for a reason that is easy to get wrong: IMGUI runs its layout and its
    /// repaint pass in the same frame, so a *delta* consumed in OnGUI is applied twice and the
    /// basket travels at double speed on a real repaint and a single speed in a still editor view.
    /// Update runs exactly once per frame, which is what a delta needs.
    ///
    /// The two hold-to-move pads are gone. They were a digital control on an analog play area: you
    /// overshoot and correct instead of arriving, and the report from the phone was that the right
    /// pad in particular did not respond. A drag that maps the finger's own travel onto the world
    /// cannot be off by a pixel, and it works anywhere on the screen.
    /// </summary>
    public class CatchFruitHud : MonoBehaviour
    {
        private CatchFruitGame _game;
        private GUIStyle _title;
        private GUIStyle _small;
        private GUIStyle _big;

        /// <summary>True while the pointer is over a panel, so taps there do not start a run.</summary>
        public static bool PointerOverPanel { get; private set; }

        private float _lastMouseX = float.NaN;

        private void Awake() => _game = GetComponent<CatchFruitGame>();

        /// <summary>
        /// Converts this frame's drag into basket movement.
        ///
        /// Two sources, because the game has to be playable in the editor as well as on a phone:
        /// the touch layer's gesture (a finger, or the mouse when the phone layout is forced on),
        /// and the raw mouse for a desktop build that is not using touch controls at all.
        /// </summary>
        private void Update()
        {
            if (_game == null) return;
            if (_game.State == CatchFruitGame.Phase.Dead) return;

            float dx = 0f;

            var gesture = DshMobile.MobileTouch.Gesture;
            if (gesture.IsActive)
            {
                dx = gesture.FrameDelta.x;

                // Taken, not merely read: this is the one input in the game that is a distance
                // rather than a flag, and a delta left lying around gets applied again next frame.
                gesture.ConsumeFrameDelta();
            }
            else if (Input.GetMouseButton(0))
            {
                float x = Input.mousePosition.x;
                if (!Input.GetMouseButtonDown(0) && !float.IsNaN(_lastMouseX)) dx = x - _lastMouseX;
                _lastMouseX = x;
            }
            else
            {
                _lastMouseX = float.NaN;
            }

            if (Mathf.Abs(dx) < 0.01f) return;

            _hintUsed = true;

            // The world is the width of the play area; the screen pixel is the unit the finger
            // actually moved in. Scaling by the design width keeps a drag the same physical
            // distance on a phone and in a tall editor window.
            float screenWidth = Mathf.Max(1f, Screen.width);
            _game.DragBy(CatchRules.PixelsToWorld(dx, screenWidth, CatchSettings.Default));
        }

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
                        ? "在屏幕上左右滑动，篮子跟着手指走"
                        : "拖动鼠标，或按 A / D 与 ← →", _small);
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

            // The control hint, on the playfield itself. It replaces the two movement pads: with the
            // whole screen draggable there is no control to look at, so the thing to draw is the
            // instruction, and it goes away once the player has dragged.
            if (_game.State == CatchFruitGame.Phase.Playing && !_hintUsed)
            {
                var hint = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
                hint.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
                GUI.Label(new Rect(0f, height - 74f, width, 30f), "◀ 左右滑动屏幕移动篮子 ▶", hint);
            }

            GUI.matrix = previous;
        }

        private bool _hintUsed;

        private static GUIStyle ButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(14, 14, 8, 8) };
            return style;
        }
    }
}
