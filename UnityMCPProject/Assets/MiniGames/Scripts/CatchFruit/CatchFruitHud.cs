using UnityEngine;
using UnityEngine.UI;

namespace DshMiniGames
{
    /// <summary>
    /// 接果子's interface, now in uGUI: a score, lives, and a whole screen you drag.
    ///
    /// The drag itself is NOT moved to uGUI — it is a distance read in <see cref="Update"/> from the
    /// shared touch layer's gesture (plus the raw mouse for a desktop build), and a drag needs the
    /// whole screen rather than a button, so the full-screen-target trick would be wrong here. Only
    /// the drawing changed; the old <c>PointerOverPanel</c> flag is gone because no game code ever
    /// read it (a plain tap on a button has no movement, so it never moves the basket).
    /// </summary>
    public class CatchFruitHud : MonoBehaviour
    {
        private CatchFruitGame _game;
        private RectTransform _root;

        private Text _score;
        private Text _livesBest;
        private Text _hint;

        private GameObject _readyPanel;
        private Text _readyLine1;

        private GameObject _deadPanel;
        private Text _deadLine1;
        private Text _deadLine2;

        private float _lastMouseX = float.NaN;
        private bool _hintUsed;
        private bool _built;

        private static readonly Color Gold = new Color(1f, 0.97f, 0.82f);
        private static readonly Color Cream = new Color(1f, 0.98f, 0.9f);
        private static readonly Color Pale = new Color(0.96f, 0.97f, 1f);
        private static readonly Color BackTint = new Color(0.30f, 0.40f, 0.58f);
        private static readonly Color RetryTint = new Color(0.30f, 0.55f, 0.35f);

        private void Awake()
        {
            _game = GetComponent<CatchFruitGame>();
            Build();
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            _root = DshMobile.Ugui.Root("CatchFruitHud");

            _score = DshMobile.Ugui.Text("Score", _root, "0", 50, Gold, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(340f, 58f));

            _livesBest = DshMobile.Ugui.Text("LivesBest", _root, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_livesBest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -76f), new Vector2(520f, 24f));

            var back = DshMobile.Ugui.Button("Back", _root, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.Place(back.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(168f, 44f));
            back.onClick.AddListener(OnBack);

            BuildReadyPanel();
            BuildDeadPanel();

            _hint = DshMobile.Ugui.Text("Hint", _root, "◀ 左右滑动屏幕移动篮子 ▶", 20,
                new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 74f), new Vector2(720f, 30f));

            _built = true;
        }

        private void BuildReadyPanel()
        {
            var go = new GameObject("ReadyPanel", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), 420f, 122f);

            var bg = DshMobile.Ugui.Panel("Bg", go.transform, 16f,
                new Color(0.08f, 0.10f, 0.12f, 0.94f), new Color(1f, 1f, 1f, 0.16f), 1.5f);
            DshMobile.Ugui.Stretch(bg.rectTransform);

            var title = DshMobile.Ugui.Text("Title", go.transform, "接果子", 26, Cream,
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 20f, 12f, 380f, 32f);

            _readyLine1 = DshMobile.Ugui.Text("Line1", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_readyLine1.rectTransform, 20f, 46f, 380f, 24f);

            var line2 = DshMobile.Ugui.Text("Line2", go.transform, "接到果子得分，掉三个就结束。", 15,
                Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(line2.rectTransform, 20f, 72f, 380f, 24f);

            _readyPanel = go;
        }

        private void BuildDeadPanel()
        {
            var go = new GameObject("DeadPanel", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), 400f, 216f);

            var bg = DshMobile.Ugui.Panel("Bg", go.transform, 16f,
                new Color(0.08f, 0.10f, 0.12f, 0.94f), new Color(1f, 1f, 1f, 0.16f), 1.5f);
            DshMobile.Ugui.Stretch(bg.rectTransform);

            var title = DshMobile.Ugui.Text("Title", go.transform, "果子掉光了", 26, Cream,
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 20f, 14f, 360f, 34f);

            _deadLine1 = DshMobile.Ugui.Text("Line1", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_deadLine1.rectTransform, 20f, 56f, 360f, 24f);

            _deadLine2 = DshMobile.Ugui.Text("Line2", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_deadLine2.rectTransform, 20f, 82f, 360f, 24f);

            var retry = DshMobile.Ugui.Button("Retry", go.transform, "再来一次", 17, RetryTint);
            DshMobile.Ugui.SetRect(retry.GetComponent<RectTransform>(), 24f, 136f, 168f, 46f);
            retry.onClick.AddListener(OnRetry);

            var home = DshMobile.Ugui.Button("Home", go.transform, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.SetRect(home.GetComponent<RectTransform>(), 208f, 136f, 168f, 46f);
            home.onClick.AddListener(OnBack);

            _deadPanel = go;
        }

        private void Update()
        {
            if (_game == null || !_built) return;
            ReadDrag();
            SyncDisplay();
        }

        /// <summary>
        /// Converts this frame's drag into basket movement — the same logic the IMGUI HUD ran, kept
        /// in Update because a delta applied in a repaint-heavy pass would double the basket speed.
        /// </summary>
        private void ReadDrag()
        {
            if (_game.State == CatchFruitGame.Phase.Dead) return;

            float dx = 0f;

            var gesture = DshMobile.MobileTouch.Gesture;
            if (gesture.IsActive)
            {
                dx = gesture.FrameDelta.x;
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

            float screenWidth = Mathf.Max(1f, Screen.width);
            _game.DragBy(CatchRules.PixelsToWorld(dx, screenWidth, CatchSettings.Default));
        }

        private void SyncDisplay()
        {
            _score.text = _game.Caught.ToString();

            string hearts = "";
            for (int i = 0; i < CatchRules.StartLives; i++) hearts += i < _game.Lives ? "● " : "○ ";
            _livesBest.text = hearts + $"最高 {_game.Best}　·　宠物币 {DshMobile.PetWallet.Coins:N0}";

            bool ready = _game.State == CatchFruitGame.Phase.Ready;
            bool dead = _game.State == CatchFruitGame.Phase.Dead;
            bool playing = _game.State == CatchFruitGame.Phase.Playing;

            _readyPanel.SetActive(ready);
            _deadPanel.SetActive(dead);
            _hint.gameObject.SetActive(playing && !_hintUsed);

            if (ready)
            {
                _readyLine1.text = DshMobile.MobileUi.UseTouchControls
                    ? "在屏幕上左右滑动，篮子跟着手指走"
                    : "拖动鼠标，或按 A / D 与 ← →";
            }

            if (dead)
            {
                _deadLine1.text = $"接到 {_game.Caught} 个　·　漏了 {_game.Missed} 个";
                _deadLine2.text = CatchRules.RankFor(_game.Caught) + $"　·　赚了 {_game.RunCoins} 个宠物币";
            }
        }

        private void OnBack() => _game?.ReturnToRoom();
        private void OnRetry() => _game?.ResetRun();
    }
}
