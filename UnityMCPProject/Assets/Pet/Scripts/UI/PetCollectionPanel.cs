using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>Which list the collection panel is showing.</summary>
    public enum CollectionTab { Shop, Warehouse, Backpack }

    /// <summary>
    /// The shop, the warehouse and the backpack, as one panel with three tabs.
    ///
    /// One panel rather than three because the actions run into each other: you buy a pet, it
    /// lands in the warehouse, you put it in the backpack, and it appears in the room. Three
    /// separate screens would each need to explain where the player is in that chain; a tab
    /// strip shows the whole chain at once, with the coin balance pinned above it.
    ///
    /// All the state lives in <see cref="PetCollection"/>; this file is only the drawing.
    /// </summary>
    public static class PetCollectionPanel
    {
        public const string TabPrefKey = "dshpet.collection.tab";

        /// <summary>Result of the last action, shown under the tabs.</summary>
        public struct Result
        {
            public string Message;
            public bool Error;
        }

        /// <summary>
        /// Buys a species and reports what happened: the message is user-facing either way,
        /// because "nothing happened" is the worst possible response to pressing a button.
        /// </summary>
        public static Result Buy(string speciesId)
        {
            string message;
            var record = PetCollection.Buy(speciesId, out message);
            return new Result { Message = message, Error = record == null };
        }

        public static Result PutInBackpack(string recordId)
        {
            var record = PetCollection.Find(recordId);
            if (record == null) return new Result { Message = "找不到这只宠物", Error = true };

            if (PetCollection.IsInBackpack(recordId))
            {
                return new Result { Message = record.Name + " 已经在背包里了" };
            }

            if (!PetCollection.AddToBackpack(recordId))
            {
                return new Result
                {
                    Message = $"背包满了（最多 {PetCollection.BackpackSlots} 只），先从背包拿一只出来",
                    Error = true
                };
            }

            return new Result { Message = record.Name + " 跟着你进屋了" };
        }

        public static Result TakeOutOfBackpack(string recordId)
        {
            var record = PetCollection.Find(recordId);
            if (record == null) return new Result { Message = "找不到这只宠物", Error = true };

            if (PetCollection.Backpack.Count <= 1)
            {
                return new Result { Message = "房间里至少要留一只宠物", Error = true };
            }

            if (!PetCollection.RemoveFromBackpack(recordId))
            {
                return new Result { Message = "没能拿出来", Error = true };
            }

            return new Result { Message = record.Name + " 回仓库了" };
        }

        /// <summary>
        /// Makes a pet the one being looked after.
        ///
        /// The room's own switch (<c>PetGameManager.MakePrimary</c>) is the real hand-over — it
        /// moves the memory, the diary and the brain. This only handles the case where the panel is
        /// open in the room scene and the manager is available; the collection itself is updated
        /// either way, so the drawer and the room agree on who is primary.
        /// </summary>
        public static Result MakePrimary(string recordId)
        {
            var record = PetCollection.Find(recordId);
            if (record == null) return new Result { Message = "找不到这只宠物", Error = true };

            var manager = PetGameManager.Instance;
            if (manager != null && manager.MakePrimary(recordId))
            {
                return new Result { Message = $"现在主要照顾 {record.Name}（记忆和记事本跟着它走）" };
            }

            PetCollection.SetPrimary(recordId);
            return new Result { Message = $"现在主要照顾 {record.Name}" };
        }

        /// <summary>Sells a pet back to the shop, with the price stated before it happens.</summary>
        public static Result Sell(string recordId)
        {
            var record = PetCollection.Find(recordId);
            if (record == null) return new Result { Message = "找不到这只宠物", Error = true };

            int price = PetCollection.SellPriceFor(record.SpeciesId);
            string message;
            int paid;
            bool sold = PetCollection.Sell(recordId, out message, out paid);

            if (!sold) return new Result { Message = message, Error = true };
            _ = price;
            return new Result { Message = message + "（宠物币可以在地图里换新房间）" };
        }

        /// <summary>What the shop would pay for this pet, for the button's label.</summary>
        public static string SellLabel(string speciesId)
            => "卖掉 " + PetCollection.SellPriceFor(speciesId);

        /// <summary>
        /// The advisory the panel shows above the backpack.
        ///
        /// The player is about to make a choice that costs them money in tokens, and nothing in
        /// the interface would otherwise say so: three pets in the room is three characters the
        /// model has to keep coherent on every single line.
        /// </summary>
        public static string TokenAdvice(int backpackCount)
        {
            if (backpackCount <= 1) return "建议只放一只：多只宠物同场更热闹，但每次对话消耗的 token 也更多。";
            return $"背包里有 {backpackCount} 只：多宠同场更热闹，但每次对话的 token 消耗也会成倍增加。";
        }

        /// <summary>Short label for what a pet is doing for the player, per tab.</summary>
        public static string RoleLabel(bool inBackpack, bool primary)
        {
            if (primary && inBackpack) return "在家里 · 主要照顾";
            if (primary) return "主要照顾（不在房间里）";
            if (inBackpack) return "在家里";
            return "在仓库";
        }

        public static CollectionTab Tab
        {
            get => (CollectionTab)Mathf.Clamp(PlayerPrefs.GetInt(TabPrefKey, 0), 0, 2);
            set
            {
                PlayerPrefs.SetInt(TabPrefKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static string TabLabel(CollectionTab tab)
        {
            switch (tab)
            {
                case CollectionTab.Shop: return "商城";
                case CollectionTab.Warehouse: return "仓库";
                default: return "背包";
            }
        }

        /// <summary>
        /// Species available in the shop, cheapest first.
        ///
        /// Sorted so the first thing a new player sees is something they can nearly afford:
        /// a list ordered by the species table puts the 680-coin bear first and makes the whole
        /// shop look out of reach.
        /// </summary>
        public static List<PetSpecies> ShopOrder()
        {
            var list = new List<PetSpecies>(PetSpecies.All);
            list.Sort((a, b) => PetCollection.PriceFor(a.Id).CompareTo(PetCollection.PriceFor(b.Id)));
            return list;
        }
    }
}
