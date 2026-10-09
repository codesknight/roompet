using System;
using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The backpack's tool slots: three pockets for carrying tools around.
    ///
    /// Tools are the things a pet can't use on its own — the shovel that lets the owner clean up
    /// a mess, the bucket that carries water to the tree. Owning one is not enough: it has to be
    /// *equipped*, which is the difference between "I bought a shovel" and "I can clean up now",
    /// and it is the whole point of the backpack as a place the player actually uses.
    ///
    /// The three pet slots live elsewhere (<see cref="PetCollection"/>), because pets and tools
    /// have different rules — a pet needs a name and a record, a tool needs nothing but a pocket.
    /// </summary>
    public static class PetBackpack
    {
        public const string EquippedKey = "dshpet.backpack.tools";

        /// <summary>How many tools can be carried at once.</summary>
        public const int ToolSlots = 3;

        public static event Action Changed;

        /// <summary>The ids of the tools currently in the backpack's tool slots, in order.</summary>
        public static List<string> Equipped()
        {
            var list = new List<string>();
            string raw = PlayerPrefs.GetString(EquippedKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string part in raw.Split(','))
                {
                    if (!string.IsNullOrEmpty(part) && !list.Contains(part)) list.Add(part);
                }
            }
            return list;
        }

        public static bool IsEquipped(string id) => Equipped().Contains(id);

        /// <summary>The shovel, carried: required to clean up a mess.</summary>
        public static bool HasShovel => IsEquipped("shovel");

        /// <summary>The bucket, carried: required to draw water and water the tree.</summary>
        public static bool HasBucket => IsEquipped("bucket");

        /// <summary>The bucket, full of water from the pond. Draw water first, then water the tree.</summary>
        public static bool BucketFull
        {
            get => PlayerPrefs.GetInt(BucketFullKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(BucketFullKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        private const string BucketFullKey = "dshpet.backpack.bucketfull";

        /// <summary>How many pockets are still empty.</summary>
        public static int FreeSlots => Mathf.Max(0, ToolSlots - Equipped().Count);

        private static void Save(List<string> tools)
        {
            PlayerPrefs.SetString(EquippedKey, string.Join(",", tools.ToArray()));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Puts a tool into a free pocket. Returns a player-facing message.</summary>
        public static string Equip(string id)
        {
            var item = PetShop.Get(id);
            if (item == null) return "没有这件道具";
            if (!item.IsTool) return item.Name + "不是道具，不能装进背包";
            if (!PetInventory.IsOwned(id)) return "还没有" + item.Name + "，先去商城买";
            if (IsEquipped(id)) return item.Name + "已经在背包里了";

            var tools = Equipped();
            if (tools.Count >= ToolSlots) return $"背包只有 {ToolSlots} 个道具栏，先取下一件再装";

            tools.Add(id);
            Save(tools);
            return item.Name + "装进了背包";
        }

        /// <summary>Takes a tool out of the backpack. Returns a player-facing message.</summary>
        public static string Unequip(string id)
        {
            var tools = Equipped();
            if (tools.Remove(id))
            {
                Save(tools);
                var item = PetShop.Get(id);
                return item != null ? item.Name + "从背包里取下了" : "取下了";
            }
            return "它没在背包里";
        }

        /// <summary>True while the owner is carrying the tool needed to clean a mess.</summary>
        public static bool CanCleanMess => HasShovel;

        /// <summary>True while the owner is carrying the tool needed to draw water.</summary>
        public static bool CanDrawWater => HasBucket;

        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(EquippedKey);
            PlayerPrefs.DeleteKey(BucketFullKey);
        }
    }
}
