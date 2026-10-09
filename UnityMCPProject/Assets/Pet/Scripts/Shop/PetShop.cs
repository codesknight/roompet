using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>Whether a shop item is a consumable (food) or a piece of furniture.</summary>
    public enum ShopCategory
    {
        /// <summary>宠物粮食: a consumable that fills the bowl, several meals per bag.</summary>
        Food = 0,

        /// <summary>Furniture: bought once, stored in the warehouse, then placed in the room.</summary>
        Prop = 1
    }

    /// <summary>
    /// One thing the shop sells.
    ///
    /// Every entry is data, not code, because "every shop item has its own special interaction"
    /// is exactly the kind of claim that has to be readable in one place: the name, the price,
    /// the one-line pitch, what interactable it places, and where the pet stands to use it.
    /// </summary>
    public class ShopItem
    {
        public string Id = "";
        public string Name = "";
        public string Emoji = "";

        /// <summary>Price in pet coins.</summary>
        public int Price;

        /// <summary>The one-line pitch shown on the buy button's row.</summary>
        public string Blurb = "";

        public ShopCategory Category = ShopCategory.Prop;

        /// <summary>
        /// The interactable this item places when it is set down. Null for food, which is not a
        /// thing in the room but a stock the pet eats out of.
        /// </summary>
        public InteractableKind? Kind;

        /// <summary>Where a freshly placed item starts, in room metres (x, z).</summary>
        public Vector2 DefaultPosition;

        /// <summary>Where the pet stands to use it, relative to the item.</summary>
        public Vector2 ApproachOffset;

        /// <summary>How many meals one bag of food is worth.</summary>
        public int FoodUnits;

        /// <summary>What you get back for selling it, half the price rounded down.</summary>
        public int SellPrice => Price / 2;

        public bool IsFood => Category == ShopCategory.Food;
    }

    /// <summary>
    /// The shop catalogue, and the item ids that the room and the warehouse both key on.
    ///
    /// The two bowls are not in the catalogue: they are starter furniture, always owned and
    /// always placed, which is what "the default room has only the pet and two bowls" means.
    /// Everything else has to be bought here.
    /// </summary>
    public static class PetShop
    {
        /// <summary>The two bowls every room starts with. Not sellable, but movable.</summary>
        public const string FoodBowl = "food_bowl";
        public const string WaterBowl = "water_bowl";

        public static readonly ShopItem[] All =
        {
            new ShopItem
            {
                Id = "food", Name = "宠物粮食", Emoji = "🦴", Price = 20, Category = ShopCategory.Food,
                Blurb = "一袋能吃 3 顿。饭碗空了宠物会挨饿，记得补货。", FoodUnits = 3
            },
            new ShopItem
            {
                Id = "litter_box", Name = "猫砂盆", Emoji = "🚽", Price = 150, Category = ShopCategory.Prop,
                Kind = InteractableKind.Toilet,
                Blurb = "有了它宠物会自己去上厕所，每用一次还自动给你赚金币；没有它……你就得擦地板了。",
                DefaultPosition = new Vector2(5.1f, -5.0f), ApproachOffset = new Vector2(0f, 1.4f)
            },
            new ShopItem
            {
                Id = "bed", Name = "小床", Emoji = "🛏️", Price = 120, Category = ShopCategory.Prop,
                Kind = InteractableKind.Bed,
                Blurb = "困了会自己爬上去睡，睡醒精力满满。",
                DefaultPosition = new Vector2(-4.4f, -4.4f), ApproachOffset = new Vector2(0f, 1.5f)
            },
            new ShopItem
            {
                Id = "ball", Name = "皮球", Emoji = "⚽", Price = 60, Category = ShopCategory.Prop,
                Kind = InteractableKind.Ball,
                Blurb = "扔出去宠物会追着捡回来，是最快的陪玩。",
                DefaultPosition = new Vector2(1.8f, 4.2f), ApproachOffset = new Vector2(0f, -1.2f)
            },
            new ShopItem
            {
                Id = "brush", Name = "梳子", Emoji = "🪮", Price = 40, Category = ShopCategory.Prop,
                Kind = InteractableKind.Brush,
                Blurb = "玩脏了就梳梳毛，干净又开心。",
                DefaultPosition = new Vector2(-1.6f, 4.6f), ApproachOffset = new Vector2(0f, -1.2f)
            },
            new ShopItem
            {
                Id = "toy", Name = "玩具", Emoji = "🧸", Price = 80, Category = ShopCategory.Prop,
                Kind = InteractableKind.Toy,
                Blurb = "一个小玩具，宠物会自己扑过去玩。",
                DefaultPosition = new Vector2(0f, -2.5f), ApproachOffset = new Vector2(0f, 1.2f)
            },
            new ShopItem
            {
                Id = "bath", Name = "澡盆", Emoji = "🛁", Price = 180, Category = ShopCategory.Prop,
                Kind = InteractableKind.Bath,
                Blurb = "彻底洗个澡，脏东西一次冲干净。",
                DefaultPosition = new Vector2(-5.2f, 4.6f), ApproachOffset = new Vector2(0f, -1.2f)
            }
        };

        public static ShopItem Get(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return null;
        }

        /// <summary>The starter furniture: always present, never sold.</summary>
        public static bool IsStarter(string id) => id == FoodBowl || id == WaterBowl;
    }
}
