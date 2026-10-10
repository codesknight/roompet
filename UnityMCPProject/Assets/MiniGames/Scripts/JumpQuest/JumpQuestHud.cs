using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DshMiniGames
{
    /// <summary>
    /// The hop game's interface, now in uGUI: a score, a charge bar, and a result panel.
    ///
    /// Migrated from IMGUI in round 41. The input is a full-screen *hold*, so the migration is the
    /// FlyBird pattern again: a full-screen transparent target drawn first, panels and buttons above
    /// it. But a hold is not a tap, so the target is a plain Image + pointer handlers rather than a
    /// Button's onClick — <see cref="HoldArea"/> pushes the game's <c>HoldInput</c> on pointer-down
    /// and <c>ReleaseInput</c> on pointer-up, and the game's own mouse path (and its
    /// <c>PointerOverPanel</c> dependency) is deleted. Keyboard charge stays in the game.
    /// </summary>
    public class JumpQuestHud : MonoBehaviour
    {
        private JumpQuestGame _game;
        private RectTransform _root;

        private Text _score;
        private Text _best;
        private Text _popup;

        private GameObject _chargeBar;
        private RectTransform _chargeFill;
        private Text _chargeHint;
        private Text _readyHint;

        private GameObject _deadPanel;
        private Text _deadLine1;
        private Text _deadLine2;

        private bool _built;

        private static readonly Color Gold = new Color(1f, 0.96f, 0.8f);
        private static readonly Color Cream = new Color(1f, 0.98f, 0.9f);
        private static readonly Color Pale = new Color(0.94f, 0.95f, 0.98f);
        private static readonly Color BackTint = new Color(0.30f, 0.40f, 0.58f);
        private static readonly Color RetryTint = new Color(0.30f, 0.55f, 0.35f);

        private void Awake()
        {
            _game = GetComponent<JumpQuestGame>();
            Build();
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            _root = DshMobile.Ugui.Root("JumpQuestHud");

            // Full-screen hold target, drawn first so everything above it blocks it.
            var holdGo = new GameObject("HoldTarget", typeof(RectTransform));
            holdGo.transform.SetParent(_root, false);
            var holdImg = holdGo.AddComponent<Image>();
            holdImg.color = new Color(0f, 0f, 0f, 0f);
            DshMobile.Ugui.Stretch(holdImg.rectTransform);
            var hold = holdGo.AddComponent<HoldArea>();
            hold.Pressed = () => { if (_game != null) _game.HoldInput = true; };
            hold.Released = () => { if (_game != null) { _game.HoldInput = false; _game.ReleaseInput = true; } };

            _score = DshMobile.Ugui.Text("Score", _root, "0", 54, Gold, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(360f, 62f));

            _best = DshMobile.Ugui.Text("Best", _root, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_best.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -84f), new Vector2(440f, 24f));

            _popup = DshMobile.Ugui.Text("Popup", _root, "", 26, new Color(1f, 0.85f, 0.4f),
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(_popup.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f), new Vector2(360f, 40f));

            var back = DshMobile.Ugui.Button("Back", _root, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.Place(back.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(168f, 44f));
            back.onClick.AddListener(OnBack);

            BuildChargeBar();
            BuildDeadPanel();

            // Ready-state hint, along the bottom.
            _readyHint = DshMobile.Ugui.Text("ReadyHint", _root, "", 16, new Color(1f, 1f, 1f, 0.75f),
                TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_readyHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 96f), new Vector2(560f, 26f));

            _built = true;
        }

        private void BuildChargeBar()
        {
            var go = new GameObject("ChargeBar", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Place(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(420f, 18f));

            var bg = DshMobile.Ugui.Panel("Bg", go.transform, 9f,
                new Color(0f, 0f, 0f, 0.45f), new Color(1f, 1f, 1f, 0.18f), 1.5f);
            DshMobile.Ugui.Stretch(bg.rectTransform);

            var fill = DshMobile.Ugui.Image("Fill", go.transform, new Color(0.55f, 0.9f, 0.6f));
            var fillRt = fill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0.5f);
            fillRt.anchorMax = new Vector2(0f, 0.5f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = new Vector2(3f, 0f);
            fillRt.sizeDelta = new Vector2(0f, 12f);
            _chargeFill = fillRt;

            _chargeHint = DshMobile.Ugui.Text("ChargeHint", _root, "松开就跳", 16,
                new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_chargeHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 76f), new Vector2(400f, 24f));

            _chargeBar = go;
        }

        private void BuildDeadPanel()
        {
            var go = new GameObject("DeadPanel", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), 400f, 216f);

            var bg = DshMobile.Ugui.Panel("Bg", go.transform, 16f,
                new Color(0.08f, 0.09f, 0.13f, 0.94f), new Color(1f, 1f, 1f, 0.16f), 1.5f);
            DshMobile.Ugui.Stretch(bg.rectTransform);

            var title = DshMobile.Ugui.Text("Title", go.transform, "没跳上去", 26, Cream,
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

            _score.text = _game.Score.ToString();
            _best.text = $"最高 {_game.Best}　·　宠物币 {DshMobile.PetWallet.Coins:N0}";

            bool hasPopup = !string.IsNullOrEmpty(_game.LastPopup);
            _popup.gameObject.SetActive(hasPopup);
            if (hasPopup) _popup.text = _game.LastPopup;

            bool charging = _game.State == JumpQuestGame.Phase.Charging;
            _chargeBar.SetActive(charging);
            _chargeHint.gameObject.SetActive(charging);
            if (charging)
            {
                _chargeFill.sizeDelta = new Vector2((420f - 6f) * _game.Charge, 12f);
                var fill = _chargeFill.GetComponent<Image>();
                fill.color = Color.Lerp(new Color(0.55f, 0.9f, 0.6f), new Color(1f, 0.55f, 0.4f), _game.Charge);
            }

            bool ready = _game.State == JumpQuestGame.Phase.Ready;
            _readyHint.gameObject.SetActive(ready);
            if (ready)
            {
                _readyHint.text = DshMobile.MobileUi.UseTouchControls
                    ? "按住屏幕蓄力，松手跳出去（跳到正中间会 +2）"
                    : "按住空格蓄力，松手跳出去（跳到正中间会 +2）";
            }

            _deadPanel.SetActive(_game.State == JumpQuestGame.Phase.Dead);
            if (_game.State == JumpQuestGame.Phase.Dead)
            {
                _deadLine1.text = $"得分 {_game.Score}　·　赚了 {_game.RunCoins} 个宠物币";
                _deadLine2.text = HopRules.RankFor(_game.Score) + $"　·　最高 {_game.Best}";
            }
        }

        private void OnBack() => _game?.ReturnToRoom();
        private void OnRetry() => _game?.ResetRun();
    }

    /// <summary>
    /// Turns a full-screen (or otherwise) transparent target into hold input: pointer-down pushes
    /// <see cref="JumpQuestGame.HoldInput"/>, pointer-up pushes <see cref="JumpQuestGame.ReleaseInput"/>.
    /// </summary>
    public class HoldArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action Pressed;
        public System.Action Released;

        public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke();
        public void OnPointerUp(PointerEventData eventData) => Released?.Invoke();
    }
}
