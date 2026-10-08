using System;
using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>The places the pet can live. One room, built differently.</summary>
    public enum RoomTheme
    {
        Cabin = 0,   // 小屋: the original warm room
        Garden = 1,  // 花园: open air, plants, softer light
        Terrace = 2  // 夜晚露台: lanterns, night sky, wind
    }

    /// <summary>
    /// Everything that makes one place different from another.
    ///
    /// Three .unity files would be the obvious way to do "more scenes", and it is the wrong one
    /// here: the room is generated from code, the furniture is generated from code, and every
    /// save lives in PlayerPrefs rather than in scene objects. Three scenes would mean three
    /// copies of the whole rig to keep in step. A theme is a palette plus a handful of numbers,
    /// and switching places is a rebuild — which is exactly what the editor menu already does.
    ///
    /// The need modifiers are the interesting half: a garden makes a pet happier and dirtier,
    /// a night terrace tires it out faster. That is what "different scenes activate different
    /// pet states" means in practice.
    /// </summary>
    public class RoomThemeInfo
    {
        public RoomTheme Theme;
        public string DisplayName = "";
        public string Blurb = "";
        public int Price;
        public string Emoji = "🏠";

        // palette
        public Color Floor;
        public Color Wall;
        public Color AccentWall;
        public Color Rug;
        public Color Light;      // ambient tint, so the whole place reads differently

        // how the place treats the pet
        public float JoyDrainScale = 1f;
        public float EnergyDrainScale = 1f;
        public float CleanDrainScale = 1f;
        public bool Outdoors;

        public string Effects()
        {
            var parts = new List<string>();
            if (JoyDrainScale < 0.98f) parts.Add("心情掉得更慢");
            else if (JoyDrainScale > 1.02f) parts.Add("心情掉得更快");
            if (EnergyDrainScale < 0.98f) parts.Add("精力更耐用");
            else if (EnergyDrainScale > 1.02f) parts.Add("更容易累");
            if (CleanDrainScale > 1.02f) parts.Add("更容易脏");
            else if (CleanDrainScale < 0.98f) parts.Add("不容易脏");
            if (Outdoors) parts.Add("在户外");
            return parts.Count == 0 ? "和平时一样" : string.Join("、", parts.ToArray());
        }

        public static readonly RoomThemeInfo[] All =
        {
            new RoomThemeInfo
            {
                Theme = RoomTheme.Cabin, DisplayName = "小屋", Emoji = "🏠", Price = 0,
                Blurb = "最初的房间，暖木地板和一条旧地毯。",
                Floor = new Color(0.62f, 0.47f, 0.36f),
                Wall = new Color(0.86f, 0.82f, 0.76f),
                AccentWall = new Color(0.55f, 0.70f, 0.78f),
                Rug = new Color(0.78f, 0.35f, 0.38f),
                Light = new Color(1f, 0.98f, 0.94f)
            },
            new RoomThemeInfo
            {
                Theme = RoomTheme.Garden, DisplayName = "花园", Emoji = "🌿", Price = 500,
                Blurb = "草地、花草和石头。宠物在这里更开心，但也更容易玩脏。",
                Floor = new Color(0.42f, 0.62f, 0.36f),
                Wall = new Color(0.58f, 0.72f, 0.55f),
                AccentWall = new Color(0.72f, 0.80f, 0.62f),
                Rug = new Color(0.86f, 0.78f, 0.52f),
                Light = new Color(1f, 1f, 0.92f),
                JoyDrainScale = 0.78f,
                CleanDrainScale = 1.45f,
                EnergyDrainScale = 1.12f,
                Outdoors = true
            },
            new RoomThemeInfo
            {
                Theme = RoomTheme.Terrace, DisplayName = "夜晚露台", Emoji = "🌙", Price = 900,
                Blurb = "灯笼、夜色和凉风。宠物在这里更容易犯困。",
                Floor = new Color(0.30f, 0.32f, 0.42f),
                Wall = new Color(0.34f, 0.36f, 0.50f),
                AccentWall = new Color(0.24f, 0.26f, 0.38f),
                Rug = new Color(0.52f, 0.42f, 0.66f),
                Light = new Color(0.78f, 0.82f, 1f),
                EnergyDrainScale = 1.30f,
                JoyDrainScale = 0.92f,
                Outdoors = true
            }
        };

        public static RoomThemeInfo Get(RoomTheme theme)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Theme == theme) return All[i];
            }
            return All[0];
        }
    }

    /// <summary>
    /// Which places are unlocked, and which one the pet lives in.
    ///
    /// Unlocking costs pet coins, which is what makes the runner worth playing beyond its own
    /// scoreboard: the map is the shop window for the whole world.
    /// </summary>
    public static class PetWorldMap
    {
        public const string UnlockedKey = "dshpet.world.unlocked";
        public const string CurrentKey = "dshpet.world.current";

        public static event Action Changed;

        /// <summary>Comma-separated theme ids, e.g. "0,1".</summary>
        private static List<int> UnlockedList
        {
            get
            {
                var list = new List<int>();
                string raw = PlayerPrefs.GetString(UnlockedKey, "0");
                foreach (string part in raw.Split(','))
                {
                    int value;
                    if (int.TryParse(part, out value) && !list.Contains(value)) list.Add(value);
                }
                // The cabin is always available: a save with nowhere to live is not a save.
                if (!list.Contains(0)) list.Add(0);
                return list;
            }
        }

        public static bool IsUnlocked(RoomTheme theme)
        {
            if (theme == RoomTheme.Cabin) return true;
            return UnlockedList.Contains((int)theme);
        }

        public static RoomTheme Current
        {
            get
            {
                int value = PlayerPrefs.GetInt(CurrentKey, 0);
                var theme = (RoomTheme)Mathf.Clamp(value, 0, RoomThemeInfo.All.Length - 1);
                return IsUnlocked(theme) ? theme : RoomTheme.Cabin;
            }
            set
            {
                if (!IsUnlocked(value)) return;
                PlayerPrefs.SetInt(CurrentKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Buys a place. Returns false and leaves everything alone if it cannot.</summary>
        public static bool TryUnlock(RoomTheme theme, out string message)
        {
            var info = RoomThemeInfo.Get(theme);
            if (IsUnlocked(theme))
            {
                message = info.DisplayName + "已经解锁了";
                return false;
            }

            if (!DshMobile.PetWallet.TrySpend(info.Price))
            {
                message = $"还差 {info.Price - DshMobile.PetWallet.Coins} 个宠物币";
                return false;
            }

            var list = UnlockedList;
            list.Add((int)theme);
            list.Sort();
            var parts = new List<string>();
            foreach (int value in list) parts.Add(value.ToString());
            PlayerPrefs.SetString(UnlockedKey, string.Join(",", parts.ToArray()));
            PlayerPrefs.Save();

            Changed?.Invoke();
            message = $"解锁了{info.DisplayName}";
            return true;
        }

        /// <summary>Moves the pet, if the place is unlocked.</summary>
        public static bool TravelTo(RoomTheme theme, out string message)
        {
            var info = RoomThemeInfo.Get(theme);
            if (!IsUnlocked(theme))
            {
                message = info.DisplayName + "还没解锁";
                return false;
            }

            if (Current == theme)
            {
                message = "已经在这里了";
                return false;
            }

            Current = theme;
            message = $"来到了{info.DisplayName}";
            return true;
        }

        public static void ResetAll()
        {
            PlayerPrefs.SetString(UnlockedKey, "0");
            PlayerPrefs.SetInt(CurrentKey, 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Replaces the map state, for tests.</summary>
        public static void SetForTests(RoomTheme current, params RoomTheme[] unlocked)
        {
            var parts = new List<string> { "0" };
            if (unlocked != null)
            {
                foreach (var theme in unlocked)
                {
                    string value = ((int)theme).ToString();
                    if (!parts.Contains(value)) parts.Add(value);
                }
            }
            PlayerPrefs.SetString(UnlockedKey, string.Join(",", parts.ToArray()));
            PlayerPrefs.SetInt(CurrentKey, (int)current);
            PlayerPrefs.Save();
        }
    }
}
