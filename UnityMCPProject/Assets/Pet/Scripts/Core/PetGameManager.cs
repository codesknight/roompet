using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Ties the pet together: needs, memory, the chosen brain, the avatar and the room.
    /// Everything the HUD renders is read from here.
    ///
    /// Chat flow for one turn:
    ///   1. build a <see cref="PetContext"/> from the CURRENT memory (the new user line is
    ///      deliberately not in it, so the API sees exactly one copy of it),
    ///   2. ask the brain,
    ///   3. on success, commit both turns to memory and act the reply out,
    ///   4. on failure, answer with the offline brain and surface the error instead of
    ///      leaving the player staring at nothing.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PetGameManager : MonoBehaviour
    {
        public static PetGameManager Instance { get; private set; }

        [Header("Wiring")]
        public PetAvatar Avatar;
        public PetController Controller;
        public PetRoom Room;
        public PlayerRoomController Player;
        public RoomCameraRig CameraRig;

        [Header("Pet identity")]
        public string PetName = "小狐狸";
        // Always a private copy: see PetSpecies.Copy for why a reference into All corrupts
        // the species table on the next scene load.
        public PetSpecies Species = PetSpecies.All[0].Copy();

        [Header("Runtime")]
        public readonly PetNeeds Needs = new PetNeeds();
        public readonly PetMemory Memory = new PetMemory();
        public readonly PetJournal Journal = new PetJournal();
        public PetBrainConfig BrainConfig = new PetBrainConfig();

        /// <summary>
        /// This pet's temperament, loaded per species and handed to the needs (decay rates),
        /// the behaviour scorer (what it prefers doing) and the prompt (how it talks about
        /// itself). One object, three consumers, so the pet cannot behave like one character
        /// and describe itself as another.
        /// </summary>
        public PetPersonality Personality { get; private set; } = new PetPersonality();

        /// <summary>Tuning for the proactive/passive behaviour split.</summary>
        public PetBehaviorScheduler Scheduler = new PetBehaviorScheduler();

        public bool IsThinking { get; private set; }
        public string LastError { get; private set; } = "";
        public string LastActionLabel { get; private set; } = "";
        public string LastBehaviorLabel { get; private set; } = "";
        public bool UsingNetworkThisTurn { get; private set; }

        public event Action ChatChanged;

        private IPetBrain _brain;
        private Coroutine _thinkRoutine;
        private float _saveTimer;

        private const string SpeciesKey = "dshpet.species";
        private const string NameKey = "dshpet.petname";
        private const string NeedsKey = "dshpet.needs";
        private const string AffectionKey = "dshpet.affection";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.runInBackground = true;
        }

        private void Start()
        {
            BrainConfig = PetBrainConfig.Load();

            if (GetComponent<PetAudioDirector>() == null) gameObject.AddComponent<PetAudioDirector>();

            string savedSpecies = PlayerPrefs.GetString(SpeciesKey, PetSpecies.All[0].Id);
            Species = PetSpecies.Copy(savedSpecies);
            PetName = PlayerPrefs.GetString(NameKey + "." + Species.Id, Species.DisplayName);
            BindPersonality();
            LoadNeeds();

            BuildRoomAndPet();
            RebuildBrain();
            Memory.Load(Species.Id);
            Journal.Bind(Species.Id);

            if (Memory.Recent.Count == 0)
            {
                Memory.AddPet(Greeting());
            }

            // Only once a day: the room scene is reloaded every time the player comes back
            // from an activity, and an unconditional entry would spam the journal.
            if (!JournalHasToday("今天见到主人了"))
            {
                Journal.Add(MemoryKind.Milestone, "今天见到主人了", "", 0.5f, true);
            }

            PetCollection.Changed += SpawnCompanions;
            SpawnCompanions();
            ChatChanged?.Invoke();
        }

        /// <summary>
        /// Loads the temperament for the current species and points every consumer at it.
        ///
        /// Called on load and on every species switch, because a personality belongs to the
        /// animal: the fox you have known for a week must not become a different fox just
        /// because you looked at the rabbit.
        /// </summary>
        private void BindPersonality()
        {
            Personality = PetPersonality.Load(Species.Id ?? "");
            Needs.Personality = Personality;
        }

        /// <summary>Rolls a new temperament for this species, for when the player wants a
        /// different companion rather than the same one again.</summary>
        public void RerollPersonality()
        {
            int seed = Environment.TickCount ^ (Species.Id != null ? Species.Id.GetHashCode() : 0);
            Personality = PetPersonality.Create(seed);
            Personality.Save(Species.Id ?? "");
            Needs.Personality = Personality;
            Journal.Add(MemoryKind.Milestone, "换了个脾气",
                $"现在的脾气是「{Personality.Archetype}」。", 0.4f);
            ChatChanged?.Invoke();
        }

        private bool JournalHasToday(string title)
        {
            var entries = Journal.ForDay(DateTime.Now);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Title == title) return true;
            }
            return false;
        }

        private void BuildRoomAndPet()
        {
            bool roomAlreadyBuilt = Room != null && Room.transform.Find("Room") != null;

            if (Room == null) Room = GetComponentInChildren<PetRoom>();
            if (Room == null) Room = gameObject.AddComponent<PetRoom>();

            if (roomAlreadyBuilt)
            {
                // The scene was authored in the editor: reuse it, just re-collect the
                // clickable objects (that list is runtime-only and not serialised).
                Room.CollectInteractables();
            }
            else
            {
                Room.Build(false);
            }

            if (Avatar == null) Avatar = GetComponentInChildren<PetAvatar>();
            if (Avatar == null)
            {
                var go = new GameObject("Pet");
                go.transform.SetParent(transform, false);
                Avatar = go.AddComponent<PetAvatar>();
            }
            Avatar.Build(Species, false);
            EnsurePokeTarget(Avatar.gameObject, true);

            if (Controller == null) Controller = Avatar.GetComponent<PetController>();
            if (Controller == null) Controller = Avatar.gameObject.AddComponent<PetController>();
            Controller.Avatar = Avatar;
            Controller.Room = Room;
            Controller.Needs = Needs;

            Controller.Interacted += OnInteracted;
            Controller.Fetched += OnFetched;
            Controller.BladderAccident += OnBladderAccident;
            for (int i = 0; i < Room.Interactables.Count; i++)
            {
                Room.Interactables[i].Clicked += OnInteractableClicked;
            }

            // The pet is wired to the ball here, so a throw can be noticed even though the
            // player character owns the actual throwing.
            var ball = Room.Ball;
            if (ball != null)
            {
                ball.Thrown += OnBallThrown;
                if (Controller != null) Controller.Ball = ball;
            }

            BuildPlayer();
        }

        // ---------------------------------------------------------------- companions

        private readonly List<GameObject> _companions = new List<GameObject>();

        /// <summary>
        /// Puts the rest of the backpack on the floor.
        ///
        /// The primary pet is the one with the brain, the memory and the journal; the others are
        /// companions that wander, use the furniture and can be poked, each with its own species,
        /// temperament and voice. That split is deliberate and it is also the honest one: every
        /// extra pet that talks to the model multiplies the token bill, and the player is the one
        /// paying it. The panel says so out loud.
        /// </summary>
        public void SpawnCompanions()
        {
            DespawnCompanions();

            var companions = PetCollection.Companions();
            int index = 0;

            foreach (var record in companions)
            {
                // Slot one is the primary pet, which already exists.
                index++;
                if (record == null || record.Primary || index == 1) continue;
                if (_companions.Count >= PetCollection.BackpackSlots - 1) break;

                var species = PetSpecies.Copy(record.SpeciesId);
                var go = new GameObject("Companion_" + record.Name);
                go.transform.SetParent(transform, false);

                // Scattered around the middle of the room so they do not stack on spawn.
                float angle = (_companions.Count + 1) * 2.1f;
                go.transform.position = new Vector3(Mathf.Cos(angle) * 3.4f, 0f, Mathf.Sin(angle) * 3.4f);

                var avatar = go.AddComponent<PetAvatar>();
                avatar.Build(species, false);

                var controller = go.AddComponent<PetController>();
                controller.Avatar = avatar;
                controller.Room = Room;
                controller.Needs = new PetNeeds { Personality = record.Personality };
                controller.Ball = Room != null ? Room.Ball : null;

                EnsurePokeTarget(go, false);
                var target = go.GetComponent<PetClickTarget>();
                target.Species = species;
                target.Personality = record.Personality;

                _companions.Add(go);
            }
        }

        public void DespawnCompanions()
        {
            for (int i = 0; i < _companions.Count; i++)
            {
                if (_companions[i] == null) continue;
                if (Application.isPlaying) Destroy(_companions[i]);
                else DestroyImmediate(_companions[i]);
            }
            _companions.Clear();
        }

        /// <summary>How many pets are walking around right now, including the primary one.</summary>
        public int PetsInRoom => 1 + _companions.Count;

        /// <summary>Creates the walkable character and hands the camera a two-subject rig.</summary>
        /// <summary>
        /// Makes a pet clickable.
        ///
        /// The avatar strips the colliders from every primitive it builds, so a pet has no
        /// clickable shape at all by default — which is why clicking it used to do nothing.
        /// A capsule on the root is enough for both platforms: Unity synthesises mouse events
        /// from the primary touch, so a tap on a phone arrives through the same OnMouseDown.
        /// </summary>
        public static void EnsurePokeTarget(GameObject petObject, bool primary)
        {
            if (petObject == null) return;

            var capsule = petObject.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = petObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.42f;
            capsule.height = 1.15f;
            capsule.center = new Vector3(0f, 0.58f, 0f);
            capsule.isTrigger = true;

            var target = petObject.GetComponent<PetClickTarget>();
            if (target == null) target = petObject.AddComponent<PetClickTarget>();
            target.Primary = primary;
        }

        private void BuildPlayer(bool editorMode = false)
        {
            if (Player == null) Player = GetComponentInChildren<PlayerRoomController>();
            if (Player == null)
            {
                var go = new GameObject("PlayerCharacter");
                go.transform.SetParent(transform, false);
                Player = go.AddComponent<PlayerRoomController>();
            }

            var avatar = Player.GetComponentInChildren<PlayerAvatar>();
            if (avatar == null)
            {
                var visual = new GameObject("PlayerVisual");
                visual.transform.SetParent(Player.transform, false);
                avatar = visual.AddComponent<PlayerAvatar>();
            }
            avatar.Build(editorMode);

            Player.Avatar = avatar;
            Player.Room = Room;
            Player.Pet = Controller;
            if (!editorMode) Player.SnapTo(new Vector3(-2.6f, 0f, -3.4f));
            else Player.transform.position = new Vector3(-2.6f, 0f, -3.4f);

            if (Controller != null) Controller.Player = Player.transform;

            var camera = Camera.main;
            if (camera != null)
            {
                CameraRig = camera.GetComponent<RoomCameraRig>();
                if (CameraRig == null) CameraRig = camera.gameObject.AddComponent<RoomCameraRig>();
                CameraRig.Player = Player.transform;
                CameraRig.Pet = Avatar != null ? Avatar.transform : null;
                if (!editorMode) CameraRig.Snap();
            }
        }

        /// <summary>
        /// Authoring entry point used by the editor menu: builds the room and the pet into
        /// the open scene so the hierarchy is real and inspectable. Does not wire events —
        /// Start does that at runtime.
        /// </summary>
        public void EditorBuild()
        {
            string savedSpecies = PlayerPrefs.GetString(SpeciesKey, PetSpecies.All[0].Id);
            Species = PetSpecies.Copy(savedSpecies);

            if (Room == null) Room = GetComponentInChildren<PetRoom>();
            if (Room == null) Room = gameObject.AddComponent<PetRoom>();
            Room.Build(true);

            if (Avatar == null) Avatar = GetComponentInChildren<PetAvatar>();
            if (Avatar == null)
            {
                var go = new GameObject("Pet");
                go.transform.SetParent(transform, false);
                Avatar = go.AddComponent<PetAvatar>();
            }
            Avatar.Build(Species, true);

            if (Controller == null) Controller = Avatar.GetComponent<PetController>();
            if (Controller == null) Controller = Avatar.gameObject.AddComponent<PetController>();
            Controller.Avatar = Avatar;
            Controller.Room = Room;
            Controller.Needs = Needs;
            Controller.Ball = Room.Ball;

            BuildPlayer(true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            PetCollection.Changed -= SpawnCompanions;
            DespawnCompanions();

            if (Controller != null)
            {
                Controller.Interacted -= OnInteracted;
                Controller.Fetched -= OnFetched;
                Controller.BladderAccident -= OnBladderAccident;
            }
            if (Room != null)
            {
                for (int i = 0; i < Room.Interactables.Count; i++)
                {
                    if (Room.Interactables[i] != null) Room.Interactables[i].Clicked -= OnInteractableClicked;
                }
                if (Room.Ball != null) Room.Ball.Thrown -= OnBallThrown;
            }
        }

        private void Update()
        {
            Needs.Tick(Time.deltaTime);
            TickThinkWatchdog();
            TickBehaviors(Time.deltaTime);
            TickNudges(Time.deltaTime);
            TickAutoFetch();
            TickAppearance();

            // Persist every few seconds rather than on every frame.
            _saveTimer += Time.deltaTime;
            if (_saveTimer > 5f)
            {
                _saveTimer = 0f;
                SaveNeeds();
                Journal.Save();
            }
        }

        /// <summary>
        /// Keeps the pet's appearance in step with its state: mud when it is filthy, and the
        /// accident flag cleared once the pet's line about it has been delivered.
        /// </summary>
        private void TickAppearance()
        {
            if (Avatar == null) return;

            // Only the bottom half of the cleanliness bar shows on the model: a pet that
            // started sprouting mud the moment it was less than pristine would look permanently
            // dirty, and the bath would stop feeling like a decision.
            float dirt = Mathf.InverseLerp(0.55f, 0.10f, Needs.Cleanliness);
            Avatar.SetDirtiness(dirt);

            if (Controller != null && Controller.HasAccident)
            {
                _accidentFlagTimer -= Time.deltaTime;
                if (_accidentFlagTimer <= 0f)
                {
                    Controller.ClearAccidentFlag();
                }
            }
        }

        private float _accidentFlagTimer = 6f;

        /// <summary>
        /// Runs the proactive/passive behaviour split. The scheduler only chooses; acting
        /// goes through the controller, which already owns movement and the interactables.
        /// </summary>
        private void TickBehaviors(float dt)
        {
            if (Controller == null || IsThinking) return;

            bool canAct = Controller.IsIdle;
            var context = BuildBehaviorContext();
            var chosen = Scheduler.Tick(dt, context, new List<PetBehavior>(PetBehaviorLibrary.All), canAct);
            if (chosen == null) return;

            LastBehaviorLabel = chosen.Label;
            Controller.RunBehavior(chosen);

            string line = chosen.PickLine(new System.Random());
            if (!string.IsNullOrEmpty(line))
            {
                Memory.AddPet(line);
                Journal.Add(JournalKindFor(chosen), chosen.Label, line, 0.3f);
                ChatChanged?.Invoke();
            }

            if (chosen.SpeakUp)
            {
                NudgeConversation();
            }
        }

        private static MemoryKind JournalKindFor(PetBehavior behavior)
        {
            switch (behavior.Id)
            {
                case "eat":
                case "drink":
                case "groom":
                case "bathe":
                case "use_toilet": return MemoryKind.Care;
                case "play":
                case "fetch": return MemoryKind.Play;
                case "sleep":
                case "nap_night": return MemoryKind.Rest;
                case "seek_attention":
                case "sit_stare":
                case "speak_up": return MemoryKind.Mood;
                default: return MemoryKind.Mood;
            }
        }

        public PetBehaviorContext BuildBehaviorContext()
        {
            var targets = new List<InteractableKind>();
            if (Room != null)
            {
                for (int i = 0; i < Room.Interactables.Count; i++)
                {
                    var item = Room.Interactables[i];
                    if (item != null && item.IsReady && !targets.Contains(item.Kind)) targets.Add(item.Kind);
                }
            }

            return new PetBehaviorContext
            {
                Hunger = Needs.Hunger,
                Energy = Needs.Energy,
                Joy = Needs.Joy,
                Cleanliness = Needs.Cleanliness,
                Bladder = Needs.Bladder,
                Affection = Needs.Affection,
                Mood = Needs.Mood,
                DominantNeed = Needs.DominantNeed,
                HourOfDay = DateTime.Now.Hour + DateTime.Now.Minute / 60f,
                PlayerPresent = true,
                BallLoose = Room != null && Room.Ball != null && Room.Ball.IsLoose,
                MessPresent = Room != null && Room.HasMess,
                AvailableTargets = targets.ToArray(),
                Personality = Personality
            };
        }

        /// <summary>The pet opening a conversation on its own.</summary>
        private void NudgeConversation()
        {
            NudgeConversation(DescribeNudgeReason());
        }

        // --------------------------------------------------- the pet starting a talk

        [Header("Proactive conversation")]
        [Tooltip("How long the pet stays quiet between self-initiated lines.")]
        public float NudgeIntervalMin = 40f;
        public float NudgeIntervalMax = 85f;

        [Tooltip("Seconds of player silence before the pet comes looking for attention.")]
        public float PlayerSilenceThreshold = 45f;

        private float _nudgeTimer = 20f;
        private float _lastPlayerTalkAt;
        private readonly System.Random _nudgeRng = new System.Random();

        /// <summary>Why the pet spoke up last, shown in the HUD.</summary>
        public string LastNudgeReason { get; private set; } = "";

        /// <summary>
        /// The pet starting a conversation on its own timer, with a reason.
        ///
        /// It picks the most pressing thing it could talk about — a need, or the player
        /// having gone quiet — and hands the model that as a stage direction rather than a
        /// bare "say something", which is what makes the line feel motivated.
        /// </summary>
        private void TickNudges(float dt)
        {
            if (IsThinking) return;

            _nudgeTimer -= dt;
            if (_nudgeTimer > 0f) return;

            _nudgeTimer = Mathf.Lerp(NudgeIntervalMin, NudgeIntervalMax, (float)_nudgeRng.NextDouble());

            string reason = DescribeNudgeReason();
            if (string.IsNullOrEmpty(reason)) return;

            NudgeConversation(reason);
        }

        /// <summary>Null when the pet has nothing worth interrupting for.</summary>
        private string DescribeNudgeReason()
        {
            if (Needs.Hunger < 0.30f) return "（你饿了，主动凑过去向主人要吃的）";
            if (Needs.Energy < 0.30f) return "（你很困，想跟主人说一声再去睡）";
            if (Needs.Cleanliness < 0.30f) return "（你觉得自己脏兮兮的，想让主人帮你梳毛）";
            if (Needs.Joy < 0.30f) return "（你有点无聊，想引起主人注意）";

            float silence = Time.realtimeSinceStartup - _lastPlayerTalkAt;
            if (silence > PlayerSilenceThreshold) return "（主人很久没理你了，你凑过去说点什么）";

            if (Needs.Affection > 0.5f && _nudgeRng.NextDouble() < 0.45)
            {
                return "（你心情不错，想主动跟主人分享一件小事）";
            }

            return null;
        }

        /// <summary>Called by the chat layer so silence can be measured.</summary>
        public void NotifyPlayerSpoke() => _lastPlayerTalkAt = Time.realtimeSinceStartup;

        private void NudgeConversation(string reason)
        {
            if (IsThinking || string.IsNullOrEmpty(reason)) return;

            LastNudgeReason = reason;
            var context = BuildContext(reason);
            _pendingUserMessage = "";
            _pendingIsNudge = true;
            _thinkDeadline = Time.realtimeSinceStartup + Mathf.Max(10f, BrainConfig.TimeoutSeconds + 6f);
            _thinkRoutine = StartCoroutine(ThinkRoutine(context, reason));
        }

        /// <summary>
        /// Safety net for the thinking state. An exception thrown inside a coroutine stops
        /// it silently, which used to leave the pet permanently "thinking" (a plain-HTTP
        /// request being rejected by Player Settings did exactly that). If a turn outlives
        /// its budget, fail it and answer locally.
        /// </summary>
        private void TickThinkWatchdog()
        {
            if (!IsThinking) return;
            if (Time.realtimeSinceStartup < _thinkDeadline) return;

            IsThinking = false;
            LastError = "请求超时（可能是网络或 Player Settings 禁止了明文 HTTP）";
            _thinkRoutine = null;

            Memory.AddUser(_pendingUserMessage ?? "");
            Memory.AddPet("唔……我刚才走神了，你再说一次好不好？");
            _pendingUserMessage = null;
            ChatChanged?.Invoke();
        }

        private float _thinkDeadline;
        private string _pendingUserMessage;
        private bool _pendingIsNudge;

        // -------------------------------------------------------------------- brains

        public void RebuildBrain()
        {
            _brain = BrainConfig.CanUseNetwork
                ? (IPetBrain)new DeepSeekPetBrain(BrainConfig)
                : new LocalPetBrain(PetName);
        }

        public string BrainLabel => _brain == null ? "未就绪" : _brain.Name;

        // ---------------------------------------------------------------------- chat

        /// <summary>
        /// One player turn. Named Talk rather than SendMessage so it cannot be confused
        /// with (or accidentally shadow) Unity's Component.SendMessage.
        /// </summary>
        public void Talk(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || IsThinking) return;

            string trimmed = text.Trim();
            LastError = "";
            NotifyPlayerSpoke();

            var context = BuildContext(trimmed);
            _pendingUserMessage = trimmed;
            _thinkDeadline = Time.realtimeSinceStartup + Mathf.Max(10f, BrainConfig.TimeoutSeconds + 6f);
            _thinkRoutine = StartCoroutine(ThinkRoutine(context, trimmed));
        }

        private IEnumerator ThinkRoutine(PetContext context, string userMessage)
        {
            IsThinking = true;
            ChatChanged?.Invoke();

            PetReply reply = default;
            bool got = false;

            IPetBrain brain = _brain ?? new LocalPetBrain(PetName);
            UsingNetworkThisTurn = brain.IsNetwork;

            yield return brain.Think(context, userMessage, r => { reply = r; got = true; });

            if (!got || !reply.IsValid)
            {
                // Network trouble should not end the conversation.
                LastError = string.IsNullOrEmpty(reply.Raw) ? "没有收到回复" : reply.Raw;
                var fallback = new LocalPetBrain(PetName);
                yield return fallback.Think(context, userMessage, r => { reply = r; got = true; });
                UsingNetworkThisTurn = false;
            }

            IsThinking = false;

            if (!got || !reply.IsValid)
            {
                LastError = "本地兜底也失败了";
                ChatChanged?.Invoke();
                yield break;
            }

            // A self-initiated line is not a player turn, so it never enters the transcript
            // as one.
            bool wasNudge = _pendingIsNudge;
            if (!wasNudge) Memory.AddUser(userMessage);
            Memory.AddPet(reply.Speech);
            _pendingUserMessage = null;
            _pendingIsNudge = false;
            Needs.AddAffection(reply.AffectionDelta <= 0f ? 0.02f : reply.AffectionDelta);

            Journal.NoteConversation(wasNudge ? "（它自己凑过来了）" : userMessage, reply.Speech);

            // The model asked us to remember something durable: pin it so pruning never
            // drops it, and surface it back through the prompt on every later turn.
            if (!string.IsNullOrWhiteSpace(reply.MemoryNote))
            {
                Journal.Remember(reply.MemoryNote);
                Debug.Log("[DshPet] remembered: " + reply.MemoryNote);
            }

            // Talking to the pet is itself a little bit of play.
            Needs.Pet(0.03f);

            LastActionLabel = PetUtil.ActionLabel(reply.Action);
            if (Controller != null)
            {
                Controller.LookAt(CameraPosition());
                Controller.ReactTo(reply.Action, ActionDuration(reply.Action));
            }

            // The pet answers out loud, coloured by how it feels right now.
            var audio = PetAudioDirector.Instance;
            if (audio != null) audio.Speak(Needs.Mood);

            SaveNeeds();
            Memory.Save(Species.Id);
            ChatChanged?.Invoke();
        }

        private static float ActionDuration(PetAction action)
        {
            switch (action)
            {
                case PetAction.Sleep: return 12f;
                case PetAction.Eat:
                case PetAction.Drink: return 2.6f;
                case PetAction.Play: return 3f;
                default: return 1.8f;
            }
        }

        private static Vector3 CameraPosition()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : new Vector3(0f, 1.6f, -4f);
        }

        public PetContext BuildContext(string lastInteraction)
        {
            // Durable memory comes from two places: the pinned journal entries (what the
            // model asked us to remember) and the legacy fact list. Merge them, journal
            // first because it is the newer, richer store.
            var facts = new List<string>(Journal.LongTermLines(20));
            foreach (var fact in Memory.FactLines())
            {
                if (!facts.Contains(fact)) facts.Add(fact);
            }

            return new PetContext
            {
                SpeciesId = Species.Id,
                SpeciesName = Species.DisplayName,
                Personality = Species.Personality,
                VoiceStyle = Species.VoiceStyle,
                Temperament = Personality != null ? Personality.PromptLine() : "",
                Archetype = Personality != null ? Personality.Archetype : "",
                PetName = PetName,
                Mood = Needs.Mood,
                Hunger = Needs.Hunger,
                Energy = Needs.Energy,
                Joy = Needs.Joy,
                Cleanliness = Needs.Cleanliness,
                Bladder = Needs.Bladder,
                Affection = Needs.Affection,
                DominantNeed = Needs.DominantNeed,
                History = Memory.HistoryArray(),
                LongTermFacts = facts.ToArray(),
                DayDigest = Journal.RecentDigest(3),
                LastInteraction = lastInteraction,
                ExtraInstructions = BrainConfig != null ? BrainConfig.ExtraInstructions : ""
            };
        }

        /// <summary>Prompt preview, for the debug panel.</summary>
        public string PreviewSystemPrompt() => PetPrompting.BuildSystemPrompt(BuildContext(""));

        // --------------------------------------------------------------- interaction

        private void OnInteractableClicked(Interactable target)
        {
            // The door is aimed at the player, not the pet: it opens the activity chooser
            // instead of sending the pet over to sniff it.
            if (target.Kind == InteractableKind.Door)
            {
                OpenDoorPrompt();
                return;
            }

            // A mess is the player's job, done on the spot: sending the pet over to clean up
            // after itself would take the only consequence out of the accident system.
            if (target.Kind == InteractableKind.Mess)
            {
                CleanMess(target);
                return;
            }

            // A ball that has been thrown is "go get it"; a ball sitting in its corner is
            // just a toy to go play with. Same click, two different verbs.
            if (target.Kind == InteractableKind.Ball && Controller != null && Controller.CanFetch)
            {
                _autoFetchAt = 0f;
                Controller.FetchBall();
                return;
            }

            if (Controller != null) Controller.GoTo(target);
        }

        /// <summary>
        /// Wipes up an accident. The pet notices and is quietly grateful — cleaning up after
        /// someone is a caring act, and the affection system should read it as one.
        /// </summary>
        public void CleanMess(Interactable mess)
        {
            if (mess == null || Room == null) return;

            Room.RemoveMess(mess);
            Needs.Clean(0.12f);
            Needs.AddAffection(0.03f);
            Needs.Pet(0.08f);

            PetAudioDirector.Instance?.Play(SfxId.Brush);
            DshMobile.MobileHaptics.Light();

            Memory.AddPet("（主人把地上的污渍擦干净了，你有点不好意思地蹭了蹭她的腿）");
            Journal.Add(MemoryKind.Care, "主人帮我收拾", "", 0.35f);
            ChatChanged?.Invoke();
        }

        // -------------------------------------------------------------- throw & fetch

        private float _autoFetchAt;

        /// <summary>
        /// The pet notices a throw on its own. It is deliberately not instant: the pause is
        /// the difference between "a dog" and "a servo", and it is longer when the pet is not
        /// in the mood, which is what makes a low mood legible without a UI number.
        /// </summary>
        private void OnBallThrown(PetBall ball)
        {
            float reaction = Mathf.Lerp(0.25f, 0.95f, (float)_nudgeRng.NextDouble());
            if (Needs.Joy < 0.3f && _nudgeRng.NextDouble() < 0.4f) reaction += 2.2f;

            _autoFetchAt = Time.time + reaction;

            Memory.AddPet("（盯着飞出去的球）");
            ChatChanged?.Invoke();
        }

        private void TickAutoFetch()
        {
            if (_autoFetchAt <= 0f || Time.time < _autoFetchAt) return;
            _autoFetchAt = 0f;

            if (Controller == null || !Controller.CanFetch) return;
            Controller.FetchBall();
        }

        /// <summary>The retriever came back. This is the payoff, so it is worth a diary line.</summary>
        private void OnFetched(PetBall ball)
        {
            Memory.AddPet("（把球叼回来，放在你脚边）");
            Journal.Add(MemoryKind.Play, "把球捡回来了", "（把球叼回来，放在你脚边）", 0.4f);
            ChatChanged?.Invoke();
        }

        // ---------------------------------------------------------------- mini games

        /// <summary>True while the "go out and play?" chooser is up.</summary>
        public bool DoorPromptOpen { get; private set; }

        public void OpenDoorPrompt()
        {
            DoorPromptOpen = true;
            PetAudioDirector.Instance?.Play(SfxId.Door);
            ChatChanged?.Invoke();
        }

        public void CloseDoorPrompt()
        {
            DoorPromptOpen = false;
            ChatChanged?.Invoke();
        }

        public List<MiniGameDefinition> AvailableMiniGames()
            => MiniGameLibrary.AvailableFor(Species.Id);

        public void LaunchMiniGame(string id)
        {
            var game = MiniGameLibrary.Get(id);
            if (game == null) return;

            DoorPromptOpen = false;
            Memory.AddPet($"（跑出门去玩「{game.DisplayName}」了）");
            Journal.Add(MemoryKind.Play, "出去玩了「" + game.DisplayName + "」", "", 0.6f);
            FlushToDisk();

            if (!MiniGameLibrary.Launch(game, this))
            {
                LastError = "场景 " + game.SceneName + " 不在 Build Settings 里";
            }
        }

        /// <summary>
        /// Writes every piece of pet state to disk. Called before unloading the room scene,
        /// because that is the only way the pet survives the trip.
        /// </summary>
        public void FlushToDisk()
        {
            SaveNeeds();
            Memory.Save(Species.Id);
            Journal.Save();
        }

        /// <summary>
        /// The player touches the pet.
        ///
        /// The reaction comes from the temperament, the sound from the voice profile and the
        /// line from a local table — so poking the pet is instant feedback that costs nothing.
        /// Routing it through the language model would make the most-repeated action in the
        /// game the most expensive one, and a two-second wait for "（蹭了蹭你的手）" is a worse
        /// answer than the table already has.
        /// </summary>
        public PokeReaction PokePet()
        {
            var reaction = PetInteraction.Choose(BuildBehaviorContext(), (float)_pokeRng.NextDouble());

            LastPokeReaction = reaction;
            LastActionLabel = PetUtil.ActionLabel(PetInteraction.Action(reaction));

            Needs.Pet(PetInteraction.JoyDelta(reaction));
            Needs.AddAffection(PetInteraction.AffectionDelta(reaction));

            string line = PetInteraction.Line(reaction, PetName, Species != null ? Species.Id : "fox");
            Memory.AddPet(line);
            ChatChanged?.Invoke();

            if (Controller != null) Controller.ReactTo(PetInteraction.Action(reaction), 1.6f);

            PetAudioDirector.Instance?.Speak(Species, Personality,
                PetInteraction.MoodFor(reaction, Needs.Mood));

            DshMobile.MobileHaptics.Light();
            return reaction;
        }

        /// <summary>The last poke reaction, for the HUD and the wiring report.</summary>
        public PokeReaction? LastPokeReaction { get; private set; }

        private readonly System.Random _pokeRng = new System.Random();

        /// <summary>
        /// The pet's accident, recorded so the notebook has the whole story.
        ///
        /// Worth a journal entry rather than just a silent stat change: the mess is the
        /// consequence, and a player who comes back to a puddle should be able to see that it
        /// happened at 3pm while they were away, not wonder whether the room is bugged.
        /// </summary>
        private void OnBladderAccident()
        {
            Memory.AddPet("（……没忍住。它低着头，假装在闻地板。）");
            Journal.Add(MemoryKind.Care, "没能忍住", "憋不住在地上了，主人擦干净了。", 0.3f);
            ChatChanged?.Invoke();
        }

        private void OnInteracted(Interactable target)
        {
            string line;
            MemoryKind kind;
            switch (target.Kind)
            {
                case InteractableKind.Food: line = "（埋头吃了起来）"; kind = MemoryKind.Care; break;
                case InteractableKind.Water: line = "（咕嘟咕嘟喝水）"; kind = MemoryKind.Care; break;
                case InteractableKind.Ball: line = "（追着球跑）"; kind = MemoryKind.Play; break;
                case InteractableKind.Brush: line = "（舒服得眯起眼）"; kind = MemoryKind.Care; break;
                case InteractableKind.Bed: line = "（爬上小床蜷起来）"; kind = MemoryKind.Rest; break;
                default: line = "（开心地晃了晃）"; kind = MemoryKind.Mood; break;
            }

            Memory.AddPet(line);
            Journal.Add(kind, InteractLabel(target.Kind), line, 0.3f);
            PlayInteractionSound(target.Kind);
            ChatChanged?.Invoke();
        }

        private static void PlayInteractionSound(InteractableKind kind)
        {
            var audio = PetAudioDirector.Instance;
            if (audio == null) return;

            switch (kind)
            {
                case InteractableKind.Food: audio.Play(SfxId.Eat); break;
                case InteractableKind.Water: audio.Play(SfxId.Drink); break;
                case InteractableKind.Ball: audio.Play(SfxId.BallBounce); break;
                case InteractableKind.Brush: audio.Play(SfxId.Brush); break;
                case InteractableKind.Bed: audio.Play(SfxId.Sleepy); break;
                case InteractableKind.Door: audio.Play(SfxId.Door); break;
                default: audio.Play(SfxId.Wag); break;
            }
        }

        private static string InteractLabel(InteractableKind kind)
        {
            switch (kind)
            {
                case InteractableKind.Food: return "吃了东西";
                case InteractableKind.Water: return "喝了水";
                case InteractableKind.Ball: return "玩了球";
                case InteractableKind.Brush: return "被梳了毛";
                case InteractableKind.Bed: return "去睡觉了";
                default: return "互动了一下";
            }
        }

        public void QuickAction(string kind)
        {
            switch (kind)
            {
                case "pet":
                    Needs.Pet(0.18f);
                    Controller?.ReactTo(PetAction.Wag, 2f);
                    Memory.AddUser("（摸了摸它）");
                    Memory.AddPet("呼噜呼噜……");
                    break;
                case "feed":
                    var food = FindKind(InteractableKind.Food);
                    if (food != null) Controller?.GoTo(food);
                    return;
                case "play":
                    if (Controller != null && Controller.CanFetch)
                    {
                        _autoFetchAt = 0f;
                        Controller.FetchBall();
                        return;
                    }
                    var ballItem = FindKind(InteractableKind.Ball);
                    if (ballItem != null) Controller?.GoTo(ballItem);
                    return;
            }

            ChatChanged?.Invoke();
        }

        private Interactable FindKind(InteractableKind kind)
        {
            if (Room == null) return null;
            for (int i = 0; i < Room.Interactables.Count; i++)
            {
                if (Room.Interactables[i] != null && Room.Interactables[i].Kind == kind) return Room.Interactables[i];
            }
            return null;
        }

        // ---------------------------------------------------------------- swapping

        public void SwitchSpecies(int index)
        {
            var next = PetSpecies.Get(index);
            if (next.Id == Species.Id) return;

            Memory.Save(Species.Id);
            Species = next.Copy();
            PetName = PlayerPrefs.GetString(NameKey + "." + Species.Id, Species.DisplayName);

            PlayerPrefs.SetString(SpeciesKey, Species.Id);
            PlayerPrefs.Save();

            LoadNeeds();
            BindPersonality();
            Avatar.Build(Species, false);
            Memory.Load(Species.Id);
            Journal.Bind(Species.Id);
            Scheduler.Reset();
            RebuildBrain();

            if (Memory.Recent.Count == 0) Memory.AddPet(Greeting());

            Controller.SnapTo(Vector3.zero);
            ChatChanged?.Invoke();
        }

        /// <summary>Longest name the pet will accept. Long names unbalance the prompt line.</summary>
        public const int MaxPetNameLength = 12;

        /// <summary>
        /// Names the pet, and makes the naming something the pet remembers.
        ///
        /// The name reaches the model through <see cref="PetContext.PetName"/>, which the prompt
        /// opens with ("你是一只名叫「X」的…"). Recording it as a long-term fact as well is what
        /// makes the pet able to refer to itself by the new name in later conversations and in
        /// lines it starts on its own — a name that only lives in one prompt line tends to get
        /// forgotten the moment the model is asked about anything else.
        /// </summary>
        /// <returns>True when the name actually changed.</returns>
        public bool RenamePet(string name)
        {
            string cleaned = PetUtil.SanitizeName(name, Species.DisplayName);
            if (cleaned == PetName) return false;

            string previous = PetName;
            PetName = cleaned;

            PlayerPrefs.SetString(NameKey + "." + Species.Id, PetName);
            PlayerPrefs.Save();

            Memory.Remember("我的名字是" + PetName + "，是主人给我起的。");
            Memory.AddPet(previous == Species.DisplayName
                ? "（主人给我起了名字：" + PetName + "）"
                : "（主人把我的名字改成了" + PetName + "）");
            Journal.Add(MemoryKind.Milestone,
                previous == Species.DisplayName ? "有了名字：" + PetName : "改名了：" + PetName,
                "主人叫我「" + PetName + "」。", 0.9f, true);

            // The greeting and the local brain both bake the name in, so rebuild them.
            RebuildBrain();

            LastActionLabel = "被叫了名字";
            ChatChanged?.Invoke();
            return true;
        }

        private string Greeting()
        {
            string need = Needs.DominantNeed;
            if (!string.IsNullOrEmpty(need)) return $"（{PetUtil.MoodLabel(Needs.Mood)}）唔……你来了。";
            return Species.Id == "cat" ? "……你来啦。哼。" : "你来啦！我等你好久了。";
        }

        public void ResetPet()
        {
            Memory.Clear();
            Memory.Save(Species.Id);
            Needs.Hunger = 0.85f;
            Needs.Energy = 0.85f;
            Needs.Joy = 0.7f;
            Needs.Cleanliness = 0.9f;
            Needs.Affection = 0.1f;
            SaveNeeds();
            Memory.AddPet(Greeting());
            ChatChanged?.Invoke();
        }

        // ---------------------------------------------------------------- storage

        private void SaveNeeds()
        {
            PlayerPrefs.SetString(NeedsKey + "." + Species.Id, JsonUtility.ToJson(Needs.Snapshot()));
            PlayerPrefs.Save();
        }

        private void LoadNeeds()
        {
            string json = PlayerPrefs.GetString(NeedsKey + "." + Species.Id, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var status = JsonUtility.FromJson<PetStatus>(json);
                Needs.CopyFrom(status);
            }
            catch
            {
                // Corrupt save: keep the defaults rather than blocking the scene.
            }
        }

        private void OnApplicationQuit()
        {
            SaveNeeds();
            Memory.Save(Species.Id);
            Journal.Save();
        }
    }
}
