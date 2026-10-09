using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// 愤怒的小鸟's interface: pigs left, birds left, the score — and the two things a slingshot game
    /// needs that are not obvious: the aim guide (the drag is invisible without it) and a hint.
    ///
    /// The hint is not a canned tip: it is the verified shot the generator used to prove the level is
    /// clearable, so pressing it shows an angle and a power that are known to work on *this* layout.
    /// </summary>
    public class AngryBirdsHud : MonoBehaviour
    {
        private AngryBirdsGame _game;
        private GUIStyle _title;
        private GUIStyle _small;
        private GUIStyle _big;
        private GUIStyle _centred;
        private Texture2D _white;

        /// <summary>True while the pointer is over a panel, so taps there do not pull the sling.</summary>
        public static bool PointerOverPanel { get; private set; }

        private void Awake() => _game = GetComponent<AngryBirdsGame>();

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
            _big.normal.textColor = new Color(1f, 0.97f, 0.82f);

            _small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _small.normal.textColor = new Color(1f, 1f, 1f);

            _centred = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };

            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            _white.hideFlags = HideFlags.HideAndDontSave;
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

            DrawStatus(width);
            DrawButtons(width);
            DrawHint(width, height);

            switch (_game.State)
            {
                case AngryBirdsGame.Phase.Ready:
                case AngryBirdsGame.Phase.Aiming:
                    DrawReadyPanel(width, height);
                    break;
                case AngryBirdsGame.Phase.Cleared:
                    DrawResult(width, height, true);
                    break;
                case AngryBirdsGame.Phase.Failed:
                    DrawResult(width, height, false);
                    break;
            }

            GUI.matrix = previous;
        }

        private void DrawStatus(float width)
        {
            GUI.Label(new Rect(width * 0.5f - 140f, 10f, 280f, 46f), _game.Score.ToString(), _big);

            var level = _game.Level;
            int pigsLeft = level == null ? 0 : level.PigsAlive;

            string birds = "";
            for (int i = 0; i < (level == null ? 0 : level.Birds); i++)
            {
                birds += i < _game.BirdsLeft ? "● " : "○ ";
            }

            GUI.Label(new Rect(width * 0.5f - 230f, 60f, 460f, 22f),
                "第 " + _game.Stage + " 关　·　剩 " + pigsLeft + " 只猪　·　" + birds, _centred);

            GUI.Label(new Rect(width * 0.5f - 230f, 82f, 460f, 22f),
                DshMobile.MobileUi.UseTouchControls
                    ? "按住小鸟往后拉，松手发射"
                    : "按住鼠标往后拉，松手发射", _centred);
        }

        private void DrawButtons(float width)
        {
            var back = new Rect(width - 178f, 16f, 158f, 38f);
            if (GUI.Button(back, "回到宠物小屋", ButtonStyle()))
            {
                _game.ReturnToRoom();
            }
            PointerOverPanel |= back.Contains(Event.current.mousePosition);

            if (_game.HasHint)
            {
                var hint = new Rect(20f, 16f, 130f, 38f);
                if (GUI.Button(hint, "看提示", ButtonStyle()))
                {
                    _game.HintRequested = true;
                }
                PointerOverPanel |= hint.Contains(Event.current.mousePosition);
            }

            var restart = new Rect(20f, 62f, 130f, 38f);
            if (GUI.Button(restart, "重开这关", ButtonStyle()))
            {
                _game.RestartStage();
            }
            PointerOverPanel |= restart.Contains(Event.current.mousePosition);
        }

        private void DrawHint(float width, float height)
        {
            if (!_game.HintVisible || string.IsNullOrEmpty(_game.HintText)) return;

            var text = new GUIStyle(_small)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontStyle = FontStyle.Bold
            };
            text.normal.textColor = new Color(1f, 0.94f, 0.6f);

            GUI.Label(new Rect(width * 0.5f - 240f, height * 0.24f, 480f, 44f), _game.HintText, text);
        }

        private void DrawReadyPanel(float width, float height)
        {
            var level = _game.Level;
            if (level == null) return;

            var panel = new Rect(width * 0.5f - 250f, height * 0.62f, 500f, 116f);
            DshMobile.UiSkin.Panel(panel, 16f, new Color(0.09f, 0.10f, 0.14f, 0.86f),
                new Color(1f, 1f, 1f, 0.14f), 1.5f);

            GUI.Label(new Rect(panel.x + 18f, panel.y + 10f, panel.width - 36f, 30f),
                "第 " + _game.Stage + " 关", _title);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 44f, panel.width - 36f, 24f),
                BirdLevels.Blurb(level), _small);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 70f, panel.width - 36f, 24f),
                "每一关都是随机搭的，但都验证过一定打得通。", _small);

            PointerOverPanel = true;
        }

        private void DrawResult(float width, float height, bool cleared)
        {
            var panel = new Rect(width * 0.5f - 240f, height * 0.26f, 480f, 250f);
            DshMobile.UiSkin.Panel(panel, 16f, new Color(0.09f, 0.10f, 0.14f, 0.94f),
                new Color(1f, 1f, 1f, 0.16f), 1.5f);

            GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 36f),
                cleared ? "过关！" : "鸟用完了", _title);

            GUI.Label(new Rect(panel.x + 20f, panel.y + 56f, panel.width - 40f, 24f),
                "得分 " + _game.Score + "　·　剩 " + _game.BirdsLeft + " 只鸟", _small);

            if (cleared)
            {
                GUI.Label(new Rect(panel.x + 20f, panel.y + 82f, panel.width - 40f, 24f),
                    _game.Rank + "　·　赚了 " + _game.RunCoins + " 个宠物币", _small);
            }
            else
            {
                GUI.Label(new Rect(panel.x + 20f, panel.y + 82f, panel.width - 40f, 24f),
                    "再试一次，或者按「看提示」看验证过的角度。", _small);
            }

            var button = ButtonStyle();

            if (cleared && _game.HasNextStage)
            {
                if (GUI.Button(new Rect(panel.x + 24f, panel.y + 136f, 200f, 48f), "下一关", button))
                {
                    _game.NextStage();
                }
            }
            else if (cleared)
            {
                GUI.Label(new Rect(panel.x + 20f, panel.y + 112f, panel.width - 40f, 24f),
                    "十二关都过了，厉害。", _small);
                if (GUI.Button(new Rect(panel.x + 24f, panel.y + 136f, 200f, 48f), "从头再来", button))
                {
                    _game.LoadStage(1);
                }
            }
            else if (GUI.Button(new Rect(panel.x + 24f, panel.y + 136f, 200f, 48f), "再来一次", button))
            {
                _game.RestartStage();
            }

            if (GUI.Button(new Rect(panel.xMax - 224f, panel.y + 136f, 200f, 48f), "回到宠物小屋", button))
            {
                _game.ReturnToRoom();
            }

            PointerOverPanel = true;
        }

        private static GUIStyle ButtonStyle()
            => new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(14, 14, 8, 8) };
    }
}
