using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// What the player owns and where it is: the warehouse.
    ///
    /// Three pieces of state, all in PlayerPrefs like every other save in the project:
    ///  - <see cref="Food"/>: meals in the pantry, bought in bags and consumed one per feed;
    ///  - the owned furniture (the warehouse proper);
    ///  - which owned furniture is placed, and where (the room's layout).
    ///
    /// A pure state machine with a string-backed persistence, so buying, selling, placing and
    /// the food economy can all be unit tested without a scene — which is the point of the
    /// request that every shop item do something: the *something* has to be a rule, not a hope.
    /// </summary>
    public static class PetInventory
    {
        public const string FoodKey = "dshpet.furnish.food";
        public const string OwnedKey = "dshpet.furnish.owned";
        public const string PlacedKey = "dshpet.furnish.placed";

        /// <summary>Raised when the warehouse changes, so an open panel can refresh.</summary>
        public static event Action Changed;

        // ------------------------------------------------------------------- food

        public static int Food
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(FoodKey, 0));
            private set
            {
                PlayerPrefs.SetInt(FoodKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static void AddFood(int units) { if (units > 0) Food += units; }

        /// <summary>True while there is at least one meal left for the bowl.</summary>
        public static bool HasFood => Food > 0;

        /// <summary>Spends one meal, or fails and leaves the pantry alone.</summary>
        public static bool TryConsumeMeal()
        {
            if (Food <= 0) return false;
            Food -= 1;
            return true;
        }

        // ------------------------------------------------------------------- owned

        /// <summary>The ids of the furniture in the warehouse (food is not furniture).</summary>
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
            var owned = Owned();
            return owned.Contains(id);
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

        /// <summary>The placed items and their positions, as a map.</summary>
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
                    string coords = entry.Substring(colon + 1);
                    string[] parts = coords.Split('|');
                    if (parts.Length != 2) continue;

                    float x, z;
                    if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) continue;
                    if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) continue;
                    map[id] = new Vector2(x, z);
                }
            }

            // The two starter bowls are always placed; only their position is remembered.
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

        /// <summary>The room position of a placed item, or its default if never moved.</summary>
        public static Vector2 PositionOf(string id)
        {
            var map = Placed();
            Vector2 at;
            if (map.TryGetValue(id, out at)) return at;

            var item = PetShop.Get(id);
            return item != null ? item.DefaultPosition : Vector2.zero;
        }

        /// <summary>Where the pet stands to use a placed item.</summary>
        public static Vector2 ApproachPointOf(string id)
        {
            var item = PetShop.Get(id);
            Vector2 offset = item != null ? item.ApproachOffset : Vector2.zero;
            if (PetShop.IsStarter(id))
            {
                offset = new Vector2(0f, -1.2f);
            }
            return PositionOf(id) + offset;
        }

        /// <summary>
        /// The room's walkable half-extent, minus a margin so a placed item cannot end up inside
        /// a wall or behind the fence.
        /// </summary>
        public const float RoomHalf = 7f;
        public const float PlacementMargin = 0.9f;

        public static Vector2 ClampToRoom(Vector2 position)
        {
            float limit = RoomHalf - PlacementMargin;
            return new Vector2(
                Mathf.Clamp(position.x, -limit, limit),
                Mathf.Clamp(position.y, -limit, limit));
        }

        /// <summary>Sets a placed item down in the room. Starter bowls can also be moved.</summary>
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
                AddFood(item.FoodUnits);
                return $"买了一袋{item.Name}（够吃 {item.FoodUnits} 顿）";
            }

            if (IsOwned(item.Id)) return item.Name + "已经买过了";
            if (!DshMobile.PetWallet.TrySpend(item.Price))
                return $"还差 {item.Price - DshMobile.PetWallet.Coins} 个宠物币";

            AddOwned(item.Id);
            return "买下了" + item.Name + "，去仓库把它摆进房间吧";
        }

        /// <summary>Puts owned furniture into the room at its default spot.</summary>
        public static string Place(string id)
        {
            if (PetShop.IsStarter(id)) return "这是基础家具，一直都在房间里";
            if (!IsOwned(id)) return "还没有这件家具，先去商城买";
            if (IsPlaced(id)) return "已经摆在房间里了";

            var map = Placed();
            var item = PetShop.Get(id);
            map[id] = item != null ? item.DefaultPosition : Vector2.zero;
            SavePlaced(map);
            return item != null ? item.Name + "摆进了房间" : "摆好了";
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

        /// <summary>Sells owned furniture for half its price. Starter bowls cannot be sold.</summary>
        public static string Sell(string id)
        {
            if (PetShop.IsStarter(id)) return "基础家具不能卖";
            var item = PetShop.Get(id);
            if (item == null) return "没有这件商品";
            if (!IsOwned(id)) return "还没有这件家具";

            var map = Placed();
            map.Remove(id);
            SavePlaced(map);
            RemoveOwned(id);
            DshMobile.PetWallet.Add(item.SellPrice);
            return $"卖掉了{item.Name}，返还 {item.SellPrice} 个宠物币";
        }

        // ------------------------------------------------------------------- tests

        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(FoodKey);
            PlayerPrefs.DeleteKey(OwnedKey);
            PlayerPrefs.DeleteKey(PlacedKey);
        }
    }
}
