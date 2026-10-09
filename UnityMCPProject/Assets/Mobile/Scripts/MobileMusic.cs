using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMobile
{
    /// <summary>The pieces of background music the game has, one per place or activity.</summary>
    public enum MusicId
    {
        None = 0,
        Menu = 1,          // 开始界面
        PetRoom = 2,       // 小屋：养宠物待着的地方
        Garden = 3,        // 花园
        Terrace = 4,       // 夜晚露台
        RunnerForest = 5,  // 森林奔跑
        FlyBird = 6,       // 小鸟飞行
        JumpQuest = 7,     // 跳一跳
        CatchFruit = 8,    // 接果子
        SliceFruit = 9,    // 切水果
        AngryBirds = 10,   // 弹弓小鸟
        Puzzle = 11        // 屋里那两个小面板（拼图 / 记忆配对）
    }

    /// <summary>One track: where the file lives, what to call it, and who to thank.</summary>
    public class MusicTrack
    {
        public MusicId Id;
        public string Resource = "";
        public string Title = "";
        public string Credit = "";

        /// <summary>What the settings panel says is playing, credits included.</summary>
        public string Describe()
            => string.IsNullOrEmpty(Credit) ? Title : Title + "（" + Credit + "）";
    }

    /// <summary>
    /// Background music: one looping track per scene, with a crossfade and a switch.
    ///
    /// It lives in DshMobile rather than with the pet because *every* scene wants it — the pet's
    /// room, the runner, and all five mini-game scenes — and those assemblies cannot see each
    /// other. The wiring is by scene name, so a new mini-game gets music by adding one row to the
    /// table: nothing to remember, nothing to forget.
    ///
    /// The switch is its own setting, deliberately not folded into the sound-effects mute: the
    /// player who wants to hear the pet's footsteps and chirps but not a soundtrack is the exact
    /// person who would ask for this, and "all audio off" is the one answer that never helps them.
    /// </summary>
    public static class MobileMusic
    {
        public const string EnabledKey = "dshmusic.enabled";
        public const string VolumeKey = "dshmusic.volume";

        /// <summary>Default level: music sits under the sound effects rather than next to them.</summary>
        public const float DefaultVolume = 0.45f;

        /// <summary>Seconds a track takes to fade in or out.</summary>
        public const float FadeSeconds = 0.7f;

        /// <summary>Where the tracks live under a Resources folder, and what each one is.
        /// All eleven are CC0 1.0 (see docs/MUSIC_SOURCES.md); the credit line is a courtesy,
        /// not a legal requirement.</summary>
        public static readonly MusicTrack[] Tracks =
        {
            new MusicTrack { Id = MusicId.Menu, Resource = "Music/menu_theme", Title = "A New Town", Credit = "cynicmusic" },
            new MusicTrack { Id = MusicId.PetRoom, Resource = "Music/pet_room", Title = "Napping on a Cloud", Credit = "congusbongus" },
            new MusicTrack { Id = MusicId.Garden, Resource = "Music/garden", Title = "Flowerbed Fields", Credit = "Zane Little Music" },
            new MusicTrack { Id = MusicId.Terrace, Resource = "Music/terrace", Title = "Somewhere in the Elevator", Credit = "You're Perfect Studio" },
            new MusicTrack { Id = MusicId.RunnerForest, Resource = "Music/runner_forest", Title = "Free Run", Credit = "TAD" },
            new MusicTrack { Id = MusicId.FlyBird, Resource = "Music/fly_bird", Title = "Funky Disco Beats", Credit = "Fupi" },
            new MusicTrack { Id = MusicId.JumpQuest, Resource = "Music/jump_quest", Title = "Party Sector", Credit = "Joth" },
            new MusicTrack { Id = MusicId.CatchFruit, Resource = "Music/catch_fruit", Title = "Raspberry Jam", Credit = "congusbongus" },
            new MusicTrack { Id = MusicId.SliceFruit, Resource = "Music/slice_fruit", Title = "The Rush", Credit = "tebruno99" },
            new MusicTrack { Id = MusicId.AngryBirds, Resource = "Music/angry_birds", Title = "Toy Soldiers", Credit = "Zane Little Music" },
            new MusicTrack { Id = MusicId.Puzzle, Resource = "Music/puzzle", Title = "Contemplation", Credit = "Joth" }
        };

        private static bool _enabledLoaded;
        private static bool _enabled = true;
        private static float _volume = DefaultVolume;
        private static bool _volumeLoaded;

        private static MusicId _current = MusicId.None;
        private static MusicId _target = MusicId.None;
        private static string _note = "";

        /// <summary>The player's setting, remembered across runs. On by default.</summary>
        public static bool Enabled
        {
            get
            {
                if (!_enabledLoaded)
                {
                    _enabled = PlayerPrefs.GetInt(EnabledKey, 1) != 0;
                    _enabledLoaded = true;
                }
                return _enabled;
            }
            set
            {
                _enabled = value;
                _enabledLoaded = true;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();

                // Silenced now rather than at the next track change: a switch that only takes
                // effect on the next scene looks broken, which is the classic audio-settings bug.
                var player = Player;
                if (player != null) player.ApplySetting();
            }
        }

        /// <summary>Music level, 0..1. Independent of the sound-effect mute on purpose.</summary>
        public static float Volume
        {
            get
            {
                if (!_volumeLoaded)
                {
                    _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
                    _volumeLoaded = true;
                }
                return _volume;
            }
            set
            {
                _volume = Mathf.Clamp01(value);
                _volumeLoaded = true;
                PlayerPrefs.SetFloat(VolumeKey, _volume);
                PlayerPrefs.Save();

                var player = Player;
                if (player != null) player.ApplySetting();
            }
        }

        /// <summary>The track that is playing (or fading in).</summary>
        public static MusicId Current => _target;

        /// <summary>The track that has finished fading in and is simply playing.</summary>
        public static MusicId Settled => _current;

        /// <summary>Human-readable state, for the settings panel and for bug reports.</summary>
        public static string NowPlayingText
        {
            get
            {
                if (!Enabled) return "背景音乐：关";
                if (!string.IsNullOrEmpty(_note)) return "背景音乐：" + _note;

                var track = Get(_target);
                if (track == null) return "背景音乐：还没有开始放";
                return "背景音乐：" + track.Describe();
            }
        }

        /// <summary>True when this track's audio file is actually in the build.</summary>
        public static bool Available(MusicId id)
        {
            var track = Get(id);
            return track != null && Resources.Load<AudioClip>(track.Resource) != null;
        }

        public static MusicTrack Get(MusicId id)
        {
            for (int i = 0; i < Tracks.Length; i++)
            {
                if (Tracks[i].Id == id) return Tracks[i];
            }
            return null;
        }

        /// <summary>
        /// Which track a scene wants.
        ///
        /// The room answers with the cabin's music; the pet's room then asks again with the place
        /// it is actually building (<see cref="PlayForTheme"/>), because "PetRoom" is the same
        /// scene whether the pet lives in a cabin, a garden or on a night terrace.
        /// </summary>
        public static MusicId TrackForScene(string sceneName)
        {
            switch (sceneName)
            {
                case "StartMenu": return MusicId.Menu;
                case "PetRoom": return MusicId.PetRoom;
                case "Main": return MusicId.RunnerForest;
                case "FlyBird": return MusicId.FlyBird;
                case "JumpQuest": return MusicId.JumpQuest;
                case "CatchFruit": return MusicId.CatchFruit;
                case "SliceFruit": return MusicId.SliceFruit;
                case "AngryBirds": return MusicId.AngryBirds;
                default: return MusicId.None;
            }
        }

        /// <summary>
        /// Which track a place wants. The three places the pet can live each have their own,
        /// which is most of what makes moving house feel like moving rather than repainting.
        /// </summary>
        public static MusicId TrackForTheme(object theme)
        {
            // Typed as object so the mobile assembly does not have to reference the pet one:
            // the caller passes its own RoomTheme enum and this reads the name.
            string name = theme == null ? "" : theme.ToString();
            switch (name)
            {
                case "Garden": return MusicId.Garden;
                case "Terrace": return MusicId.Terrace;
                default: return MusicId.PetRoom;
            }
        }

        /// <summary>Asks for a track by name, for the pet's room to call with its own enum.</summary>
        public static void PlayForTheme(object theme) => Play(TrackForTheme(theme));

        public static void PlayForScene(string sceneName) => Play(TrackForScene(sceneName));

        /// <summary>
        /// Asks for a track. Asking for the one already playing does nothing, so callers can be
        /// careless — which they must be, because this gets called from scene loads and from the
        /// room rebuild that follows one.
        /// </summary>
        public static void Play(MusicId id, float fade = FadeSeconds)
        {
            if (id == MusicId.None) return;
            if (!Available(id))
            {
                // Honest rather than silent: a missing file is a build mistake, and the panel
                // should be able to say so instead of showing a track that never plays.
                _note = "缺少音乐文件 " + Get(id).Resource;
                Debug.LogWarning("[DshMobile] " + _note);
                return;
            }

            _note = "";
            _target = id;

            if (!Enabled) return;

            var player = Player;
            if (player != null) player.CrossfadeTo(id, fade);
        }

        /// <summary>Stops the music, e.g. while a silent cutscene plays.</summary>
        public static void Stop(float fade = FadeSeconds)
        {
            _target = MusicId.None;
            var player = Player;
            if (player != null) player.CrossfadeTo(MusicId.None, fade);
        }

        /// <summary>Called by the player component while it fades, and by tests.</summary>
        public static void NoteSettled(MusicId id)
        {
            _current = id;
        }

        // ------------------------------------------------------------------ the player

        private static MobileMusicPlayer _player;

        /// <summary>The live player, created on demand so any scene can start music with one call.</summary>
        private static MobileMusicPlayer Player
        {
            get
            {
                if (_player != null) return _player;
                if (!Application.isPlaying) return null;

                var go = new GameObject("DshMusic");
                Object.DontDestroyOnLoad(go);
                _player = go.AddComponent<MobileMusicPlayer>();
                return _player;
            }
        }

        /// <summary>
        /// Wires music to scene loading.
        ///
        /// One hook rather than a line in every game's Awake: the five mini-games, the runner and
        /// the room all come here, and a new scene gets music by appearing in
        /// <see cref="TrackForScene"/> — the kind of wiring that cannot be forgotten under a
        /// deadline because it is not written anywhere.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoWire()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            PlayForScene(SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            => PlayForScene(scene.name);

        /// <summary>Forgets everything, for tests. Not used by the game.</summary>
        public static void ResetForTests()
        {
            _enabledLoaded = false;
            _volumeLoaded = false;
            _enabled = true;
            _volume = DefaultVolume;
            _current = MusicId.None;
            _target = MusicId.None;
            _note = "";
            PlayerPrefs.DeleteKey(EnabledKey);
            PlayerPrefs.DeleteKey(VolumeKey);
        }
    }

    /// <summary>
    /// The two AudioSources and the crossfade.
    ///
    /// Two sources rather than one, because a single source cannot fade out the old track while
    /// fading in the new one — the "clunk" between scenes is exactly the thing a crossfade exists
    /// to hide, and the room rebuilds itself on every trip home from a mini game.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public class MobileMusicPlayer : MonoBehaviour
    {
        private AudioSource _a;
        private AudioSource _b;

        /// <summary>The source fading the old track out, and the one fading the new track in.</summary>
        private AudioSource _outgoing;
        private AudioSource _incoming;

        private MusicId _to = MusicId.None;
        private float _fade = 1f;
        private float _t = 1f;

        private readonly Dictionary<MusicId, AudioClip> _loaded = new Dictionary<MusicId, AudioClip>();

        private void Awake()
        {
            _a = MakeSource("MusicA");
            _b = MakeSource("MusicB");
        }

        private AudioSource MakeSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        /// <summary>
        /// Applied immediately when the switch or the volume moves.
        ///
        /// Switching back on has to *restart*: the setting being off means the sources were
        /// stopped, so re-enabling and doing nothing would leave the game silent until the next
        /// scene change — the classic "I turned it on and nothing happened" bug.
        /// </summary>
        public void ApplySetting()
        {
            if (!MobileMusic.Enabled)
            {
                StopAll();
                return;
            }

            // Turning back on with nothing targeted — the track was never requested while the
            // switch was off, or the scene came up unmapped — must not leave the switch doing
            // nothing. Re-derive the track from the scene, then fall through to the crossfade.
            if (MobileMusic.Current == MusicId.None)
            {
                MobileMusic.PlayForScene(SceneManager.GetActiveScene().name);
            }

            if (_to != MobileMusic.Current)
            {
                CrossfadeTo(MobileMusic.Current, MobileMusic.FadeSeconds);
                return;
            }

            var incoming = _incoming;
            var outgoing = _outgoing;
            if (incoming != null) incoming.volume = MusicVolume() * _t;
            if (outgoing != null && outgoing.isPlaying) outgoing.volume = MusicVolume() * OutWeight();
        }

        private static float MusicVolume() => MobileMusic.Volume;

        /// <summary>The outgoing track falls away faster than the new one rises, which is what
        /// keeps a crossfade from sounding like two songs at once in the middle of it.</summary>
        private float OutWeight()
        {
            float remaining = 1f - _t;
            return remaining * remaining;
        }

        private AudioSource Active() => _incoming != null ? _incoming : _outgoing;

        /// <summary>The source that is not currently fading in, i.e. the free one.</summary>
        private AudioSource Free() => _incoming == _a ? _b : _a;

        /// <summary>
        /// Fades the track that is playing out and the new one in.
        ///
        /// Roles are held as source references rather than as "the other one", because a switch
        /// that arrives in the middle of another switch has three tracks in play at once: the one
        /// still fading out, the one fading in, and the new one. With roles, the new request simply
        /// takes the free source and promotes the fading-in track to be the thing fading out — the
        /// room does exactly this, since loading PetRoom asks for the cabin's track and the room
        /// then rebuilds itself and asks for the garden's.
        /// </summary>
        public void CrossfadeTo(MusicId id, float fade)
        {
            if (id == _to && _t < 1f) return;

            var outgoing = Active();
            var incoming = Free();

            if (id == MusicId.None)
            {
                incoming = null;   // a pure fade-out: nothing new to play
            }
            else
            {
                var clip = Clip(id);
                if (clip == null) return;

                incoming.clip = clip;
                incoming.volume = 0f;
                incoming.Play();
            }

            if (_outgoing != null && _outgoing != outgoing) _outgoing.Stop();

            _outgoing = outgoing;
            _incoming = incoming;
            _to = id;
            _fade = Mathf.Max(0.05f, fade);
            _t = 0f;
        }

        private void StopAll()
        {
            if (_a != null) _a.Stop();
            if (_b != null) _b.Stop();
            _incoming = null;
            _outgoing = null;
            _to = MusicId.None;
            _t = 1f;
        }

        private void Update()
        {
            if (_a == null || _b == null) return;

            if (!MobileMusic.Enabled)
            {
                StopAll();
                return;
            }

            if (_t < 1f)
            {
                // Unscaled: a mini-game that pauses itself must not leave the music half-faded.
                _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / _fade);

                if (_incoming != null) _incoming.volume = MusicVolume() * _t;
                if (_outgoing != null) _outgoing.volume = MusicVolume() * OutWeight();

                if (_t >= 1f)
                {
                    if (_incoming == null)
                    {
                        StopAll();
                        return;
                    }

                    if (_outgoing != null) _outgoing.Stop();
                    _outgoing = null;
                    MobileMusic.NoteSettled(_to);
                }
            }
            else
            {
                var active = Active();
                if (active != null && active.isPlaying && !Mathf.Approximately(active.volume, MusicVolume()))
                {
                    active.volume = MusicVolume();
                }
            }
        }

        private AudioClip Clip(MusicId id)
        {
            AudioClip cached;
            if (_loaded.TryGetValue(id, out cached) && cached != null) return cached;

            var track = MobileMusic.Get(id);
            if (track == null) return null;

            var clip = Resources.Load<AudioClip>(track.Resource);
            if (clip != null) _loaded[id] = clip;
            return clip;
        }
    }
}
