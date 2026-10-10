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

            // The room is the hub, and the hub must never open frozen. The forest run pauses by
            // setting Time.timeScale to 0 and that value is global: a player who paused and then
            // went home used to arrive in a room where nothing moved and no button worked, with no
            // error to explain it. The clock is restored on arrival as well as on departure, because
            // "the scene I came from forgot" is not something this scene can rule out.
            DshMobile.SceneClock.Restore("the pet room loaded");
        }

        private void Start()
        {
            BrainConfig = PetBrainConfig.Load();

            if (GetComponent<PetAudioDirector>() == null) gameObject.AddComponent<PetAudioDirector>();

            string savedSpecies = PlayerPrefs.GetString(SpeciesKey, PetSpecies.All[0].Id);
            Species = PetSpecies.Copy(savedSpecies);

            // With a collection, the pet that owns the room is the primary *record*: its name and
            // its temperament come from the record, and its save lives under the record's id.
            var primary = PetCollection.Primary;
            if (primary != null && !string.IsNullOrEmpty(primary.SpeciesId))
            {
                Species = PetSpecies.Copy(primary.SpeciesId);
                PetName = primary.Name;
            }
            else
            {
                PetName = PlayerPrefs.GetString(NameKey + "." + PetKey, Species.DisplayName);
            }

            BindPersonality();
            LoadNeeds();

            if (Room != null) Room.Theme = PetWorldMap.Current;
            BuildRoomAndPet();
            ApplyThemeToNeeds();
            RebuildBrain();
            Memory.Load(PetKey);
            Journal.Bind(PetKey);

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
            // The individual first: a pet in the collection owns its own temperament, so the cat
            // you have been looking after for a week does not become a different cat because you
            // switched to the other one and back.
            var primary = PetCollection.Primary;
            if (primary != null && !string.IsNullOrEmpty(primary.Id) && primary.SpeciesId == Species.Id)
            {
                Personality = primary.Personality;
            }
            else
            {
                Personality = PetPersonality.Load(PetKey);
            }

            Needs.Personality = Personality;
        }

        /// <summary>Rolls a new temperament for this pet, for when the player wants a different
        /// companion rather than the same one again.</summary>
        public void RerollPersonality()
        {
            int seed = Environment.TickCount ^ (Species.Id != null ? Species.Id.GetHashCode() : 0);
            Personality = PetPersonality.Create(seed);
            Personality.Save(PetKey);

            // Written back to the record as well: the panel reads the record, so a reroll that only
            // lived in PlayerPrefs would look like it did nothing.
            var primary = PetCollection.Primary;
            if (primary != null && !string.IsNullOrEmpty(primary.Id))
            {
                primary.SetPersonality(Personality);
                PetCollection.SaveNow();
            }

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
            if (Room == null) Room = GetComponentInChildren<PetRoom>();
            if (Room == null) Room = gameObject.AddComponent<PetRoom>();

            // The room is a function of BOTH the place and the furniture the player owns, and the
            // furniture is changed from the shop/warehouse, outside the room. A scene snapshot can
            // therefore be stale even when the place is unchanged, so the room is rebuilt on every
            // load — it is a few dozen primitives, and "the room matches the save" is the thing
            // that actually matters. (The old shortcut reused the authored scene when the theme
            // matched, which is precisely what would show a returning player's bought bed as absent.)
            Room.Theme = PetWorldMap.Current;
            Room.Build(false);

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

        // ---------------------------------------------------------------- world map

        /// <summary>
        /// Moves the household to another place.
        ///
        /// A rebuild rather than a scene load: the room is generated, so "another scene" is a
        /// palette plus a few props, and the pet's needs, memory and the collection all live
        /// outside the scene and survive untouched. It also means there is no scene-transition
        /// state to lose — the bug this project already has a habit of producing.
        /// </summary>
        public bool TravelTo(RoomTheme theme, out string message)
        {
            if (!PetWorldMap.TravelTo(theme, out message)) return false;

            var info = RoomThemeInfo.Get(theme);
            if (Room != null)
            {
                Room.Theme = theme;
                Room.Build(false);
            }

            RebindRoom();
            RecentrePet();
            SpawnCompanions();

            Journal.Add(MemoryKind.Milestone, "搬到了新地方",
                info.DisplayName + "　" + info.Effects(), 0.5f);
            ChatChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Re-subscribes to everything in the room after it has been rebuilt.
        ///
        /// Rebuilding throws away every GameObject in the room, so every click handler the
        /// manager had registered is now pointing at a destroyed object. Forgetting this is
        /// exactly how "the bowls stopped working after I moved" happens.
        /// </summary>
        /// <summary>
        /// Rebuilds the room in place, for when the furniture changes (bought, sold, placed,
        /// stored) but the place does not. The pet and its companions are re-anchored afterwards.
        /// </summary>
        public void RebuildRoom()
        {
            if (Room != null)
            {
                Room.Theme = PetWorldMap.Current;
                Room.Build(false);
            }

            RebindRoom();
            RecentrePet();
            SpawnCompanions();
            ChatChanged?.Invoke();
        }

        private void RebindRoom()
        {
            if (Room == null) return;

            for (int i = 0; i < Room.Interactables.Count; i++)
            {
                var item = Room.Interactables[i];
                if (item == null) continue;
                item.Clicked -= OnInteractableClicked;
                item.Clicked += OnInteractableClicked;
            }

            if (Controller != null)
            {
                Controller.Room = Room;
                Controller.Ball = Room.Ball;
            }

            var ball = Room.Ball;
            if (ball != null)
            {
                ball.Thrown -= OnBallThrown;
                ball.Thrown += OnBallThrown;
                if (Controller != null) Controller.Ball = ball;
            }
        }

        /// <summary>Puts the pet and the player back in the middle, after a rebuild.</summary>
        public void RecentrePet()
        {
            if (Controller != null) Controller.SnapTo(Vector3.zero);
            if (Player != null) Player.SnapTo(new Vector3(2.4f, 0f, 2.4f));
            if (CameraRig != null) CameraRig.Snap();
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

            // The pet that owns the room is "the one with the brain", and that is now whichever
            // record the player selected — not "whoever happens to be first in the backpack". The
            // old version skipped slot one, which was the same thing while the primary could only
            // ever be the original pet; the moment a hand-over could choose the second cat, the
            // first one silently stopped being instantiated at all.
            var primary = PetCollection.Primary;
            string primaryId = primary != null ? primary.Id : "";

            foreach (var record in companions)
            {
                if (record == null) continue;
                if (record.Id == primaryId || record.Primary) continue;
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

                EnsurePokeTarget(go, false, record.Id);
                var target = go.GetComponent<PetClickTarget>();
                target.Species = species;
                target.Personality = record.Personality;

                _companions.Add(go);
            }
            // The pet that was selected may no longer exist, or may be a different animal now.
            ResetSelection();
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

        /// <summary>
        /// The key this pet's save lives under.
        ///
        /// A pet's save used to be keyed by its *species*, which was fine while a species was an
        /// individual. The moment the collection could hold two cats, it stopped being fine: they
        /// shared a name, a pantry and a diary, and switching between them did nothing. Now the key
        /// is the collection record when there is one, and the species only for the bare case where
        /// no collection exists yet.
        /// </summary>
        public string PetKey
        {
            get
            {
                var primary = PetCollection.Primary;
                if (primary != null && !string.IsNullOrEmpty(primary.Id)) return primary.Id;
                return Species != null ? Species.Id : "fox";
            }
        }

        /// <summary>
        /// Tells the HUD the transcript changed, for the few places the UI itself writes to it —
        /// the puzzle paying out, for instance.
        /// </summary>
        public void AnnounceChat() => ChatChanged?.Invoke();

        /// <summary>
        /// Everything one status card needs to know about one pet in the room.
        ///
        /// A struct returned by value rather than a live reference to the manager's own fields:
        /// the HUD draws a card per pet per frame, and a companion's needs live on its own
        /// controller, so "which pet is this card about" has to be answerable without the HUD
        /// knowing how companions are stored.
        /// </summary>
        public struct PetCard
        {
            public bool Primary;
            public string Name;
            public PetSpecies Species;
            public PetPersonality Personality;
            public PetNeeds Needs;
            public PetController Controller;

            /// <summary>Which collection record this pet is, or empty for a pet with no record.</summary>
            public string RecordId;
        }

        /// <summary>
        /// One card per pet currently in the room, primary first.
        ///
        /// Companions are read from the scene objects rather than from the collection records,
        /// because the card is a *status* card: it has to show the needs the controller is
        /// actually acting on, which are the ones that decay and drive behaviour.
        /// </summary>
        public List<PetCard> Cards()
        {
            var cards = new List<PetCard>
            {
                new PetCard
                {
                    Primary = true,
                    Name = PetName,
                    Species = Species,
                    Personality = Personality,
                    Needs = Needs,
                    Controller = Controller,
                    RecordId = PetCollection.Primary != null ? PetCollection.Primary.Id : ""
                }
            };

            for (int i = 0; i < _companions.Count; i++)
            {
                var go = _companions[i];
                if (go == null) continue;

                var controller = go.GetComponent<PetController>();
                var avatar = go.GetComponent<PetAvatar>();
                var target = go.GetComponent<PetClickTarget>();
                var needs = controller != null ? controller.Needs : null;

                cards.Add(new PetCard
                {
                    Primary = false,
                    Name = go.name.StartsWith("Companion_")
                        ? go.name.Substring("Companion_".Length)
                        : go.name,
                    Species = avatar != null ? avatar.Species : Species,
                    Personality = needs != null && needs.Personality != null
                        ? needs.Personality
                        : (target != null ? target.Personality : Personality),
                    Needs = needs,
                    Controller = controller,
                    RecordId = target != null ? target.RecordId : ""
                });
            }

            return cards;
        }

        /// <summary>Which pet the status card is showing. 0 is the primary pet.</summary>
        public int SelectedPetIndex { get; private set; }

        /// <summary>Whether the status card shows its detail or just the row of pets.</summary>
        public bool CardExpanded { get; private set; } = true;

        /// <summary>
        /// Points the status card at one pet, and opens it.
        ///
        /// Called from two places with the same meaning: tapping a pet in the room, and tapping
        /// its chip in the card. Tapping the chip of the pet already selected toggles the card
        /// open and shut, which is the only way to get the room back on a small screen without
        /// hiding the pets themselves.
        /// </summary>
        public void SelectPet(int index, bool toggleIfSame = false)
        {
            int count = PetsInRoom;
            if (index < 0 || index >= count) index = 0;

            if (toggleIfSame && index == SelectedPetIndex)
            {
                CardExpanded = !CardExpanded;
            }
            else
            {
                RequestPrimary(index);
                CardExpanded = true;
            }

            ChatChanged?.Invoke();
        }

        /// <summary>
        /// Tapping a pet asks for it to become the one being looked after.
        ///
        /// Asking rather than doing, because the switch is a hand-over that rebuilds the room: it
        /// has to happen once, after the input pass, and not in the middle of drawing the card that
        /// asked for it. <see cref="Update"/> carries it out on the next frame.
        /// </summary>
        private void RequestPrimary(int cardIndex)
        {
            if (cardIndex <= 0)
            {
                SelectedPetIndex = 0;
                _pendingPrimary = null;
                return;
            }

            var cards = Cards();
            if (cardIndex >= cards.Count) return;

            string id = cards[cardIndex].RecordId;
            if (string.IsNullOrEmpty(id))
            {
                SelectedPetIndex = cardIndex;
                return;
            }

            var primary = PetCollection.Primary;
            if (primary != null && primary.Id == id)
            {
                SelectedPetIndex = cardIndex;
                return;
            }

            _pendingPrimary = id;
        }

        private string _pendingPrimary;

        /// <summary>Forgets which pet was selected, after the room changes under it.</summary>
        public void ResetSelection()
        {
            SelectedPetIndex = 0;
            CardExpanded = true;
        }

        /// <summary>
        /// Makes one of the collection's pets the one being looked after.
        ///
        /// This is what "点谁就切到谁" means in practice, and it is a real hand-over rather than a
        /// highlight: the chosen pet gets the brain, the memory, the diary, its own pantry and its
        /// own name, and the pet that had them becomes a companion. Which is why it flushes the
        /// outgoing pet's state first — a swap that forgets to flush is how a conversation
        /// disappears.
        /// </summary>
        public bool MakePrimary(string recordId)
        {
            var record = PetCollection.Find(recordId);
            if (record == null) return false;

            var current = PetCollection.Primary;
            if (current != null && current.Id == recordId) return false;

            // Flush before anything moves: memory and needs belong to the pet that is leaving.
            SaveNeeds();
            Memory.Save(PetKey);
            Journal.Save();

            PetCollection.SetPrimary(recordId);

            Species = PetSpecies.Copy(record.SpeciesId);
            PetName = record.Name;
            PlayerPrefs.SetString(SpeciesKey, Species.Id);
            PlayerPrefs.Save();

            BindPersonality();
            LoadNeeds();
            Avatar.Build(Species, false);
            EnsurePokeTarget(Avatar.gameObject, true);
            Memory.Load(PetKey);
            Journal.Bind(PetKey);
            Scheduler.Reset();

            if (Memory.Recent.Count == 0) Memory.AddPet(Greeting());

            Journal.Add(MemoryKind.Milestone, record.Name + "成了主要照顾的宠物",
                "现在陪着你的换成了它。", 0.4f);

            // The room is rebuilt around the new pet: the one that had the brain becomes a companion.
            ResetSelection();
            SpawnCompanions();
            RebuildBrain();
            RecentrePet();

            Memory.AddSystem($"（现在主要照顾的是{record.Name}，它的记忆和记事本会跟着它）");
            ChatChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Two pets in the room, alone with each other, occasionally produce a kitten.
        ///
        /// Local and free — the point of the feature is that a *new pet* can appear without the
        /// shop, made out of the two the player already has. The rules (chance, cooldown, the blend
        /// of temperaments) live in <see cref="PetBreeding"/> and are pure; this only applies the
        /// answer.
        /// </summary>
        private void TryBreeding()
        {
            var records = PetCollection.Companions();
            if (records.Count < 2) return;

            var first = records[0];
            var second = records[1];
            if (first == null || second == null || first.Id == second.Id) return;

            float roll = (float)_chatterRng.NextDouble();
            float chance;
            bool litter = PetBreeding.TryBreed(first, second, Needs.Affection,
                PetBreeding.MinutesSinceLastLitter(), roll, out chance);

            LastBreedingChance = chance;
            if (!litter) return;

            var child = PetBreeding.Child(first, second,
                Environment.TickCount ^ first.Id.GetHashCode() ^ second.Id.GetHashCode());
            if (child == null) return;

            PetBreeding.MarkLitter();
            PetCollection.AddBorn(child);

            Memory.AddPet(PetBreeding.Announcement(first, second, child));
            Memory.AddSystem($"（{child.Name}在仓库里等着你，去「宠物」面板就能让它进屋）");
            Journal.Add(MemoryKind.Milestone, "家里多了一只小的",
                $"{first.Name}和{second.Name}的宝宝：{child.Name}（{child.Personality.Archetype}）", 0.7f);
            PetAudioDirector.Instance?.Play(SfxId.Happy);
            DshMobile.MobileHaptics.Medium();
            ChatChanged?.Invoke();
        }

        /// <summary>The last breeding chance that was rolled, for the wiring report.</summary>
        public float LastBreedingChance { get; private set; }

        /// <summary>
        /// Points the card at whichever pet was clicked in the room.
        ///
        /// Clicking a pet is the gesture the player already makes to touch it, so it is also the
        /// one that has to answer "which of these three is this card about" — otherwise the card
        /// shows one animal's numbers while the player is looking at another.
        /// </summary>
        public void SelectPetObject(GameObject petObject)
        {
            if (petObject == null) return;

            if (Avatar != null && petObject == Avatar.gameObject)
            {
                SelectPet(0);
                return;
            }

            for (int i = 0; i < _companions.Count; i++)
            {
                if (_companions[i] == petObject)
                {
                    SelectPet(i + 1);
                    return;
                }
            }
        }

        /// <summary>
        /// Ticks the companions' needs.
        ///
        /// The manager ticks the primary pet's needs, and companions used to get none at all —
        /// their bars sat at their starting values forever, which nobody noticed while they were
        /// decorative and is obvious the moment each pet has a status card of its own. They are
        /// ticked with the same place modifiers, so a companion in the garden gets dirty just as
        /// fast as the pet you are looking after.
        /// </summary>
        private void TickCompanionNeeds(float dt)
        {
            if (_companions.Count == 0) return;

            var info = RoomThemeInfo.Get(Room != null ? Room.Theme : PetWorldMap.Current);
            for (int i = 0; i < _companions.Count; i++)
            {
                var go = _companions[i];
                if (go == null) continue;

                var controller = go.GetComponent<PetController>();
                var needs = controller != null ? controller.Needs : null;
                if (needs == null) continue;

                needs.JoyDrainScale = info.JoyDrainScale;
                needs.EnergyDrainScale = info.EnergyDrainScale;
                needs.CleanDrainScale = info.CleanDrainScale;
                needs.Tick(dt);
            }
        }

        /// <summary>Creates the walkable character and hands the camera a two-subject rig.</summary>
        /// <summary>
        /// Makes a pet clickable.
        ///
        /// The avatar strips the colliders from every primitive it builds, so a pet has no
        /// clickable shape at all by default — which is why clicking it used to do nothing.
        /// A capsule on the root is enough for both platforms: Unity synthesises mouse events
        /// from the primary touch, so a tap on a phone arrives through the same OnMouseDown.
        /// </summary>
        public static void EnsurePokeTarget(GameObject petObject, bool primary,
            string recordId = null)
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
            target.RecordId = recordId;
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

            // Author the scene in the place the save says the pet lives, so the snapshot and the
            // save agree and nothing has to be rebuilt on the first frame.
            Room.Theme = PetWorldMap.Current;
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

            // The room is going away (usually because the player left for a mini game), and a
            // synthetic voice still reading the last reply over the loading screen is the kind of
            // bug that makes a feature feel broken.
            DshMobile.MobileTts.Stop();
            DshMobile.MobileStt.Cancel();

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

        /// <summary>
        /// Hands the current place's modifiers to the needs.
        ///
        /// Pushed every frame rather than applied once at load, because the pet can move between
        /// places without the scene reloading — a place that only affected the pet after a
        /// restart would look like a decoration.
        /// </summary>
        private void ApplyThemeToNeeds()
        {
            var info = RoomThemeInfo.Get(Room != null ? Room.Theme : PetWorldMap.Current);
            Needs.JoyDrainScale = info.JoyDrainScale;
            Needs.EnergyDrainScale = info.EnergyDrainScale;
            Needs.CleanDrainScale = info.CleanDrainScale;
            Needs.Personality = Personality;
        }

        // ------------------------------------------------------------- pet chatter

        private float _chatterTimer = 18f;
        private readonly System.Random _chatterRng = new System.Random();

        /// <summary>
        /// Two pets in the room noticing each other.
        ///
        /// Deliberately local and free. The player pays for conversations with their own pet;
        /// two animals sniffing each other every half minute should not be a line item, and the
        /// stage-direction style keeps them reading as animals rather than as chatbots.
        /// </summary>
        private void TickChatter(float dt)
        {
            if (_companions.Count == 0) return;

            _chatterTimer -= dt;
            if (_chatterTimer > 0f) return;

            var records = PetCollection.Companions();
            if (records.Count < 2)
            {
                _chatterTimer = 30f;
                return;
            }

            // Find a pair that is actually near each other.
            for (int i = 0; i < _companions.Count; i++)
            {
                if (_companions[i] == null) continue;

                var first = _companions[i].transform;
                string aName = _companions[i].name.Replace("Companion_", "");
                var second = FindNearbyCompanion(first, out var otherName);
                if (second == null && Avatar != null)
                {
                    // The primary pet can be the other half of the pair.
                    float toPet = Vector3.Distance(Avatar.transform.position, first.position);
                    if (toPet <= PetChatter.NoticeRange) second = Avatar.transform;
                }

                if (second == null) continue;

                // Names come from the scene and temperaments from the records, matched by name:
                // the companion list and the record list are not guaranteed to be in the same
                // order, and an exchange that mixes up who said what is worse than no exchange.
                string bName = otherName ?? PetName;
                var a = PersonalityOf(records, aName, Personality);
                var b = PersonalityOf(records, bName, Personality);
                var exchange = PetChatter.Choose(a, b, (float)_chatterRng.NextDouble());

                var lines = PetChatter.Lines(exchange, aName, bName);

                for (int line = 0; line < lines.Length; line++)
                {
                    if (!string.IsNullOrEmpty(lines[line])) Memory.AddPet(lines[line]);
                }

                float joy = PetChatter.JoyDelta(exchange);
                Needs.Pet(joy);
                Journal.Add(MemoryKind.Mood, PetChatter.JournalTitle(exchange, aName, bName), "", 0.3f);
                ChatChanged?.Invoke();

                LastExchange = exchange;
                PetAudioDirector.Instance?.Speak(Species, Personality,
                    joy >= 0f ? PetMood.Happy : PetMood.Lonely);

                // An exchange that went well is also the only moment a kitten can happen: two pets
                // that like each other, alone in a room, on a cooldown. Nothing here costs a token.
                if (exchange == PetChatter.Exchange.Cuddle || exchange == PetChatter.Exchange.Play)
                {
                    TryBreeding();
                }

                break;
            }

            float liveliness = Personality != null ? Personality.Liveliness : 0.5f;
            _chatterTimer = PetChatter.NextDelay(_chatterRng, liveliness);
        }

        /// <summary>The last exchange between two pets, for the HUD and the wiring report.</summary>
        public PetChatter.Exchange? LastExchange { get; private set; }

        /// <summary>Temperament of one pet by name, falling back to the primary pet's.</summary>
        private static PetPersonality PersonalityOf(List<PetRecord> records, string name,
            PetPersonality fallback)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] != null && records[i].Name == name) return records[i].Personality;
            }
            return fallback ?? new PetPersonality();
        }

        private Transform FindNearbyCompanion(Transform from, out string name)
        {
            name = null;
            for (int i = 0; i < _companions.Count; i++)
            {
                var candidate = _companions[i];
                if (candidate == null || candidate.transform == from) continue;
                if (Vector3.Distance(candidate.transform.position, from.position) > PetChatter.NoticeRange) continue;

                name = candidate.name.Replace("Companion_", "");
                return candidate.transform;
            }
            return null;
        }

        private void Update()
        {
            // A pending hand-over runs here rather than in the click handler: the switch rebuilds
            // the room, and doing that from inside the input pass that asked for it is how a UI
            // ends up drawing a card for an animal that no longer exists.
            if (!string.IsNullOrEmpty(_pendingPrimary))
            {
                string wanted = _pendingPrimary;
                _pendingPrimary = null;
                MakePrimary(wanted);
            }

            ApplyThemeToNeeds();
            Needs.Tick(Time.deltaTime);
            TickCompanionNeeds(Time.deltaTime);
            TickThinkWatchdog();
            TickBehaviors(Time.deltaTime);
            TickNudges(Time.deltaTime);
            TickAutoFetch();
            TickChatter(Time.deltaTime);
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
                FoodAvailable = PetInventory.HasFood,
                AvailableTargets = targets.ToArray(),
                Personality = Personality,

                // Read from the map rather than from the room's serialized theme: the theme on a
                // restored scene object is whatever it was built for, and the save is the truth.
                Place = PetWorldMap.Current.ToString()
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

            // An order is understood and carried out here, on the device, in this frame.
            if (TakeOrder(trimmed)) return;

            var context = BuildContext(trimmed);
            _pendingUserMessage = trimmed;
            _thinkDeadline = Time.realtimeSinceStartup + Mathf.Max(10f, BrainConfig.TimeoutSeconds + 6f);
            _thinkRoutine = StartCoroutine(ThinkRoutine(context, trimmed));
        }

        /// <summary>
        /// Handles 「拿球」「去吃饭」「过来」「别动」「饭碗在哪」 without asking anyone.
        ///
        /// Two reasons this is local rather than a prompt instruction. The honest one: the pet's
        /// spatial knowledge is already in this process, so a round trip can only make the answer
        /// slower and less accurate. The practical one: a model asked to chat *and* to fire game
        /// actions will eventually answer "好的，我这就去！" and not go anywhere — and the player cannot
        /// tell the difference between a lazy pet and a broken feature.
        ///
        /// Returns true when the message was an order, in which case it never reaches the brain.
        /// </summary>
        private bool TakeOrder(string text)
        {
            var order = PetCommands.Parse(text);
            if (!order.IsOrder) return false;

            bool done = ExecuteOrder(order);

            string line;
            if (order.Kind == PetOrderKind.AskWhere) line = DescribeWhere(order);
            else if (done) line = PetCommands.Reply(order, Environment.TickCount / 1000);
            else line = PetCommands.CannotDo(order);

            if (string.IsNullOrEmpty(line)) line = "……";

            var action = PetCommands.Action(order);

            Memory.AddUser(text);
            Memory.AddPet(line);
            LastActionLabel = PetUtil.ActionLabel(action);
            Journal.NoteConversation(text, line);

            // Talking to the pet is itself a little bit of play, and doing as it was told even more so.
            Needs.Pet(0.04f);
            Needs.AddAffection(done ? 0.04f : 0.01f);

            if (Controller != null)
            {
                Controller.LookAt(CameraPosition());

                // The scripted reaction is skipped for orders that are already moving the pet: a
                // reaction would cancel the walk that the order just started.
                if (order.Kind != PetOrderKind.AskWhere && !IsMovingOrder(order.Kind))
                {
                    Controller.ReactTo(action, ActionDuration(action));
                }
                else if (order.Kind == PetOrderKind.AskWhere)
                {
                    Controller.ReactTo(PetAction.Curious, 1.6f);
                }
            }

            var audio = PetAudioDirector.Instance;
            if (audio != null) audio.Speak(Needs.Mood);

            float pitch, rate;
            PetVoice.SpeechParams(Species, Personality, out pitch, out rate);
            DshMobile.MobileTts.Speak(line, pitch, rate);

            HintAboutSpeech();

            SaveNeeds();
            Memory.Save(PetKey);
            ChatChanged?.Invoke();
            return true;
        }

        /// <summary>Orders that run themselves: reacting on top of them would cancel the walk.</summary>
        private static bool IsMovingOrder(PetOrderKind kind)
        {
            switch (kind)
            {
                case PetOrderKind.Eat:
                case PetOrderKind.Drink:
                case PetOrderKind.Play:
                case PetOrderKind.Fetch:
                case PetOrderKind.Toilet:
                case PetOrderKind.Bath:
                case PetOrderKind.Groom:
                case PetOrderKind.Come:
                case PetOrderKind.Follow:
                    return true;
                default:
                    return false;
            }
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

            // ...and says the line in words, unless the player has muted it. Same voice profile as
            // the chirp, so a bear reading its own sentence still sounds like the bear. This is ON
            // by default now: it used to be off, and the report from the first phone to run it was
            // "the pet still does not talk", because the switch lives in a panel nobody opens.
            float speechPitch, speechRate;
            PetVoice.SpeechParams(Species, Personality, out speechPitch, out speechRate);
            DshMobile.MobileTts.Speak(reply.Speech, speechPitch, speechRate);

            HintAboutSpeech();

            SaveNeeds();
            Memory.Save(PetKey);
            ChatChanged?.Invoke();
        }

        /// <summary>
        /// Tells the player once, in the conversation itself, that the pet can speak.
        ///
        /// A switch nobody finds is a feature nobody has. The first version defaulted to off and
        /// said so only inside the settings panel, which is exactly where a player who has never
        /// opened the settings will never look — and the report back was "I never heard it talk".
        /// Once, only where a speech engine exists, and never again after the switch is touched.
        /// </summary>
        private void HintAboutSpeech()
        {
            if (!DshMobile.MobileTts.Available) return;
            if (DshMobile.MobileTts.Enabled) return;
            if (DshMobile.MobileTts.HintShown) return;

            DshMobile.MobileTts.HintShown = true;
            Memory.AddSystem("（设置里可以打开「朗读宠物的话」，它就能把说的话念出来）");
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
                ExtraInstructions = BrainConfig != null ? BrainConfig.ExtraInstructions : "",
                Perception = DescribePerception()
            };
        }

        // ------------------------------------------------------------- perception & orders

        /// <summary>
        /// Everything the pet can see, as the prompt block body and as the source of every spatial
        /// answer it gives. See <see cref="PetPerception"/> for the word choices.
        /// </summary>
        public string DescribePerception()
        {
            var targets = new List<PetTarget>();

            if (Room != null)
            {
                for (int i = 0; i < Room.Interactables.Count; i++)
                {
                    var item = Room.Interactables[i];
                    if (item == null) continue;
                    if (item.Kind == InteractableKind.Mess && !Room.HasMess) continue;

                    targets.Add(new PetTarget
                    {
                        Name = string.IsNullOrEmpty(item.Label)
                            ? PetPerception.NameOf(item.Kind)
                            : item.Label,
                        Kind = item.Kind,
                        Position = item.transform.position,
                        Ready = item.IsReady
                    });
                }
            }

            Vector3 eye = Controller != null ? Controller.transform.position : Vector3.zero;
            Vector3 facing = Controller != null ? Controller.transform.forward : Vector3.forward;

            bool hasOwner = Controller != null && Controller.Player != null;
            Vector3 owner = hasOwner ? Controller.Player.position : Vector3.zero;

            return PetPerception.Describe(eye, facing, targets, Room != null ? Room.Size : 0f,
                hasOwner, owner);
        }

        /// <summary>The pet's own position, for the spatial answers.</summary>
        private Vector3 PetPosition()
            => Controller != null ? Controller.transform.position : Vector3.zero;

        private Vector3 PetFacing()
        {
            if (Controller == null) return Vector3.forward;
            var facing = Controller.transform.forward;
            facing.y = 0f;
            return facing.sqrMagnitude > 0.0001f ? facing : Vector3.forward;
        }

        /// <summary>
        /// Answers 「XX 在哪」 from perception, on the device.
        ///
        /// The whole point of owning the pet's spatial knowledge is that this answer needs nobody's
        /// help: no network, no tokens, and no chance of the pet pointing at a wall because a model
        /// felt like it.
        /// </summary>
        public string DescribeWhere(PetOrder order)
        {
            if (order.AboutOwner)
            {
                if (Controller == null || Controller.Player == null) return "我看不到你在哪，你在房间里吗？";
                return "你" + OnlyWhere(PetPosition(), PetFacing(), Controller.Player.position) + "呀。";
            }

            if (order.Target.HasValue)
            {
                var item = FindRoomItem(order.Target.Value);

                // The room's own label wins, the same way it does in the perception block: the pet
                // must call things what the player calls them, or 「饭碗在哪」 is answered about a
                // 「食物碗」 and reads like a different object.
                string name = "「" + (item != null && !string.IsNullOrEmpty(item.Label)
                    ? item.Label
                    : PetPerception.NameOf(order.Target.Value)) + "」";

                if (item == null) return "房间里好像没有" + name + "……你是不是还没摆出来？";

                return name + OnlyWhere(PetPosition(), PetFacing(), item.transform.position)
                    + "，我这就指给你看。";
            }

            // No object named: describe the room instead of refusing, because "在哪" with no subject
            // is a question about where things are, and the pet does know that.
            return "你现在在" + PetPerception.RoomPlace(PetPosition(), Room != null ? Room.Size : 0f)
                + "，房间里的东西我都记得，你问哪一样？";
        }

        /// <summary>"在你右前方，大概 3 步" without the leading 在你 of the prompt phrasing.</summary>
        private static string OnlyWhere(Vector3 from, Vector3 facing, Vector3 to)
        {
            string sentence = PetPerception.WhereSentence(from, facing, to);
            return sentence.StartsWith("在你") ? sentence.Substring(2) : sentence;
        }

        private Interactable FindRoomItem(InteractableKind kind)
        {
            if (Room == null) return null;

            Interactable best = null;
            float bestDistance = float.MaxValue;
            Vector3 from = PetPosition();

            for (int i = 0; i < Room.Interactables.Count; i++)
            {
                var item = Room.Interactables[i];
                if (item == null || item.Kind != kind) continue;

                float distance = Vector3.Distance(from, item.transform.position);
                if (distance < bestDistance) { bestDistance = distance; best = item; }
            }

            return best;
        }

        /// <summary>
        /// Carries out an order. False when the room cannot support it, which the caller turns into an
        /// honest answer rather than silence.
        /// </summary>
        private bool ExecuteOrder(PetOrder order)
        {
            if (Controller == null) return false;

            switch (order.Kind)
            {
                case PetOrderKind.Come: return Controller.ComeToOwner();
                case PetOrderKind.Follow: return Controller.FollowOwner();

                case PetOrderKind.Stay:
                    Controller.Stay();
                    return true;

                case PetOrderKind.Sleep:
                    Controller.EnterSleep();
                    return true;

                case PetOrderKind.Fetch:
                    // A thrown ball is the whole retrieve loop; a ball sitting in its corner is "go
                    // and pick it up", which is the same walk the pet does when it decides to play.
                    // Refusing because the ball was not *loose* was the first version's behaviour and
                    // it made 「拿球」 fail in exactly the case a player would try it.
                    if (Controller.CanFetch)
                    {
                        Controller.FetchBall();
                        return true;
                    }

                    return Controller.OrderTo(InteractableKind.Ball);

                case PetOrderKind.AskWhere:
                    return true;   // answering is the action

                case PetOrderKind.Eat: return Controller.OrderTo(InteractableKind.Food);
                case PetOrderKind.Drink: return Controller.OrderTo(InteractableKind.Water);
                case PetOrderKind.Play:
                    // 玩 means whichever toy is there: the ball if it is out, otherwise the toy box.
                    return Controller.OrderTo(InteractableKind.Ball)
                        || Controller.OrderTo(InteractableKind.Toy);
                case PetOrderKind.Toilet: return Controller.OrderTo(InteractableKind.Toilet);
                case PetOrderKind.Bath: return Controller.OrderTo(InteractableKind.Bath);
                case PetOrderKind.Groom: return Controller.OrderTo(InteractableKind.Brush);

                default: return false;
            }
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

            // The apple tree is the owner's to harvest, not the pet's: picking and watering
            // happen on the spot, eating is what the pet does on its own when it is hungry.
            if (target.Kind == InteractableKind.AppleTree)
            {
                HarvestAppleTree(target);
                return;
            }

            // The pond offers two owner actions: fish (always) and draw water (with the bucket).
            if (target.Kind == InteractableKind.Pond)
            {
                UsePond(target);
                return;
            }

            if (Controller != null) Controller.GoTo(target);
        }

        /// <summary>
        /// The owner harvests the apple tree: pick its apples, or water it with a full bucket.
        /// </summary>
        private void HarvestAppleTree(Interactable target)
        {
            var tree = target != null ? target.GetComponent<PetAppleTree>() : null;
            if (tree == null) return;

            if (PetBackpack.HasBucket && PetBackpack.BucketFull)
            {
                PetBackpack.BucketFull = false;
                tree.Water();
                PetHud.SetToast("用水桶浇了苹果树，它马上多结了一个苹果。");
                DshMobile.MobileHaptics.Light();
                return;
            }

            int picked = tree.PickAll();
            if (picked <= 0)
            {
                PetHud.SetToast("树上还没有苹果，等等就有了" +
                                  (PetBackpack.HasBucket ? "（背包装备水桶、在池塘打水再浇，结得更快）" : "。"));
                return;
            }

            PetInventory.Add("apple", picked);
            PetHud.SetToast($"摘了 {picked} 个苹果，放进了仓库（能吃也能卖钱）。");
            DshMobile.MobileHaptics.Light();
        }

        /// <summary>The owner uses the pond: fish, or fill the bucket.</summary>
        private void UsePond(Interactable target)
        {
            var pond = target != null ? target.GetComponent<PetPond>() : null;
            if (pond == null) return;

            if (PetBackpack.HasBucket && !PetBackpack.BucketFull)
            {
                PetBackpack.BucketFull = true;
                PetHud.SetToast("用水桶从池塘打了水，去浇苹果树吧。");
                DshMobile.MobileHaptics.Light();
                return;
            }

            if (pond.FishReady)
            {
                if (pond.TryCatch())
                {
                    PetInventory.Add("fish", 1);
                    PetHud.SetToast("钓上了一条鱼！放进了仓库（能吃也能卖钱）。");
                    DshMobile.MobileHaptics.Medium();
                }
                return;
            }

            if (pond.IsFishing)
            {
                PetHud.SetToast("鱼漂动了……再等等。");
                return;
            }

            pond.Cast();
            PetHud.SetToast("把鱼线甩进了池塘，等鱼上钩。");
            DshMobile.MobileHaptics.Light();
        }

        /// <summary>
        /// Wipes up an accident — but only with a shovel equipped. The pet notices and is quietly
        /// grateful. Without the tool the player is told what is missing, which is the shovel's
        /// whole reason for being a backpack item rather than a free hand gesture.
        /// </summary>
        public void CleanMess(Interactable mess)
        {
            if (mess == null || Room == null) return;

            if (!PetBackpack.CanCleanMess)
            {
                PetHud.SetToast("需要装备铲子才能清理排泄物（商城买铲子 → 背包装备）。");
                return;
            }

            Room.RemoveMess(mess);
            Needs.Clean(0.12f);
            Needs.AddAffection(0.03f);
            Needs.Pet(0.08f);

            // Tidying up is a chore, and chores pay. It is also the consolation prize of the
            // no-litter-box loop: a pet without a tray makes work, and work should be worth it.
            DshMobile.PetWallet.Add(CleaningCoinReward);

            PetAudioDirector.Instance?.Play(SfxId.Brush);
            DshMobile.MobileHaptics.Light();

            Memory.AddPet("（主人用铲子把地上的污渍铲干净了，你有点不好意思地蹭了蹭她的腿）");
            Journal.Add(MemoryKind.Care, "主人帮我收拾", "", 0.35f);
            ChatChanged?.Invoke();
        }

        /// <summary>Coins earned for wiping up an accident.</summary>
        public const int CleaningCoinReward = 5;

        /// <summary>
        /// Feeds the pet one unit of a food item straight from the backpack/warehouse, no bowl
        /// required. This is the "把食物放进背包投喂" action: it is the owner's hand, not the
        /// bowl, doing the feeding, and it works for anything edible — kibble, meat, apple, fish,
        /// or a drink of water.
        /// </summary>
        public void FeedFromBackpack(string itemId)
        {
            var item = PetShop.Get(itemId);
            if (item == null || !item.IsFood) return;
            if (PetInventory.Count(itemId) <= 0)
            {
                PetHud.SetToast("没有" + item.Name + "了");
                return;
            }

            PetInventory.SetCount(itemId, PetInventory.Count(itemId) - 1);

            if (item.IsDrink) Needs.GiveWater();
            else Needs.Feed(item.FoodAmount);

            Needs.AddAffection(0.04f);
            PetAudioDirector.Instance?.Play(SfxId.Eat);
            DshMobile.MobileHaptics.Light();

            Memory.AddPet("（主人喂我吃了" + item.Name + "，真好吃）");
            Journal.Add(MemoryKind.Care, "主人喂我吃了" + item.Name, "", 0.35f);
            ChatChanged?.Invoke();
            PetHud.SetToast("喂了" + item.Name + "，宠物很开心。");
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
            Memory.Save(PetKey);
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
            // Touching the pet is also "show me this one": the card follows the hand.
            SelectPet(0);

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

            Memory.Save(PetKey);
            Species = next.Copy();
            PetName = PlayerPrefs.GetString(NameKey + "." + PetKey, Species.DisplayName);

            PlayerPrefs.SetString(SpeciesKey, Species.Id);
            PlayerPrefs.Save();

            LoadNeeds();
            BindPersonality();
            Avatar.Build(Species, false);
            Memory.Load(PetKey);
            Journal.Bind(PetKey);
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

            PlayerPrefs.SetString(NameKey + "." + PetKey, PetName);
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
            Memory.Save(PetKey);
            Needs.Hunger = 0.85f;
            Needs.Energy = 0.85f;
            Needs.Joy = 0.7f;
            Needs.Cleanliness = 0.9f;
            // The bladder was added after this button was written, and a reset that leaves the
            // pet desperate for the tray is a reset that did not happen.
            Needs.Bladder = 0.9f;
            Needs.Affection = 0.1f;
            SaveNeeds();
            Memory.AddPet(Greeting());
            ChatChanged?.Invoke();
        }

        // ---------------------------------------------------------------- storage

        private void SaveNeeds()
        {
            PlayerPrefs.SetString(NeedsKey + "." + PetKey, JsonUtility.ToJson(Needs.Snapshot()));
            PlayerPrefs.Save();
        }

        private void LoadNeeds()
        {
            string json = PlayerPrefs.GetString(NeedsKey + "." + PetKey, "");
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
            Memory.Save(PetKey);
            Journal.Save();
        }
    }
}
