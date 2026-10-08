using System;
using UnityEngine;

namespace DshRunner
{
    /// <summary>
    /// Owns the run: state machine, score, difficulty, and the hand-off between the
    /// track, the player and the power-up system. Everything else reads state from here.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Wiring (auto-resolved when left empty)")]
        public PlayerController Player;
        public TrackManager Track;
        public PowerUpSystem PowerUps;
        public FollowCamera CameraRig;

        [Header("Run state")]
        public GameState State = GameState.Boot;
        public LevelDefinition Level;
        public readonly ScoreKeeper Score = new ScoreKeeper();

        /// <summary>1 while running normally; lowered by the slow-motion power-up.</summary>
        public float SpeedScale = 1f;

        public float CurrentSpeed { get; private set; }
        public float MaxDistanceThisRun { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<PowerUpKind, float> PowerUpStarted;

        /// <summary>Set by the HUD so the game-over screen can explain the death.</summary>
        public string LastFailReason = "";

        private bool _resolvedRefs;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Application.targetFrameRate = 120;
            // Keep simulating when the editor window is not focused, so a run continues
            // while the developer is looking at another window (and so automated
            // play-mode checks see the game actually advance).
            Application.runInBackground = true;
            ResolveRefs();
        }

        private void ResolveRefs()
        {
            if (_resolvedRefs) return;
            _resolvedRefs = true;

            if (Player == null) Player = FindObjectOfType<PlayerController>();
            if (Track == null) Track = FindObjectOfType<TrackManager>();
            if (PowerUps == null) PowerUps = FindObjectOfType<PowerUpSystem>();
            if (CameraRig == null) CameraRig = FindObjectOfType<FollowCamera>();
        }

        private void Start()
        {
            ResolveRefs();
            if (Track != null) Track.Rebuild(LevelLibrary.Endless, 0);
            SetState(GameState.Menu);
        }

        private void SetState(GameState next)
        {
            if (State == next) return;
            State = next;
            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            StateChanged?.Invoke(next);
        }

        // ---------------------------------------------------------------- run control

        public void StartRun(LevelDefinition level)
        {
            ResolveRefs();
            Level = level ?? LevelLibrary.Endless;

            Score.Reset();
            MaxDistanceThisRun = 0f;
            SpeedScale = 1f;
            LastFailReason = "";

            if (PowerUps != null) PowerUps.ClearAll();
            if (Track != null) Track.Rebuild(Level, 0);
            if (Player != null) Player.ResetForRun(GameConfig.LaneX(GameConfig.LaneCount / 2), 0f);
            if (CameraRig != null) CameraRig.SnapToTarget();

            SetState(GameState.Playing);
        }

        public void RestartRun() => StartRun(Level);

        public void ReturnToMenu()
        {
            SetState(GameState.Menu);
            if (Track != null) Track.Rebuild(LevelLibrary.Endless, 0);
            if (Player != null) Player.ResetForRun(GameConfig.LaneX(GameConfig.LaneCount / 2), 0f);
            if (CameraRig != null) CameraRig.SnapToTarget();
        }

        public void OpenLevelSelect() => SetState(GameState.LevelSelect);

        public void TogglePause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void Fail(string reason)
        {
            if (State != GameState.Playing) return;

            // A shield eats the hit instead of ending the run.
            if (PowerUps != null && PowerUps.ConsumeShield())
            {
                LastFailReason = "";
                return;
            }

            LastFailReason = reason;
            RecordRun();
            SetState(GameState.GameOver);
        }

        public void CompleteLevel()
        {
            if (State != GameState.Playing) return;
            // Finishing a level is what unlocks the next one, whether or not the player
            // goes on to press "next level".
            UnlockNextLevel();
            RecordRun();
            SetState(GameState.LevelComplete);
        }

        private void RecordRun()
        {
            var snap = Score.Snapshot();
            ProgressStore.BestScore = snap.Score;
            ProgressStore.BestDistance = snap.Distance;
            ProgressStore.AddCoins(snap.Coins);

            if (Level != null && !Level.Endless)
            {
                ProgressStore.RecordLevel(Level.Index, snap.Score);
            }
        }

        /// <summary>Called by the level-complete screen's "next level" button.</summary>
        public void UnlockNextLevel()
        {
            if (Level == null || Level.Endless) return;
            int next = Level.Index + 1;
            if (next < LevelLibrary.Count && next > ProgressStore.UnlockedLevels)
            {
                ProgressStore.UnlockedLevels = next;
            }
        }

        public void NextLevel()
        {
            UnlockNextLevel();
            if (Level == null || Level.Endless) { StartRun(LevelLibrary.Endless); return; }
            int next = Mathf.Min(Level.Index + 1, LevelLibrary.Count - 1);
            StartRun(LevelLibrary.Get(next));
        }

        // ---------------------------------------------------------------- frame loop

        private void Update()
        {
            switch (State)
            {
                case GameState.Playing:
                    TickRun();
                    break;
                case GameState.Menu:
                case GameState.LevelSelect:
                    if (Input.GetKeyDown(KeyCode.Escape)) { /* no-op, menus handle their own keys */ }
                    break;
                case GameState.Paused:
                    if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) TogglePause();
                    break;
            }

            // Global shortcuts that work in any in-run state.
            if (State == GameState.Playing && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)))
            {
                TogglePause();
            }
        }

        private void TickRun()
        {
            if (Player == null)
            {
                ResolveRefs();
                if (Player == null) return;
            }

            if (Level == null) Level = LevelLibrary.Endless;
            LevelDefinition level = Level;

            // Power-ups run first: they decide the speed scale and score multiplier this frame.
            if (PowerUps != null)
            {
                PowerUps.Tick(Time.deltaTime, Score.Distance, CurrentSpeed);
                Score.ExternalMultiplier = PowerUps.ScoreMultiplier;
            }

            float baseSpeed = Difficulty.Speed(level, Score.Distance);
            CurrentSpeed = baseSpeed * SpeedScale;

            float travelled = Player.ConsumeTraveledDistance();
            Score.Advance(travelled);
            MaxDistanceThisRun = Score.Distance;

            if (!level.Endless && Score.Distance >= level.TargetDistance)
            {
                CompleteLevel();
            }
        }

        /// <summary>Notified by the power-up system so the HUD can flash.</summary>
        public void NotifyPowerUp(PowerUpKind kind, float duration) => PowerUpStarted?.Invoke(kind, duration);
    }
}
