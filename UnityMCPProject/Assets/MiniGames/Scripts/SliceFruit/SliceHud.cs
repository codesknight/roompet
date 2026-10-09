using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// 切水果's interface: the score, the lives, the mode, and the blade.
    ///
    /// The blade trail is drawn here rather than in the game because it is a *drawing* of the swipe
    /// the game already computed — one source of truth (the world-space segment in
    /// <see cref="SliceGame.BladeFrom"/>) and one place that turns it into pixels. Drawing it in the
    /// game would have meant a second copy of the same input, free to disagree with the hit test.
    /// </summary>
    public class SliceHud : MonoBehaviour
    {
        private SliceGame _game;
        private GUIStyle _title;
        private GUIStyle _small;
        private GUIStyle _big;
        private GUIStyle _centred;
        private Texture2D _white;

        /// <summary>True while the pointer is over a panel, so taps there do not start a run.</summary>
        public static bool PointerOverPanel { get; private set; }

        private void Awake() => _game = GetComponent<SliceGame>();

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.98f, 0.9f);

            _big = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _big.normal.textColor = new Color(1f, 0.97f, 0.82f);

            _small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _small.normal.textColor = new Color(0.96f, 0.97f, 1f);

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

            DrawBlade(width, height);
            DrawScore(width, height);
            DrawButtons(width);

            if (_game.Paused)
            {
                DrawPaused(width, height);
            }
            else if (_game.State == SliceGame.Phase.Ready) DrawReady(width, height);
            else if (_game.State == SliceGame.Phase.LevelDone) DrawLevelDone(width, height);
            else if (_game.State == SliceGame.Phase.Dead) DrawDead(width, height);

            GUI.matrix = previous;
        }

        /// <summary>
        /// The blade: the swipe's recent history, turned back into screen pixels.
        ///
        /// This used to draw one frame's segment, which is why it did not follow the finger: on a fast
        /// swipe the segment is short, and on any frame where the finger paused there was no segment at
        /// all, so the "blade" blinked out from under the player's hand. It is a polyline of the last
        /// fraction of a second now — tapered, fading towards its tail, with the tip drawn exactly under
        /// the finger.
        /// </summary>
        private void DrawBlade(float width, float height)
        {
            if (_game == null) return;
            var camera = Camera.main;
            if (camera == null) return;

            var trail = _game.BladeTrail;
            if (trail == null || trail.Count < 2) return;

            int count = trail.Count;
            for (int i = 0; i < count; i++)
            {
                var sample = trail[i];
                float along = i / (float)(count - 1);          // 0 = tail, 1 = newest
                float fade = Mathf.Clamp01(1f - sample.Age / SliceGame.TrailSeconds);

                var point = WorldToGui(camera, sample.Position);
                float fat = Mathf.Lerp(0.30f, 1f, along);
                float size = 30f * fat;

                var rect = new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
                var was = GUI.color;
                GUI.color = new Color(1f, 0.98f, 0.88f, 0.42f * fat * fade);
                GUI.DrawTexture(rect, _white);
                GUI.color = was;
            }

            // The tip: a brighter disc under the finger, so the blade is visibly *in hand* even when
            // the swipe is slow enough that the tail has almost nothing in it.
            if (_game.PointerDown)
            {
                var tip = WorldToGui(camera, _game.PointerPosition);
                float size = 34f;
                var was = GUI.color;
                GUI.color = new Color(1f, 1f, 0.94f, 0.55f);
                GUI.DrawTexture(new Rect(tip.x - size * 0.5f, tip.y - size * 0.5f, size, size), _white);
                GUI.color = was;
            }
        }

        /// <summary>World → IMGUI coordinates (which are top-down, like the touch layer's).</summary>
        private static Vector2 WorldToGui(Camera camera, Vector2 world)
        {
            var screen = camera.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        private void DrawScore(float width, float height)
        {
            GUI.Label(new Rect(width * 0.5f - 140f, 14f, 280f, 60f), _game.Score.ToString(), _big);

            string lives = "";
            for (int i = 0; i < SliceRules.StartLives; i++) lives += i < _game.Lives ? "● " : "○ ";

            string mode = _game.Mode == SliceMode.Endless
                ? "无尽模式"
                : _game.Plan.Name + "　第 " + Mathf.Min(_game.Wave, _game.Plan.Waves) + "/" + _game.Plan.Waves + " 波";

            GUI.Label(new Rect(width * 0.5f - 210f, 74f, 420f, 22f),
                lives + "　·　" + mode, _centred);

            if (_game.Mode == SliceMode.Levels)
            {
                GUI.Label(new Rect(width * 0.5f - 210f, 96f, 420f, 22f),
                    "目标 " + _game.Plan.TargetScore + " 分　·　漏 " + _game.Missed + "/" + _game.Plan.MaxMisses +
                    (_game.Plan.BombChance > 0f ? "　·　有炸弹" : ""), _centred);
            }
            else
            {
                GUI.Label(new Rect(width * 0.5f - 210f, 96f, 420f, 22f),
                    "最高 " + _game.Best + "　·　🐾 " + DshMobile.PetWallet.Coins.ToString("N0"), _centred);
            }

            if (_game.PopupVisible && !string.IsNullOrEmpty(_game.Popup))
            {
                var style = new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = _game.Popup.StartsWith("炸弹") ? new Color(1f, 0.5f, 0.42f)
                                                                      : new Color(1f, 0.92f, 0.6f);
                GUI.Label(new Rect(width * 0.5f - 200f, height * 0.36f, 400f, 40f), _game.Popup, style);
            }
        }

        private void DrawButtons(float width)
        {
            var back = new Rect(width - 178f, 16f, 158f, 38f);
            if (GUI.Button(back, "回到宠物小屋", ButtonStyle()))
            {
                _game.ReturnToRoom();
            }
            PointerOverPanel |= back.Contains(Event.current.mousePosition);

            // Pause, wherever the player is in a run: the button that stops the fruit is the one thing
            // a game with a three-second rhythm has to offer without a menu.
            var pause = new Rect(width - 178f, 62f, 158f, 38f);
            if (GUI.Button(pause, _game.Paused ? "继续" : "暂停", ButtonStyle()))
            {
                _game.SetPaused(!_game.Paused);
            }
            PointerOverPanel |= pause.Contains(Event.current.mousePosition);

            if (_game.State != SliceGame.Phase.Dead && _game.State != SliceGame.Phase.LevelDone)
            {
                var mode = new Rect(20f, 16f, 150f, 38f);
                if (GUI.Button(mode, _game.Mode == SliceMode.Endless ? "无尽模式" : "闯关模式",
                        ButtonStyle()))
                {
                    _game.SetMode(_game.Mode == SliceMode.Endless ? SliceMode.Levels : SliceMode.Endless);
                }
                PointerOverPanel |= mode.Contains(Event.current.mousePosition);
            }
        }

        /// <summary>
        /// The pause panel.
        ///
        /// Nothing in the world is frozen by a global clock: <see cref="SliceGame.SetPaused"/> stops the
        /// game's own update, so pausing cannot leak into the next scene (which is exactly how the pet
        /// room came back immobile in round 17).
        /// </summary>
        private void DrawPaused(float width, float height)
        {
            var panel = new Rect(width * 0.5f - 210f, height * 0.3f, 420f, 210f);
            DshMobile.UiSkin.Panel(panel, 16f, new Color(0.10f, 0.08f, 0.13f, 0.95f),
                new Color(1f, 1f, 1f, 0.16f), 1.5f);

            GUI.Label(new Rect(panel.x + 20f, panel.y + 16f, panel.width - 40f, 36f), "暂停", _title);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 58f, panel.width - 40f, 24f),
                "得分 " + _game.Score + "　·　漏了 " + _game.Missed + " 个", _small);

            var button = ButtonStyle();
            if (GUI.Button(new Rect(panel.x + 24f, panel.y + 104f, 180f, 46f), "继续", button))
            {
                _game.SetPaused(false);
            }

            if (GUI.Button(new Rect(panel.xMax - 204f, panel.y + 104f, 180f, 46f),
                    _game.Mode == SliceMode.Levels ? "重玩这一关" : "重新开始", button))
            {
                _game.ResetRun(_game.Mode);
            }

            if (GUI.Button(new Rect(panel.x + 24f, panel.y + 156f, 180f, 46f), "回到宠物小屋", button))
            {
                _game.ReturnToRoom();
            }

            PointerOverPanel = true;
        }

        private void DrawReady(float width, float height)
        {
            var panel = new Rect(width * 0.5f - 240f, height * 0.3f, 480f, 168f);
            DshMobile.UiSkin.Panel(panel, 16f, new Color(0.10f, 0.08f, 0.13f, 0.94f),
                new Color(1f, 1f, 1f, 0.16f), 1.5f);

            GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 34f), "切水果", _title);

            if (_game.Mode == SliceMode.Endless)
            {
                GUI.Label(new Rect(panel.x + 20f, panel.y + 54f, panel.width - 40f, 24f),
                    DshMobile.MobileUi.UseTouchControls
                        ? "在屏幕上滑动切水果，别切到炸弹"
                        : "按住鼠标划过水果，别碰到炸弹", _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 80f, panel.width - 40f, 24f),
                    "漏掉一个水果或切到炸弹，就少一条命（三条）。", _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 106f, panel.width - 40f, 24f),
                    "水果越切越多、越来越快，看你能切到多少分。", _small);
            }
            else
            {
                GUI.Label(new Rect(panel.x + 20f, panel.y + 54f, panel.width - 40f, 24f),
                    _game.Plan.Name + "：" + _game.Plan.Waves + " 波，每波 " + _game.Plan.FruitsPerWave + " 个",
                    _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 80f, panel.width - 40f, 24f),
                    "目标 " + _game.Plan.TargetScore + " 分　·　最多漏 " + _game.Plan.MaxMisses + " 个", _small);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 106f, panel.width - 40f, 24f),
                    SliceRules.DifficultyFor(_game.Plan), _small);
            }

            GUI.Label(new Rect(panel.x + 20f, panel.y + 134f, panel.width - 40f, 24f),
                "滑动一下就开始。", _centred);

            PointerOverPanel = true;
        }

        private void DrawLevelDone(float width, float height)
        {
            var panel = new Rect(width * 0.5f - 240f, height * 0.28f, 480f, 240f);
            DshMobile.UiSkin.Panel(panel, 16f, new Color(0.10f, 0.08f, 0.13f, 0.94f),
                new Color(1f, 1f, 1f, 0.16f), 1.5f);

            bool passed = _game.LevelPassed;
            GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 36f),
                passed ? "过关！" : "没到目标分", _title);

            GUI.Label(new Rect(panel.x + 20f, panel.y + 56f, panel.width - 40f, 24f),
                _game.Plan.Name + "　·　得分 " + _game.Score + " / 目标 " + _game.Plan.TargetScore, _small);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 82f, panel.width - 40f, 24f),
                "漏了 " + _game.Missed + " 个", _small);

            var button = ButtonStyle();

            if (passed && _game.Level < SliceRules.LevelCount)
            {
                if (GUI.Button(new Rect(panel.x + 24f, panel.y + 132f, 200f, 48f), "下一关", button))
                {
                    _game.NextLevel();
                }
            }
            else if (passed)
            {
                GUI.Label(new Rect(panel.x + 20f, panel.y + 112f, panel.width - 40f, 24f),
                    "八关都过了，厉害。", _small);
                if (GUI.Button(new Rect(panel.x + 24f, panel.y + 132f, 200f, 48f), "从头再来", button))
                {
                    _game.SetMode(SliceMode.Levels);
                }
            }
            else
            {
                if (GUI.Button(new Rect(panel.x + 24f, panel.y + 132f, 200f, 48f), "再试一次", button))
                {
                    _game.ResetRun(SliceMode.Levels);
                }
            }

            if (GUI.Button(new Rect(panel.xMax - 224f, panel.y + 132f, 200f, 48f), "无尽模式", button))
            {
                _game.SetMode(SliceMode.Endless);
            }

            PointerOverPanel = true;
        }

        private void DrawDead(float width, float height)
        {
            var panel = new Rect(width * 0.5f - 240f, height * 0.28f, 480f, 250f);
            DshMobile.UiSkin.Panel(panel, 16f, new Color(0.10f, 0.08f, 0.13f, 0.94f),
                new Color(1f, 1f, 1f, 0.16f), 1.5f);

            GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 36f), "切完了", _title);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 56f, panel.width - 40f, 24f),
                "得分 " + _game.Score + "　·　漏了 " + _game.Missed + " 个", _small);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 82f, panel.width - 40f, 24f),
                SliceRules.RankFor(_game.Score) + "　·　赚了 " + _game.RunCoins + " 个宠物币", _small);

            var button = ButtonStyle();
            if (GUI.Button(new Rect(panel.x + 24f, panel.y + 136f, 200f, 48f), "再来一次", button))
            {
                _game.ResetRun(_game.Mode);
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
