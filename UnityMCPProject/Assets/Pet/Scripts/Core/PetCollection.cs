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
            Primary = Primary
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

        public static void Save()
        {
            if (_data == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Gives a fresh save its first animal.
        ///
        /// Without this the player opens the game with an empty room and no way to get a pet
        /// except grinding the runner — which is a terrible first five minutes for a pet game.
        /// </summary>
        private static void EnsureStarters()
        {
            EnsureUnlocked();

            if (_data.Pets.Count > 0) return;

            var starter = PetSpecies.All[0];
            var record = PetRecord.Create(starter.Id, starter.DisplayName,
                PetPersonality.Load(starter.Id), 1);
            record.Primary = true;
            _data.Pets.Add(record);
            _data.Backpack.Add(record.Id);
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
        public static PetRecord Primary
        {
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
            if (!IsInBackpack(recordId)) AddToBackpack(recordId);

            Save();
            Changed?.Invoke();
        }

        /// <summary>The records that get instantiated into the room, primary first.</summary>
        public static List<PetRecord> Companions()
        {
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
