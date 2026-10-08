using UnityEngine;

namespace DshRunner
{
    /// <summary>
    /// Immediate-mode HUD and menus. IMGUI is used deliberately: it needs no Canvas,
    /// no font asset and no extra package, so the game runs in any project state.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        private GUIStyle _title;
        private GUIStyle _hud;
        private GUIStyle _hudSmall;
        private GUIStyle _button;
        private GUIStyle _buttonTiny;
        private GUIStyle _panel;
        private GUIStyle _center;
        private bool _stylesReady;

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _title.normal.textColor = new Color(0.75f, 0.95f, 1f);

            _hud = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _hud.normal.textColor = Color.white;

            _hudSmall = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _hudSmall.normal.textColor = new Color(0.85f, 0.9f, 1f);

            _button = new GUIStyle(GUI.skin.button) { fontSize = 20, padding = new RectOffset(18, 18, 10, 10) };
            _buttonTiny = new GUIStyle(GUI.skin.button) { fontSize = 15, padding = new RectOffset(10, 10, 6, 6) };

            _panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 18, 18) };

            _center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            _center.normal.textColor = Color.white;
        }

        /// <summary>
        /// Viewport in design pixels. The runner layout is written against a ~1280x720
        /// viewport and then scaled as a whole with GUI.matrix, so the draw methods must use
        /// these — reading Screen.width under a scaling matrix would scale everything twice.
        /// </summary>
        private static float W = 1280f;
        private static float H = 720f;

        private void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            EnsureStyles();

            // Same scaling rule as the pet HUD: lay out in design pixels, apply one matrix.
            var safe = DshMobile.MobileUi.SafeArea;
            float scale = DshMobile.MobileUi.UseTouchControls ? DshMobile.MobileUi.UiScale : 1f;
            W = Mathf.Max(320f, safe.width / scale);
            H = Mathf.Max(240f, safe.height / scale);

            var previousMatrix = GUI.matrix;
            if (scale != 1f || safe.x != 0f || safe.y != 0f)
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, safe.y, 0f), Quaternion.identity,
                    new Vector3(scale, scale, 1f));
            }

            DshMobile.MobileTouch.PlayInputEnabled = gm.State == GameState.Playing;

            if (gm.State == GameState.Playing) MaybeArmGestureHint();

            switch (gm.State)
            {
                case GameState.Menu: DrawMenu(gm); break;
                case GameState.LevelSelect: DrawLevelSelect(gm); break;
                case GameState.Playing:
                    DrawHud(gm);
                    DrawTouchControls(gm, scale, safe);
                    DrawGestureHint();
                    break;
                case GameState.Paused: DrawHud(gm); DrawTouchControls(gm, scale, safe); DrawPaused(gm); break;
                case GameState.GameOver: DrawGameOver(gm); break;
                case GameState.LevelComplete: DrawLevelComplete(gm); break;
            }

            GUI.matrix = previousMatrix;
        }

        /// <summary>
        /// A modal panel for the menus.
        ///
        /// GUI.skin.box is translucent, so the forest showed straight through the menu and the
        /// text fought the treeline for contrast. A rounded, opaque panel with a shadow is both
        /// readable and the difference between "a debug window over a game" and "a game menu".
        /// </summary>
        private static void MenuPanel(Rect rect)
        {
            DshMobile.UiSkin.Panel(rect, 18f, new Color(0.09f, 0.10f, 0.13f, 0.97f),
                new Color(1f, 1f, 1f, 0.14f), 2f, shadow: true);
        }

        /// <summary>
        /// On-screen runner controls: pause, and nothing else.
        ///
        /// The lane/jump/slide buttons are gone. They sat in the bottom corners, which is
        /// exactly where a thumb rests while swiping, so the buttons and the gesture fought
        /// each other and the screen was permanently cluttered with four large rectangles that
        /// the player did not need once they had learned the swipes. Movement is gesture-only
        /// now — swipe left/right to change lane, up or tap to jump, down to slide — which is
        /// how every runner on a phone works.
        ///
        /// A first-run hint fades in over the first seconds of a run to teach that (see
        /// <see cref="DrawGestureHint"/>); after that the screen belongs to the game.
        /// </summary>
        private void DrawTouchControls(GameManager gm, float scale, Rect safe)
        {
            if (!DshMobile.MobileUi.UseTouchControls) return;

            // The widgets draw in the HUD's design space and register screen-space hit areas;
            // this tells them how the two relate. See DshMobile.MobileWidgets.
            DshMobile.MobileWidgets.BeginFrame(scale, new Vector2(safe.x, safe.y));

            // Pause sits top-right, out of both thumbs' way.
            var pause = new Rect(W - 20f - DshMobile.MobileUi.Touchable(64f), 16f,
                DshMobile.MobileUi.Touchable(64f), DshMobile.MobileUi.Touchable(44f));
            if (DshMobile.MobileWidgets.Button(DshMobile.MobileButtonIds.RunnerPause,
                    pause, "暂停", new Color(0.45f, 0.45f, 0.5f)))
            {
                gm.TogglePause();
            }
        }

        /// <summary>
        /// The swipe tutorial, shown over the first seconds of the first run.
        ///
        /// Drawn only while <see cref="_gestureHintUntil"/> is in the future, and faded out so
        /// it never has to be dismissed. It exists because removing the buttons removes the only
        /// place the controls were explained.
        /// </summary>
        private void DrawGestureHint()
        {
            float remaining = _gestureHintUntil - Time.unscaledTime;
            if (remaining <= 0f) return;

            float alpha = Mathf.Clamp01(remaining / 1.2f) * Mathf.Clamp01((GestureHintSeconds - remaining) / 0.5f);
            if (alpha <= 0.01f) return;

            var panel = new Rect(W * 0.5f - 240f, H * 0.5f - 60f, 480f, 120f);
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            DshMobile.UiSkin.Panel(panel, 18f, new Color(0.07f, 0.08f, 0.11f, 0.78f),
                new Color(1f, 1f, 1f, 0.18f), 2f);

            var style = new GUIStyle(_hudSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            var big = new GUIStyle(_title) { fontSize = 26 };

            GUI.Label(new Rect(panel.x, panel.y + 14f, panel.width, 30f), "滑动屏幕来操作", big);
            GUI.Label(new Rect(panel.x, panel.y + 52f, panel.width, 26f),
                "← → 换道　　↑ 或点一下 跳跃（按住更高）　　↓ 滑铲", style);
            GUI.Label(new Rect(panel.x, panel.y + 80f, panel.width, 24f), "也可以直接点屏幕跳跃", _hudSmall);

            GUI.color = previous;
        }

        /// <summary>Seconds the swipe tutorial stays on screen at the start of a run.</summary>
        private const float GestureHintSeconds = 5.5f;

        private static float _gestureHintUntil;
        private static bool _gestureHintShown;

        /// <summary>
        /// Arms the tutorial the first time a run starts.
        ///
        /// Once per launch rather than once per run: a player who restarts after dying does not
        /// need the controls explained again, and a hint that reappears every thirty seconds is
        /// just an obstruction.
        /// </summary>
        private static void MaybeArmGestureHint()
        {
            if (!DshMobile.MobileUi.UseTouchControls) return;
            if (_gestureHintShown) return;

            _gestureHintShown = true;
            _gestureHintUntil = Time.unscaledTime + GestureHintSeconds;
        }

        // ------------------------------------------------------------------- in-game

        private void DrawHud(GameManager gm)
        {
            var score = gm.Score.Snapshot();

            GUILayout.BeginArea(new Rect(20, 16, 320, 200));
            GUILayout.Label($"{score.Score:N0}", _hud);
            GUILayout.Label($"{score.Distance:F0} m   倍率 x{score.Multiplier:F2}", _hudSmall);
            GUILayout.Label($"果子 {score.Coins}", _hudSmall);
            GUILayout.Label($"速度 {gm.CurrentSpeed:F1} m/s", _hudSmall);
            GUILayout.EndArea();

            // Level progress (only for finite levels).
            if (gm.Level != null && !gm.Level.Endless)
            {
                float progress = Difficulty.Progress(gm.Level, score.Distance);
                var bar = new Rect(W * 0.5f - 160f, 22f, 320f, 16f);
                GUI.color = new Color(1f, 1f, 1f, 0.25f);
                GUI.Box(bar, GUIContent.none);
                GUI.color = new Color(0.4f, 0.95f, 1f, 0.95f);
                GUI.Box(new Rect(bar.x, bar.y, bar.width * progress, bar.height), GUIContent.none);
                GUI.color = Color.white;

                GUI.Label(new Rect(bar.x, bar.y + 20f, bar.width, 22f),
                    $"{gm.Level.Name}  {score.Distance:F0}/{gm.Level.TargetDistance:F0} m", _center);
            }

            DrawPowerUps(gm);

            // The hint line is the only place the controls are explained, so it has to match
            // the platform the player is holding. On a phone it also has to move: at the
            // viewport's bottom edge it sat underneath the touch button row, which covered
            // the half of it describing the on-screen buttons.
            bool touch = DshMobile.MobileUi.UseTouchControls;
            string hint = touch
                ? "滑动换道 · 上滑或点击跳跃 · 下滑滑铲"
                : "A/D 或 ←/→ 换道    W/↑/空格 跳跃    S/↓ 滑铲    P/Esc 暂停";

            float hintY = touch ? TouchButtonRowTop - 26f : H - 30f;

            // Wide enough not to clip, but never so wide that it runs under the "back to the
            // room" button, which sits on the same row at the right on a phone. (The landscape
            // rect used to be a fixed 46% of the viewport, which is narrower than this sentence
            // on a portrait screen — the tail of the hint was simply cut off.)
            float hintWidth = W - 40f - (ReturnToRoomVisible ? 176f : 0f);
            GUI.Label(new Rect(20f, hintY, Mathf.Max(120f, hintWidth), 24f), hint, _hudSmall);

            DrawReturnToRoom();
        }

        /// <summary>Edge length of a touch button, in design pixels.</summary>
        private static float TouchButtonSize => DshMobile.MobileUi.Touchable(90f);

        /// <summary>Top edge of the touch button row, in design pixels.</summary>
        private static float TouchButtonRowTop => H - 18f - TouchButtonSize;

        /// <summary>True when the run was started from the pet room, so the way home is offered.</summary>
        private static bool ReturnToRoomVisible => PlayerPrefs.GetInt(AwayFlagKey, 0) == 1;

        /// <summary>
        /// When the run was started from the pet room, offer the way home. The two gameplay
        /// assemblies deliberately do not reference each other, so the hand-off is a
        /// PlayerPrefs flag and a scene name rather than a direct call.
        /// </summary>
        private static void DrawReturnToRoom()
        {
            if (!ReturnToRoomVisible) return;

            // Above the touch button row on a phone; at the bottom edge it landed on the jump
            // button, which is exactly the button a player is holding when they would want it.
            float y = DshMobile.MobileUi.UseTouchControls ? TouchButtonRowTop - 40f : H - 44f;

            if (GUI.Button(new Rect(W - 168f, y, 148f, 30f), "返回宠物小屋"))
            {
                ReturnToRoom();
            }
        }

        /// <summary>
        /// Goes back to the pet room and clears the "away" flag, so the pet's HUD stops
        /// offering to bring the player home.
        /// </summary>
        public static void ReturnToRoom()
        {
            PlayerPrefs.SetInt(AwayFlagKey, 0);
            PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(RoomSceneName);
        }

        private const string AwayFlagKey = "dshpet.away";
        private const string RoomSceneName = "PetRoom";

        private void DrawPowerUps(GameManager gm)
        {
            var powerUps = gm.PowerUps;
            if (powerUps == null) return;

            float x = W - 240f;

            // On a phone the pause button lives in the top-right corner, which is where the
            // power-up pills would otherwise end: shift them clear of it.
            if (DshMobile.MobileUi.UseTouchControls) x -= DshMobile.MobileUi.Touchable(64f) + 14f;

            float y = 16f;
            var kinds = new[]
            {
                PowerUpKind.Shield, PowerUpKind.Magnet,
                PowerUpKind.DoubleScore, PowerUpKind.SlowMotion
            };

            foreach (var kind in kinds)
            {
                float remaining = powerUps.Remaining(kind);
                bool active = kind == PowerUpKind.Shield ? powerUps.ShieldActive : remaining > 0f;
                if (!active) continue;

                var rect = new Rect(x, y, 220f, 26f);
                GUI.color = new Color(1f, 1f, 1f, 0.2f);
                GUI.Box(rect, GUIContent.none);
                GUI.color = PowerUpTint(kind);
                GUI.Box(new Rect(rect.x, rect.y, rect.width * powerUps.Normalised(kind), rect.height), GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width, rect.height),
                    $"{powerUps.Describe(kind)}  {remaining:F1}s", _hudSmall);
                y += 32f;
            }
        }

        private static Color PowerUpTint(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Shield: return new Color(0.35f, 0.85f, 1f, 0.85f);
                case PowerUpKind.Magnet: return new Color(1f, 0.55f, 0.2f, 0.85f);
                case PowerUpKind.DoubleScore: return new Color(1f, 0.9f, 0.3f, 0.85f);
                case PowerUpKind.SlowMotion: return new Color(0.6f, 0.6f, 1f, 0.85f);
                default: return new Color(1f, 1f, 1f, 0.85f);
            }
        }

        // --------------------------------------------------------------------- menus

        private void DrawMenu(GameManager gm)
        {
            float w = 460f;
            float h = 470f;
            var rect = new Rect(W * 0.5f - w * 0.5f, H * 0.5f - h * 0.5f, w, h);

            MenuPanel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 20f, w - 48f, h - 40f));

            GUILayout.Label("森 林 奔 跑", _title);
            GUILayout.Space(6f);
            GUILayout.Label("FOREST RUNNER · 换道 / 跳跃 / 滑铲 / 道具", _center);
            GUILayout.Space(18f);

            GUILayout.Label($"最高分  {ProgressStore.BestScore:N0}", _hudSmall);
            GUILayout.Label($"最远距离  {ProgressStore.BestDistance:F0} m", _hudSmall);
            GUILayout.Label($"累计果子  {ProgressStore.TotalCoins:N0}", _hudSmall);
            GUILayout.Space(18f);

            if (GUILayout.Button("无尽模式  (Enter)", _button, GUILayout.Height(48f)))
            {
                gm.StartRun(LevelLibrary.Endless);
            }

            if (GUILayout.Button("关卡模式  (L)", _button, GUILayout.Height(48f)))
            {
                gm.OpenLevelSelect();
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("重置进度", _buttonTiny, GUILayout.Height(30f)))
            {
                ProgressStore.ResetAll();
            }

            // The way home is offered here too, not only mid-run: a player who came out of the
            // cabin and landed on this menu had no way back except quitting the game, which is
            // a dead end the moment the two scenes are one world.
            if (ReturnToRoomVisible)
            {
                GUILayout.Space(10f);
                if (GUILayout.Button("返回宠物小屋", _button, GUILayout.Height(40f)))
                {
                    ReturnToRoom();
                }
            }

            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    gm.StartRun(LevelLibrary.Endless);
                }
                else if (Event.current.keyCode == KeyCode.L)
                {
                    gm.OpenLevelSelect();
                }
            }
        }

        private void DrawLevelSelect(GameManager gm)
        {
            float w = 720f;
            float h = 560f;
            var rect = new Rect(W * 0.5f - w * 0.5f, H * 0.5f - h * 0.5f, w, h);

            MenuPanel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 18f, w - 48f, h - 36f));

            GUILayout.Label("选择关卡", _title);
            GUILayout.Space(10f);

            for (int i = 1; i < LevelLibrary.Count; i++)
            {
                var level = LevelLibrary.Get(i);
                bool unlocked = i <= ProgressStore.UnlockedLevels;
                int best = ProgressStore.LevelBest(i);
                string label = unlocked
                    ? $"{level.Name}   ·   {level.Blurb}   ·   目标 {level.TargetDistance:F0} m   ·   最佳 {best:N0}"
                    : $"{level.Name}   ·   未解锁（先通关第 {i - 1} 关）";

                GUI.enabled = unlocked;
                if (GUILayout.Button(label, _button, GUILayout.Height(40f)))
                {
                    gm.StartRun(level);
                }
                GUI.enabled = true;
            }

            GUILayout.Space(12f);
            if (GUILayout.Button("返回主菜单  (Esc)", _button, GUILayout.Height(40f)))
            {
                gm.ReturnToMenu();
            }

            if (ReturnToRoomVisible)
            {
                if (GUILayout.Button("返回宠物小屋", _button, GUILayout.Height(40f)))
                {
                    ReturnToRoom();
                }
            }

            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                gm.ReturnToMenu();
            }
        }

        private void DrawPaused(GameManager gm)
        {
            var rect = CenterBox(360f, 300f);
            MenuPanel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 24f, rect.width - 48f, rect.height - 48f));

            GUILayout.Label("暂停", _title);
            GUILayout.Space(14f);

            if (GUILayout.Button("继续  (Esc)", _button, GUILayout.Height(44f))) gm.TogglePause();
            if (GUILayout.Button("重新开始  (R)", _button, GUILayout.Height(44f))) gm.RestartRun();
            if (GUILayout.Button("返回主菜单", _button, GUILayout.Height(44f))) gm.ReturnToMenu();
            if (ReturnToRoomVisible && GUILayout.Button("返回宠物小屋", _button, GUILayout.Height(44f)))
            {
                ReturnToRoom();
            }

            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R)
            {
                gm.RestartRun();
            }
        }

        private void DrawGameOver(GameManager gm)
        {
            var score = gm.Score.Snapshot();
            var rect = CenterBox(440f, 380f);
            MenuPanel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 22f, rect.width - 48f, rect.height - 44f));

            GUILayout.Label("游戏结束", _title);
            if (!string.IsNullOrEmpty(gm.LastFailReason))
            {
                GUILayout.Label(gm.LastFailReason, _center);
            }
            GUILayout.Space(12f);

            GUILayout.Label($"本次得分  {score.Score:N0}", _hud);
            GUILayout.Label($"距离  {score.Distance:F0} m      金币  {score.Coins}", _hudSmall);
            GUILayout.Label($"最高分  {ProgressStore.BestScore:N0}", _hudSmall);
            GUILayout.Space(16f);

            if (GUILayout.Button("再来一次  (R)", _button, GUILayout.Height(46f))) gm.RestartRun();
            if (GUILayout.Button("返回主菜单", _button, GUILayout.Height(46f))) gm.ReturnToMenu();
            if (ReturnToRoomVisible && GUILayout.Button("返回宠物小屋", _button, GUILayout.Height(46f)))
            {
                ReturnToRoom();
            }

            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R)
            {
                gm.RestartRun();
            }
        }

        private void DrawLevelComplete(GameManager gm)
        {
            var score = gm.Score.Snapshot();
            bool hasNext = gm.Level != null && !gm.Level.Endless && gm.Level.Index + 1 < LevelLibrary.Count;

            var rect = CenterBox(460f, 400f);
            MenuPanel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 22f, rect.width - 48f, rect.height - 44f));

            GUILayout.Label("通关！", _title);
            GUILayout.Space(10f);
            GUILayout.Label($"本次得分  {score.Score:N0}", _hud);
            GUILayout.Label($"距离  {score.Distance:F0} m      金币  {score.Coins}", _hudSmall);
            GUILayout.Space(16f);

            if (hasNext)
            {
                if (GUILayout.Button("下一关  (Enter)", _button, GUILayout.Height(46f))) gm.NextLevel();
            }
            if (GUILayout.Button("重玩本关  (R)", _button, GUILayout.Height(46f))) gm.RestartRun();
            if (GUILayout.Button("返回主菜单", _button, GUILayout.Height(46f))) gm.ReturnToMenu();
            if (ReturnToRoomVisible && GUILayout.Button("返回宠物小屋", _button, GUILayout.Height(46f)))
            {
                ReturnToRoom();
            }

            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.R) gm.RestartRun();
                else if (hasNext && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
                {
                    gm.NextLevel();
                }
            }
        }

        private static Rect CenterBox(float w, float h)
            => new Rect(W * 0.5f - w * 0.5f, H * 0.5f - h * 0.5f, w, h);
    }
}
