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
        public const string StoredKey = "dshpet.furnish.stored";

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

        /// <summary>
        /// Filling the bucket at the pond fills the bucket AND puts one bottle of water in the
        /// warehouse. The water is a real stackable item, not just a flag on the bucket, so it
        /// shows up in the warehouse and can be fed or sold like anything else.
        /// </summary>
        public static void FillBucketFromPond()
        {
            PetBackpack.BucketFull = true;
            Add("water", 1);
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
            if (PetShop.IsDefault(id)) return true;
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

        // ------------------------------------------------------------------- placed (per scene)

        /// <summary>
        /// The furniture placed in ONE scene, with its positions.
        ///
        /// Each place is its own space now: the garden has its own layout, the terrace its own,
        /// and moving between them does not carry furniture over — a garden is a different room,
        /// not a repaint of the same one. (Ownership is still global: you own the bed once, but
        /// it is only *set down* in the scene you placed it in.)
        /// </summary>
        public static Dictionary<string, Vector2> Placed(RoomTheme theme)
        {
            var map = new Dictionary<string, Vector2>();
            string raw = PlayerPrefs.GetString(SceneKey(theme), "");
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

            // The two bowls exist in every scene: a place without food or water is not a place.
            if (!map.ContainsKey(PetShop.FoodBowl)) map[PetShop.FoodBowl] = new Vector2(3.4f, 3.2f);
            if (!map.ContainsKey(PetShop.WaterBowl)) map[PetShop.WaterBowl] = new Vector2(4.6f, 3.2f);

            // Default furniture is placed in its own scene until the player stores it. The
            // "stored" list is the only thing that distinguishes "not placed yet" (default it in)
            // from "put back in the warehouse" (leave it out) — the placed map alone cannot tell.
            var stored = StoredStarters();
            for (int i = 0; i < PetShop.All.Length; i++)
            {
                var item = PetShop.All[i];
                if (item == null || !item.Starter) continue;
                if (!item.AllowedIn(theme)) continue;
                if (stored.Contains(item.Id)) continue;
                if (map.ContainsKey(item.Id)) continue;
                map[item.Id] = item.DefaultPosition;
            }
            return map;
        }

        /// <summary>The default-furniture ids the player has put back in the warehouse.</summary>
        private static List<string> StoredStarters()
        {
            var list = new List<string>();
            string raw = PlayerPrefs.GetString(StoredKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string part in raw.Split(','))
                {
                    if (!string.IsNullOrEmpty(part) && !list.Contains(part)) list.Add(part);
                }
            }
            return list;
        }

        private static void SetStored(string id, bool stored)
        {
            var list = StoredStarters();
            if (stored) { if (!list.Contains(id)) list.Add(id); }
            else list.Remove(id);
            PlayerPrefs.SetString(StoredKey, string.Join(",", list.ToArray()));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static string SceneKey(RoomTheme theme) => PlacedKey + "." + (int)theme;

        private static void SavePlaced(RoomTheme theme, Dictionary<string, Vector2> map)
        {
            var parts = new List<string>();
            foreach (var pair in map)
            {
                parts.Add(pair.Key + ":" +
                         pair.Value.x.ToString("R", CultureInfo.InvariantCulture) + "|" +
                         pair.Value.y.ToString("R", CultureInfo.InvariantCulture));
            }
            parts.Sort();
            PlayerPrefs.SetString(SceneKey(theme), string.Join(";", parts.ToArray()));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool IsPlaced(string id, RoomTheme theme) => Placed(theme).ContainsKey(id);

        public static Vector2 PositionOf(string id, RoomTheme theme)
        {
            Vector2 at;
            var map = Placed(theme);
            if (map.TryGetValue(id, out at)) return at;

            var item = PetShop.Get(id);
            return item != null ? item.DefaultPosition : Vector2.zero;
        }

        /// <summary>The scenes where a piece of furniture is currently set down, for the UI.</summary>
        public static List<RoomTheme> ScenesWherePlaced(string id)
        {
            var list = new List<RoomTheme>();
            for (int i = 0; i < RoomThemeInfo.All.Length; i++)
            {
                var theme = RoomThemeInfo.All[i].Theme;
                if (Placed(theme).ContainsKey(id)) list.Add(theme);
            }
            return list;
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

        /// <summary>The overlap radius of a placed item, so furniture does not sit inside furniture.</summary>
        public static float FootprintOf(string id)
        {
            if (id == PetShop.FoodBowl || id == PetShop.WaterBowl) return 0.55f;
            var item = PetShop.Get(id);
            if (item != null && item.Footprint > 0f) return item.Footprint;
            return 0.7f;
        }

        /// <summary>Whether a position overlaps a piece of furniture already set down in the scene.</summary>
        public static bool Overlaps(Vector2 position, float radius, RoomTheme theme, string ignoreId = null)
        {
            var map = Placed(theme);
            foreach (var pair in map)
            {
                if (pair.Key == ignoreId) continue;
                if (Vector2.Distance(position, pair.Value) < radius + FootprintOf(pair.Key)) return true;
            }
            return false;
        }

        /// <summary>
        /// Clamps a placement into the room, then pushes it clear of every other piece of furniture.
        /// The bowls are ordinary obstacles here, which is exactly "食物盆/水盆不和其他物体重叠".
        /// </summary>
        public static Vector2 ResolveOverlap(Vector2 position, float radius, RoomTheme theme, string ignoreId = null)
        {
            Vector2 result = ClampToRoom(position);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                bool pushed = false;
                var map = Placed(theme);
                foreach (var pair in map)
                {
                    if (pair.Key == ignoreId) continue;
                    float minDist = radius + FootprintOf(pair.Key);
                    if (Vector2.Distance(result, pair.Value) >= minDist) continue;

                    Vector2 dir = result - pair.Value;
                    if (dir.sqrMagnitude < 0.0001f)
                    {
                        // Exactly on top: push toward the room centre, which is the direction with
                        // the most open floor — not a fixed axis that may be blocked by a wall.
                        dir = new Vector2(-pair.Value.x, -pair.Value.y);
                        if (dir.sqrMagnitude < 0.0001f) dir = new Vector2(1f, 0f);
                    }
                    dir.Normalize();
                    // Push a touch past the minimum, so a float-rounding boundary never leaves two
                    // pieces of furniture "exactly touching" and thus reported as overlapping.
                    result = ClampToRoom(pair.Value + dir * (minDist + 0.05f));
                    pushed = true;
                }
                if (!pushed) break;
            }
            return result;
        }

        public static void MoveItem(string id, Vector2 position, RoomTheme theme)
        {
            if (!IsPlaced(id, theme)) return;
            var map = Placed(theme);
            map[id] = ResolveOverlap(position, FootprintOf(id), theme, id);
            SavePlaced(theme, map);
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

        /// <summary>Puts owned furniture into this scene, if it belongs here.</summary>
        public static string Place(string id, RoomTheme place)
        {
            if (PetShop.IsStarter(id)) return "这是基础家具，一直都在房间里";
            var item = PetShop.Get(id);
            if (item == null) return "没有这件商品";
            if (!item.IsFurniture) return item.Name + "不是家具，不用摆放";
            if (!IsOwned(id)) return "还没有这件家具，先去商城买";
            if (IsPlaced(id, place)) return "已经摆在这里了";
            if (!item.AllowedIn(place)) return item.Name + "只能摆在" + PlaceName(item.Scene);

            var map = Placed(place);
            map[id] = ResolveOverlap(item.DefaultPosition, item.Footprint, place, id);
            SavePlaced(place, map);

            if (item.Starter) SetStored(id, false);
            return item.Name + "摆进了" + RoomThemeInfo.Get(place).DisplayName;
        }

        /// <summary>Picks furniture up off this scene's floor and returns it to the warehouse.</summary>
        public static string Store(string id, RoomTheme place)
        {
            if (PetShop.IsStarter(id)) return "基础家具不能收起来";
            if (!IsPlaced(id, place)) return "它没在这个地方";

            var map = Placed(place);
            map.Remove(id);
            SavePlaced(place, map);

            var item = PetShop.Get(id);
            if (item != null && item.Starter) SetStored(id, true);
            return "收回了仓库";
        }

        /// <summary>
        /// Sells one unit of a stackable food, or a whole tool / furniture. Starter bowls and an
        /// equipped tool cannot be sold (the tool has to come out of the backpack first), and the
        /// room's default furniture cannot be sold — it can only be stored, so the default room is
        /// always restorable.
        /// </summary>
        public static string Sell(string id)
        {
            if (PetShop.IsStarter(id)) return "基础家具不能卖";
            var item = PetShop.Get(id);
            if (item == null) return "没有这件商品";
            if (item.Starter) return "默认家具不能卖，只能收回仓库";

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

            // Selling removes the furniture from every scene it was set down in.
            for (int i = 0; i < RoomThemeInfo.All.Length; i++)
            {
                var theme = RoomThemeInfo.All[i].Theme;
                var map = Placed(theme);
                if (map.Remove(id)) SavePlaced(theme, map);
            }

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
                case ItemScene.Cabin: return "小屋";
                default: return "任何地方";
            }
        }

        // ------------------------------------------------------------------- tests

        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(OwnedKey);
            PlayerPrefs.DeleteKey(CountKey);
            PlayerPrefs.DeleteKey(StoredKey);
            for (int i = 0; i < RoomThemeInfo.All.Length; i++)
            {
                PlayerPrefs.DeleteKey(PlacedKey + "." + (int)RoomThemeInfo.All[i].Theme);
            }
            PetBackpack.ResetForTests();
        }
    }
}
