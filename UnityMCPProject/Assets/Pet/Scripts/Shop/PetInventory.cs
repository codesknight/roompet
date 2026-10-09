using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// What the player owns and where it is: the warehouse, the pantry and the room's layout.
    ///
    /// State, all in PlayerPrefs like every other save in the project:
    ///  - a *count* per stackable item (food, water, meat, apple, fish);
    ///  - the owned tools and furniture (one of each);
    ///  - which furniture is placed, where, and whether it belongs in the current place.
    ///
    /// A pure state machine with string-backed persistence, so the whole economy — buying,
    /// selling, placing, eating, watering — is unit testable without a scene.
    /// </summary>
    public static class PetInventory
    {
        public const string OwnedKey = "dshpet.furnish.owned";
        public const string PlacedKey = "dshpet.furnish.placed";
        public const string CountKey = "dshpet.items.count";

        /// <summary>How many slots the warehouse holds. Reported, and enforced lightly.</summary>
        public const int WarehouseSlots = 99;

        /// <summary>Raised when the warehouse changes, so an open panel can refresh.</summary>
        public static event Action Changed;

        // ------------------------------------------------------------------- stackable items

        /// <summary>How many of a stackable item are held (food, water, meat, apple, fish).</summary>
        public static int Count(string id)
        {
            string raw = PlayerPrefs.GetString(CountKey, "");
            foreach (string entry in raw.Split(';'))
            {
                int colon = entry.IndexOf(':');
                if (colon <= 0) continue;
                if (entry.Substring(0, colon) != id) continue;
                int value;
                if (int.TryParse(entry.Substring(colon + 1), out value)) return Mathf.Max(0, value);
            }
            return 0;
        }

        public static void SetCount(string id, int amount)
        {
            var counts = AllCounts();
            counts[id] = Mathf.Max(0, amount);
            SaveCounts(counts);
        }

        public static void Add(string id, int amount)
        {
            if (amount <= 0) return;
            SetCount(id, Count(id) + amount);
        }

        private static Dictionary<string, int> AllCounts()
        {
            var counts = new Dictionary<string, int>();
            string raw = PlayerPrefs.GetString(CountKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string entry in raw.Split(';'))
                {
                    int colon = entry.IndexOf(':');
                    if (colon <= 0) continue;
                    int value;
                    if (int.TryParse(entry.Substring(colon + 1), out value) && value > 0)
                    {
                        counts[entry.Substring(0, colon)] = value;
                    }
                }
            }
            return counts;
        }

        private static void SaveCounts(Dictionary<string, int> counts)
        {
            var parts = new List<string>();
            foreach (var pair in counts)
            {
                if (pair.Value > 0) parts.Add(pair.Key + ":" + pair.Value);
            }
            parts.Sort();
            PlayerPrefs.SetString(CountKey, string.Join(";", parts.ToArray()));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Meals of kibble in the pantry, for the food bowl.</summary>
        public static int Food
        {
            get => Count("food");
            private set => SetCount("food", value);
        }

        public static void AddFood(int units) { if (units > 0) Food += units; }

        public static bool HasFood => Food > 0;

        /// <summary>Spends one meal, or fails and leaves the pantry alone.</summary>
        public static bool TryConsumeMeal()
        {
            if (Food <= 0) return false;
            Food -= 1;
            return true;
        }

        /// <summary>True when the player holds at least one of a food item.</summary>
        public static bool Has(string id) => Count(id) > 0;

        // ------------------------------------------------------------------- owned

        /// <summary>The ids of the tools and furniture in the warehouse.</summary>
        public static List<string> Owned()
        {
            var list = new List<string>();
            string raw = PlayerPrefs.GetString(OwnedKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string part in raw.Split(','))
                {
                    if (!string.IsNullOrEmpty(part) && !list.Contains(part)) list.Add(part);
                }
            }
            return list;
        }

        public static bool IsOwned(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (PetShop.IsStarter(id)) return true;
            return Owned().Contains(id);
        }

        private static void AddOwned(string id)
        {
            var owned = Owned();
            if (!owned.Contains(id)) owned.Add(id);
            owned.Sort();
            PlayerPrefs.SetString(OwnedKey, string.Join(",", owned.ToArray()));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void RemoveOwned(string id)
        {
            var owned = Owned();
            if (owned.Remove(id))
            {
                PlayerPrefs.SetString(OwnedKey, string.Join(",", owned.ToArray()));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        // ------------------------------------------------------------------- placed

        /// <summary>The placed furniture and its positions, as a map.</summary>
        public static Dictionary<string, Vector2> Placed()
        {
            var map = new Dictionary<string, Vector2>();
            string raw = PlayerPrefs.GetString(PlacedKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string entry in raw.Split(';'))
                {
                    if (string.IsNullOrEmpty(entry)) continue;
                    int colon = entry.IndexOf(':');
                    if (colon <= 0) continue;
                    string id = entry.Substring(0, colon);
                    string[] parts = entry.Substring(colon + 1).Split('|');
                    if (parts.Length != 2) continue;

                    float x, z;
                    if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) continue;
                    if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) continue;
                    map[id] = new Vector2(x, z);
                }
            }

            if (!map.ContainsKey(PetShop.FoodBowl)) map[PetShop.FoodBowl] = new Vector2(3.4f, 3.2f);
            if (!map.ContainsKey(PetShop.WaterBowl)) map[PetShop.WaterBowl] = new Vector2(4.6f, 3.2f);
            return map;
        }

        private static void SavePlaced(Dictionary<string, Vector2> map)
        {
            var parts = new List<string>();
            foreach (var pair in map)
            {
                parts.Add(pair.Key + ":" +
                         pair.Value.x.ToString("R", CultureInfo.InvariantCulture) + "|" +
                         pair.Value.y.ToString("R", CultureInfo.InvariantCulture));
            }
            parts.Sort();
            PlayerPrefs.SetString(PlacedKey, string.Join(";", parts.ToArray()));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool IsPlaced(string id) => Placed().ContainsKey(id);

        public static Vector2 PositionOf(string id)
        {
            Vector2 at;
            var map = Placed();
            if (map.TryGetValue(id, out at)) return at;

            var item = PetShop.Get(id);
            return item != null ? item.DefaultPosition : Vector2.zero;
        }

        public const float RoomHalf = 7f;
        public const float PlacementMargin = 0.9f;

        public static Vector2 ClampToRoom(Vector2 position)
        {
            float limit = RoomHalf - PlacementMargin;
            return new Vector2(
                Mathf.Clamp(position.x, -limit, limit),
                Mathf.Clamp(position.y, -limit, limit));
        }

        public static void MoveItem(string id, Vector2 position)
        {
            if (!IsPlaced(id)) return;
            var map = Placed();
            map[id] = ClampToRoom(position);
            SavePlaced(map);
        }

        // ------------------------------------------------------------------- buy / sell / place

        /// <summary>Buys a shop item. Returns a player-facing message.</summary>
        public static string Buy(ShopItem item)
        {
            if (item == null) return "没有这件商品";

            if (item.IsFood)
            {
                if (!DshMobile.PetWallet.TrySpend(item.Price))
                    return $"还差 {item.Price - DshMobile.PetWallet.Coins} 个宠物币";
                Add(item.Id, item.FoodUnits);
                return $"买了一袋{item.Name}（够吃 {item.FoodUnits} 顿）";
            }

            if (IsOwned(item.Id)) return item.Name + "已经买过了";
            if (!DshMobile.PetWallet.TrySpend(item.Price))
                return $"还差 {item.Price - DshMobile.PetWallet.Coins} 个宠物币";

            AddOwned(item.Id);
            return item.IsTool
                ? $"买下了{item.Name}，去背包把它装备上"
                : $"买下了{item.Name}，去仓库把它摆进房间吧";
        }

        /// <summary>Puts owned furniture into the room, if it belongs in this place.</summary>
        public static string Place(string id, RoomTheme place)
        {
            if (PetShop.IsStarter(id)) return "这是基础家具，一直都在房间里";
            var item = PetShop.Get(id);
            if (item == null) return "没有这件商品";
            if (!item.IsFurniture) return item.Name + "不是家具，不用摆放";
            if (!IsOwned(id)) return "还没有这件家具，先去商城买";
            if (IsPlaced(id)) return "已经摆在房间里了";
            if (!item.AllowedIn(place)) return item.Name + "只能摆在" + PlaceName(item.Scene);

            var map = Placed();
            map[id] = item.DefaultPosition;
            SavePlaced(map);
            return item.Name + "摆进了房间";
        }

        /// <summary>Picks furniture up off the floor and returns it to the warehouse.</summary>
        public static string Store(string id)
        {
            if (PetShop.IsStarter(id)) return "基础家具不能收起来";
            if (!IsPlaced(id)) return "它没在房间里";

            var map = Placed();
            map.Remove(id);
            SavePlaced(map);
            return "收回了仓库";
        }

        /// <summary>
        /// Sells one unit of a stackable food, or a whole tool / furniture. Starter bowls and an
        /// equipped tool cannot be sold (the tool has to come out of the backpack first).
        /// </summary>
        public static string Sell(string id)
        {
            if (PetShop.IsStarter(id)) return "基础家具不能卖";
            var item = PetShop.Get(id);
            if (item == null) return "没有这件商品";

            if (item.IsFood)
            {
                if (Count(id) <= 0) return "没有多余的" + item.Name + "可以卖";
                SetCount(id, Count(id) - 1);
                DshMobile.PetWallet.Add(item.SellPrice);
                return $"卖掉了{item.Name}，赚了 {item.SellPrice} 个宠物币";
            }

            if (item.IsTool)
            {
                if (PetBackpack.IsEquipped(id)) return "先把" + item.Name + "从背包里取下来再卖";
                if (!IsOwned(id)) return "还没有" + item.Name;
                RemoveOwned(id);
                DshMobile.PetWallet.Add(item.SellPrice);
                return $"卖掉了{item.Name}，返还 {item.SellPrice} 个宠物币";
            }

            if (!IsOwned(id)) return "还没有这件家具";
            var map = Placed();
            map.Remove(id);
            SavePlaced(map);
            RemoveOwned(id);
            DshMobile.PetWallet.Add(item.SellPrice);
            return $"卖掉了{item.Name}，返还 {item.SellPrice} 个宠物币";
        }

        /// <summary>The Chinese name of a place, for "only allowed in …" messages.</summary>
        public static string PlaceName(ItemScene scene)
        {
            switch (scene)
            {
                case ItemScene.Garden: return "花园";
                case ItemScene.Terrace: return "夜晚露台";
                default: return "任何地方";
            }
        }

        // ------------------------------------------------------------------- tests

        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(OwnedKey);
            PlayerPrefs.DeleteKey(PlacedKey);
            PlayerPrefs.DeleteKey(CountKey);
            PetBackpack.ResetForTests();
        }
    }
}
