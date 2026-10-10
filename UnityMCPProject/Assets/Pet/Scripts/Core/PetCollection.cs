using System;
using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// One pet the player owns.
    ///
    /// A record rather than a live object: the warehouse holds many of these, the backpack
    /// holds up to three, and only the ones in the backpack get instantiated into the room.
    /// Each record carries its own name and temperament so that two foxes are two different
    /// animals — the whole point of being able to own more than one.
    /// </summary>
    [Serializable]
    public class PetRecord
    {
        /// <summary>Stable id, unique per owned pet. Used as the backpack's slot value.</summary>
        public string Id = "";

        public string SpeciesId = "fox";
        public string Name = "";
        public float Liveliness = 0.5f;
        public float Clinginess = 0.5f;
        public float Curiosity = 0.5f;
        public float Neatness = 0.5f;

        /// <summary>Set for the pet the player is currently focused on: it owns the brain and the journal.</summary>
        public bool Primary;

        /// <summary>
        /// Who the parents were, for a pet that was born in the room rather than bought: "小猫咪 和 小熊".
        /// Kept so the collection panel can say where a pet came from — a pet with a history is
        /// worth more to look at than one that appeared from a shop.
        /// </summary>
        public string Parents = "";

        /// <summary>When it was born, as a UTC round-trip string. Empty for bought pets.</summary>
        public string BornAt = "";

        public PetPersonality Personality => new PetPersonality
        {
            Liveliness = Liveliness,
            Clinginess = Clinginess,
            Curiosity = Curiosity,
            Neatness = Neatness
        };

        public void SetPersonality(PetPersonality personality)
        {
            if (personality == null) return;
            Liveliness = personality.Liveliness;
            Clinginess = personality.Clinginess;
            Curiosity = personality.Curiosity;
            Neatness = personality.Neatness;
        }

        public static PetRecord Create(string speciesId, string name, PetPersonality personality,
            int seed)
        {
            var record = new PetRecord
            {
                Id = $"p{seed}{Mathf.Abs((speciesId ?? "pet").GetHashCode() % 1000)}",
                SpeciesId = speciesId,
                Name = string.IsNullOrEmpty(name) ? PetSpecies.Get(speciesId).DisplayName : name
            };
            record.SetPersonality(personality);
            return record;
        }

        public PetRecord Copy() => new PetRecord
        {
            Id = Id,
            SpeciesId = SpeciesId,
            Name = Name,
            Liveliness = Liveliness,
            Clinginess = Clinginess,
            Curiosity = Curiosity,
            Neatness = Neatness,
            Primary = Primary,
            Parents = Parents,
            BornAt = BornAt
        };
    }

    /// <summary>Everything the collection owns, as one serialisable blob.</summary>
    [Serializable]
    public class PetCollectionData
    {
        public List<PetRecord> Pets = new List<PetRecord>();
        public List<string> Backpack = new List<string>();
        public List<string> UnlockedSpecies = new List<string>();
    }

    /// <summary>
    /// The shop, the warehouse and the backpack, on top of one saved collection.
    ///
    /// Three ideas in one place because they are one system: buying a pet creates a record,
    /// the warehouse is where records live, and the backpack is the shortlist of records that
    /// are actually walking around the room. Splitting them into three files would mean three
    /// copies of "which pets exist", which is exactly the kind of state that drifts.
    /// </summary>
    public static class PetCollection
    {
        public const string Key = "dshpet.collection";
        public const int BackpackSlots = 3;

        private static PetCollectionData _data;

        public static event Action Changed;

        public static PetCollectionData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static IReadOnlyList<PetRecord> Warehouse => Data.Pets;

        public static IReadOnlyList<string> Backpack => Data.Backpack;

        // ------------------------------------------------------------------ storing

        public static void Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { _data = JsonUtility.FromJson<PetCollectionData>(json); }
                catch { _data = null; }
            }

            if (_data == null) _data = new PetCollectionData();
            if (_data.Pets == null) _data.Pets = new List<PetRecord>();
            if (_data.Backpack == null) _data.Backpack = new List<string>();
            if (_data.UnlockedSpecies == null) _data.UnlockedSpecies = new List<string>();

            EnsureStarters();
            Save();
        }

        /// <summary>Writes the collection to disk. Public so the room can push a change into it.</summary>
        public static void SaveNow() => Save();

        public static void Save()
        {
            if (_data == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Gives a fresh save its first animal, chosen by the player on the front door.
        ///
        /// Without this the player opens the game with an empty room and no way to get a pet
        /// except grinding the runner — which is a terrible first five minutes for a pet game.
        /// A save that already has pets predates the onboarding flow, so it is migrated rather
        /// than re-seeded.
        /// </summary>
        private static void EnsureStarters()
        {
            EnsureUnlocked();

            if (_data.Pets.Count > 0)
            {
                // Migration: an existing home already made its choice, even if it happened before
                // the pick-your-pet screen existed. Mark it so the front door never pops the
                // starter question over a player who is already living with a bear.
                if (!PetOnboarding.HasChosenStarter)
                {
                    string migrate = _data.Pets[0].SpeciesId;
                    for (int i = 0; i < _data.Pets.Count; i++)
                    {
                        if (_data.Pets[i].Primary) { migrate = _data.Pets[i].SpeciesId; break; }
                    }
                    PetOnboarding.MarkChosen(migrate);
                }
                return;
            }

            string chosen = PetOnboarding.IsStarterChoice(PetOnboarding.StarterChoice)
                ? PetOnboarding.StarterChoice
                : "fox";

            var starter = PetSpecies.Get(chosen);
            var record = PetRecord.Create(starter.Id, starter.DisplayName,
                PetPersonality.Load(starter.Id), 1);
            record.Primary = true;
            _data.Pets.Add(record);
            _data.Backpack.Add(record.Id);

            // Starter kit: one bag of kibble so the tutorial's first feed is possible at once.
            PetOnboarding.GrantStarterKit();
        }

        private static void EnsureUnlocked()
        {
            foreach (var species in PetSpecies.All)
            {
                if (species.Starter && !_data.UnlockedSpecies.Contains(species.Id))
                {
                    _data.UnlockedSpecies.Add(species.Id);
                }
            }
        }

        // ---------------------------------------------------------------------- shop

        public static bool IsSpeciesUnlocked(string speciesId)
            => Data.UnlockedSpecies.Contains(speciesId);

        public static int OwnedCount(string speciesId)
        {
            int count = 0;
            for (int i = 0; i < Data.Pets.Count; i++)
            {
                if (Data.Pets[i].SpeciesId == speciesId) count++;
            }
            return count;
        }

        /// <summary>
        /// Buys a species. Returns null when it could not be afforded.
        ///
        /// Buying an already-owned species is allowed and cheap-ish on purpose: two foxes is a
        /// legitimate thing to want, and each one rolls its own temperament.
        /// </summary>
        /// <summary>
        /// What this species costs right now.
        ///
        /// The re-buy discount has a floor, and the floor is the point: the starter species is
        /// free, so half of free is free, and without a minimum the shop would sell unlimited
        /// foxes for nothing — which makes the whole economy optional.
        /// </summary>
        public static int PriceFor(string speciesId)
        {
            var species = PetSpecies.Get(speciesId);
            return IsSpeciesUnlocked(speciesId)
                ? Mathf.Max(MinRebuyPrice, Mathf.RoundToInt(species.Price * 0.5f))
                : species.Price;
        }

        /// <summary>Cheapest a species can be bought again for.</summary>
        public const int MinRebuyPrice = 120;

        /// <summary>What the shop pays for a pet. Half of what it costs, with a floor.</summary>
        public const int MinSellPrice = 80;

        /// <summary>
        /// What selling this pet is worth.
        ///
        /// Deliberately less than the purchase price — a shop that buys back at cost is a bank,
        /// not a shop — but never nothing: a pet the player has stopped wanting is still worth a
        /// couple of runs.
        /// </summary>
        public static int SellPriceFor(string speciesId)
            => Mathf.Max(MinSellPrice, PriceFor(speciesId) / 2);

        /// <summary>
        /// Sells a pet back to the shop. Returns false, and changes nothing, when it cannot.
        ///
        /// Two refusals, both deliberate: the pet being looked after is not for sale (the player
        /// would be selling the animal whose memory and diary they are reading), and the last pet
        /// is not for sale either — a room with nobody in it is not a game any more.
        /// </summary>
        public static bool Sell(string recordId, out string message, out int paid)
        {
            paid = 0;

            var record = Find(recordId);
            if (record == null)
            {
                message = "没有这只宠物";
                return false;
            }

            var primary = Primary;
            if (primary != null && primary.Id == recordId)
            {
                message = "它正在被你照顾，先换一只主要照顾的宠物再卖";
                return false;
            }

            if (Data.Pets.Count <= 1)
            {
                message = "至少要留一只宠物陪你";
                return false;
            }

            paid = SellPriceFor(record.SpeciesId);
            Data.Pets.Remove(record);
            Data.Backpack.Remove(recordId);

            // The room always keeps somebody: if the backpack empties, the primary moves in.
            if (Data.Backpack.Count == 0 && primary != null) Data.Backpack.Add(primary.Id);

            DshMobile.PetWallet.Add(paid);
            Save();
            Changed?.Invoke();

            message = $"卖掉了{record.Name}，得到 {paid} 个宠物币";
            return true;
        }

        /// <summary>
        /// Adds a pet that was born in the room rather than bought.
        ///
        /// It goes to the warehouse, not straight into the backpack: a birth is not a purchase, and
        /// filling the last backpack slot with a kitten the player did not ask for would be the
        /// game deciding who they are looking after.
        /// </summary>
        public static PetRecord AddBorn(PetRecord record)
        {
            if (record == null) return null;

            record.Name = UniqueName(record.Name);
            record.BornAt = System.DateTime.UtcNow.ToString("o");
            Data.Pets.Add(record);
            if (!Data.UnlockedSpecies.Contains(record.SpeciesId)) Data.UnlockedSpecies.Add(record.SpeciesId);

            Save();
            Changed?.Invoke();
            return record;
        }

        public static PetRecord Buy(string speciesId, out string message)
        {
            var species = PetSpecies.Get(speciesId);
            int price = PriceFor(speciesId);

            if (!DshMobile.PetWallet.TrySpend(price))
            {
                message = $"还差 {price - DshMobile.PetWallet.Coins} 个宠物币";
                return null;
            }

            var personality = PetPersonality.Create(
                Environment.TickCount ^ (speciesId ?? "").GetHashCode() ^ Data.Pets.Count * 7919);

            var record = PetRecord.Create(speciesId, UniqueName(species.DisplayName), personality,
                Data.Pets.Count + 1);
            Data.Pets.Add(record);
            if (!Data.UnlockedSpecies.Contains(speciesId)) Data.UnlockedSpecies.Add(speciesId);

            // A newly bought pet goes straight into the backpack if there is room: buying
            // something and then having to go and find it is a pointless second step.
            if (Data.Backpack.Count < BackpackSlots) Data.Backpack.Add(record.Id);

            Save();
            Changed?.Invoke();

            message = $"带回了{record.Name}（{personality.Archetype}）";
            return record;
        }

        /// <summary>
        /// A default name that is not already taken: 小猫咪, 小猫咪2, 小猫咪3.
        ///
        /// Two pets called 小猫咪 is fine in a warehouse list and useless in a room: the status
        /// card labels each animal by name, so the names have to tell them apart. A name the
        /// player typed is never touched — only the generated one.
        /// </summary>
        public static string UniqueName(string baseName)
        {
            if (string.IsNullOrEmpty(baseName)) baseName = "宠物";

            string candidate = baseName;
            int suffix = 1;
            while (NameTaken(candidate))
            {
                suffix++;
                candidate = baseName + suffix;
            }
            return candidate;
        }

        private static bool NameTaken(string name)
        {
            for (int i = 0; i < Data.Pets.Count; i++)
            {
                if (Data.Pets[i] != null && Data.Pets[i].Name == name) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ backpack

        /// <summary>
        /// A pull from the 扭蛋机: one fixed price, one random gacha-only pet, straight to the
        /// warehouse. The species is weighted, so a pull is a real bet.
        /// </summary>
        public static PetRecord RollGacha(out string message)
        {
            if (!DshMobile.PetWallet.TrySpend(PetGacha.Cost))
            {
                message = $"还差 {PetGacha.Cost - DshMobile.PetWallet.Coins} 个宠物币";
                return null;
            }

            var species = PetGacha.Pick(new System.Random(Environment.TickCount ^ Data.Pets.Count * 104729));
            if (species == null)
            {
                message = "扭蛋机空转了一下，什么都没出";
                return null;
            }

            var personality = PetPersonality.Create(
                Environment.TickCount ^ species.Id.GetHashCode() ^ Data.Pets.Count * 7919);
            var record = PetRecord.Create(species.Id, UniqueName(species.DisplayName), personality,
                Data.Pets.Count + 1);
            record.Name = UniqueName(record.Name);

            // A gacha win goes to the warehouse, not the backpack: it is a surprise, not a
            // purchase, and the player decides whether to bring it into the room.
            Data.Pets.Add(record);
            if (!Data.UnlockedSpecies.Contains(species.Id)) Data.UnlockedSpecies.Add(species.Id);

            Save();
            Changed?.Invoke();

            message = $"扭蛋机转出了 {record.Name}（{personality.Archetype}）";
            return record;
        }

        public static PetRecord Find(string recordId)
        {
            for (int i = 0; i < Data.Pets.Count; i++)
            {
                if (Data.Pets[i].Id == recordId) return Data.Pets[i];
            }
            return null;
        }

        public static bool IsInBackpack(string recordId) => Data.Backpack.Contains(recordId);

        /// <summary>
        /// Puts a pet into the backpack. Returns false when it is full.
        ///
        /// Three slots, and the UI says out loud that one is the sensible number: every extra
        /// pet in the room is another animal the brain has to keep coherent, and the player is
        /// the one paying for the tokens.
        /// </summary>
        public static bool AddToBackpack(string recordId)
        {
            if (IsInBackpack(recordId)) return true;
            if (Data.Backpack.Count >= BackpackSlots) return false;
            if (Find(recordId) == null) return false;

            Data.Backpack.Add(recordId);
            Save();
            Changed?.Invoke();
            return true;
        }

        public static bool RemoveFromBackpack(string recordId)
        {
            // Never leave the room empty: taking the last pet out would leave a bare room with
            // no way to put one back from inside the scene.
            if (Data.Backpack.Count <= 1) return false;
            if (!Data.Backpack.Remove(recordId)) return false;

            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>The pet that owns the brain, the journal and the player's attention.</summary>
        public static PetRecord Primary        {
            get
            {
                for (int i = 0; i < Data.Pets.Count; i++)
                {
                    if (Data.Pets[i].Primary) return Data.Pets[i];
                }
                return Data.Pets.Count > 0 ? Data.Pets[0] : null;
            }
        }

        /// <summary>Makes a record the primary pet, and clears the flag everywhere else.</summary>
        public static void SetPrimary(string recordId)
        {
            var target = Find(recordId);
            if (target == null) return;

            for (int i = 0; i < Data.Pets.Count; i++) Data.Pets[i].Primary = false;
            target.Primary = true;

            // The primary pet leads the backpack list: the panel and the room both read it first,
            // so "主要照顾" is always the first slot rather than wherever the pet happened to be
            // added. If the backpack is full the record keeps its flag but stays out of the room,
            // which the panel labels "主要照顾（不在房间里）".
            if (!Data.Backpack.Contains(recordId) && Data.Backpack.Count < BackpackSlots)
            {
                Data.Backpack.Add(recordId);
            }
            if (Data.Backpack.Contains(recordId))
            {
                Data.Backpack.Remove(recordId);
                Data.Backpack.Insert(0, recordId);
            }

            Save();
            Changed?.Invoke();
        }

        /// <summary>The records that get instantiated into the room, primary first.</summary>
        public static List<PetRecord> Companions()        {
            var list = new List<PetRecord>();
            for (int i = 0; i < Data.Backpack.Count; i++)
            {
                var record = Find(Data.Backpack[i]);
                if (record != null) list.Add(record);
            }

            if (list.Count == 0)
            {
                var primary = Primary;
                if (primary != null) list.Add(primary);
            }

            return list;
        }

        /// <summary>Forgets everything. Used by the reset button and by tests.</summary>
        public static void ResetAll()
        {
            _data = new PetCollectionData();
            EnsureStarters();
            Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Replaces the whole collection, for tests that need a known starting state.
        ///
        /// Seeds the starter pet and saves, exactly like a fresh load would. A replacement that
        /// left the collection empty would test a state the game never actually produces — and
        /// then disagree with the real thing about how many pets exist, which is precisely the
        /// bug the first version of this helper caused.
        /// </summary>
        public static void ReplaceForTests(PetCollectionData data)
        {
            _data = data ?? new PetCollectionData();
            if (_data.Pets == null) _data.Pets = new List<PetRecord>();
            if (_data.Backpack == null) _data.Backpack = new List<string>();
            if (_data.UnlockedSpecies == null) _data.UnlockedSpecies = new List<string>();

            EnsureStarters();
            Save();
            Changed?.Invoke();
        }
    }
}
