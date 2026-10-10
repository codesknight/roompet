using UnityEngine;
using UnityEngine.UI;

namespace DshMiniGames
{
    /// <summary>
    /// The flappy game's whole interface — a score, a hint, and a result panel — now in uGUI.
    ///
    /// Migrated from IMGUI in round 41 as the first HUD. The interesting change is the input:
    /// the old IMGUI version computed a <c>PointerOverPanel</c> flag from the mouse and registered
    /// a full-screen tap with the shared touch layer, then let the game read both. Here the
    /// full-screen tap is a transparent uGUI Button drawn <b>first</b> (bottom of the hierarchy),
    /// so every panel and button above it naturally swallows the taps that used to be gated by
    /// that flag — no per-frame rect bookkeeping, no touch-layer registration. The game's own
    /// mouse/touch flap paths were removed; keyboard flap stays in the game.
    ///
    /// One deliberate behaviour change: a tap outside the result panel now restarts after the
    /// game's 0.6s guard on the phone too (the old IMGUI only did that with a desktop mouse).
    /// </summary>
    public class FlyBirdHud : MonoBehaviour
    {
        private FlyBirdGame _game;
        private RectTransform _root;

        private Text _score;
        private Text _best;
        private Text _hint;

        private GameObject _introPanel;
        private Text _introLine1;

        private GameObject _deadPanel;
        private Text _deadTitle;
        private Text _deadLine1;
        private Text _deadLine2;
        private Text _deadLine3;

        private Button _flap;

        private bool _built;

        private static readonly Color Gold = new Color(1f, 0.95f, 0.72f);
        private static readonly Color Cream = new Color(1f, 0.98f, 0.9f);
        private static readonly Color Pale = new Color(0.94f, 0.95f, 0.98f);
        private static readonly Color BackTint = new Color(0.30f, 0.40f, 0.58f);
        private static readonly Color RetryTint = new Color(0.30f, 0.55f, 0.35f);

        private void Awake()
        {
            _game = GetComponent<FlyBirdGame>();
            Build();
        }

        private void OnDestroy()
        {
            // The shared canvas survives scene loads; this HUD's container must not.
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            _root = DshMobile.Ugui.Root("FlyBirdHud");

            // Full-screen tap target, drawn first so everything above it blocks it.
            var flapGo = new GameObject("FlapTarget", typeof(RectTransform));
            flapGo.transform.SetParent(_root, false);
            var flapImg = flapGo.AddComponent<Image>();
            flapImg.color = new Color(0f, 0f, 0f, 0f); // invisible, still hit-tested
            DshMobile.Ugui.Stretch(flapImg.rectTransform);
            _flap = flapGo.AddComponent<Button>();
            _flap.transition = Selectable.Transition.None;
            _flap.onClick.AddListener(OnFlap);

            // Score, top centre.
            _score = DshMobile.Ugui.Text("Score", _root, "0", 48, Gold,
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), new Vector2(400f, 58f));

            // Best + coins, just under the score.
            _best = DshMobile.Ugui.Text("Best", _root, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_best.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -76f), new Vector2(520f, 24f));

            // Always-visible way home, top right.
            var back = DshMobile.Ugui.Button("Back", _root, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.Place(back.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(168f, 44f));
            back.onClick.AddListener(OnBack);

            BuildIntroPanel();
            BuildDeadPanel();

            // Quiet hint along the bottom while flying.
            _hint = DshMobile.Ugui.Text("Hint", _root, "", 15, new Color(1f, 1f, 1f, 0.55f),
                TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(400f, 24f));

            _built = true;
        }

        private void BuildIntroPanel()
        {
            var go = new GameObject("IntroPanel", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), 380f, 116f);

            var panel = DshMobile.Ugui.Panel("Bg", go.transform, 16f,
                new Color(0.08f, 0.09f, 0.13f, 0.94f), new Color(1f, 1f, 1f, 0.16f), 1.5f);
            DshMobile.Ugui.Stretch(panel.rectTransform);
            panel.raycastTarget = true; // swallows taps so the flap target does not start the run here

            var title = DshMobile.Ugui.Text("Title", go.transform, "小鸟飞行", 26, Cream,
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 20f, 12f, 340f, 30f);

            _introLine1 = DshMobile.Ugui.Text("Line1", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_introLine1.rectTransform, 20f, 46f, 340f, 24f);

            var line2 = DshMobile.Ugui.Text("Line2", go.transform,
                "每过一根管子赚一个宠物币，撞到就结束。", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(line2.rectTransform, 20f, 70f, 340f, 24f);

            _introPanel = go;
        }

        private void BuildDeadPanel()
        {
            var go = new GameObject("DeadPanel", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), 400f, 250f);

            var panel = DshMobile.Ugui.Panel("Bg", go.transform, 16f,
                new Color(0.08f, 0.09f, 0.13f, 0.94f), new Color(1f, 1f, 1f, 0.16f), 1.5f);
            DshMobile.Ugui.Stretch(panel.rectTransform);
            panel.raycastTarget = true; // swallows taps so a tap does not restart through the flap target

            _deadTitle = DshMobile.Ugui.Text("Title", go.transform, "撞到了", 26, Cream,
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(_deadTitle.rectTransform, 20f, 14f, 360f, 34f);

            _deadLine1 = DshMobile.Ugui.Text("Line1", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_deadLine1.rectTransform, 20f, 52f, 360f, 24f);

            _deadLine2 = DshMobile.Ugui.Text("Line2", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_deadLine2.rectTransform, 20f, 76f, 360f, 24f);

            _deadLine3 = DshMobile.Ugui.Text("Line3", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_deadLine3.rectTransform, 20f, 100f, 360f, 24f);

            var retry = DshMobile.Ugui.Button("Retry", go.transform, "再来一次", 17, RetryTint);
            DshMobile.Ugui.SetRect(retry.GetComponent<RectTransform>(), 24f, 148f, 168f, 44f);
            retry.onClick.AddListener(OnRetry);

            var back = DshMobile.Ugui.Button("Back2", go.transform, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.SetRect(back.GetComponent<RectTransform>(), 208f, 148f, 168f, 44f);
            back.onClick.AddListener(OnBack);

            _deadPanel = go;
        }

        private void Update()
        {
            if (_game == null || !_built) return;

            _score.text = _game.Score.ToString();
            _best.text = $"最高 {_game.Best}　·　宠物币 {DshMobile.PetWallet.Coins:N0}";

            bool ready = _game.State == FlyBirdGame.Phase.Ready;
            bool flying = _game.State == FlyBirdGame.Phase.Flying;
            bool dead = _game.State == FlyBirdGame.Phase.Dead;

            _introPanel.SetActive(ready);
            _deadPanel.SetActive(dead);

            if (ready)
            {
                _introLine1.text = DshMobile.MobileUi.UseTouchControls
                    ? "点屏幕扇翅膀，钻过管子"
                    : "空格 / 点击 扇翅膀，钻过管子";
            }

            _hint.gameObject.SetActive(flying);
            if (flying)
            {
                _hint.text = DshMobile.MobileUi.UseTouchControls ? "点一下扇翅膀" : "空格扇翅膀";
            }

            if (dead)
            {
                _deadLine1.text = $"飞过 {_game.Score} 根管子";
                _deadLine2.text = FlyBirdRules.RankFor(_game.Score) + $"　·　赚了 {_game.RunCoins} 个宠物币";
                _deadLine3.text = $"最高 {_game.Best} 根";
            }
        }

        private void OnFlap() => _game?.Flap();
        private void OnBack() => _game?.ReturnToRoom();
        private void OnRetry() => _game?.ResetRun();
    }
}
