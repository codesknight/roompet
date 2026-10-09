using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The three shelves of the shop, matching the request: 道具 / 食品 / 家具.
    ///
    ///  - <see cref="Food"/> is consumed one unit at a time (a bag of kibble, a bottle of water,
    ///    a cut of meat, an apple, a fish). Some food is bought, some is produced in the world.
    ///  - <see cref="Tool"/> is carried in a backpack tool slot: a shovel for the mess, a bucket
    ///    for watering the tree. Owning one is enough; it does not sit in the room.
    ///  - <see cref="Furniture"/> is placed in the room — and, since places are now genuinely
    ///    different, most furniture is only allowed in the place it belongs to.
    /// </summary>
    public enum ShopCategory
    {
        Food = 0,
        Tool = 1,
        Furniture = 2
    }

    /// <summary>Where a piece of furniture is allowed to be placed.</summary>
    public enum ItemScene
    {
        /// <summary>Any of the three places.</summary>
        Anywhere = 0,

        /// <summary>Only in the garden.</summary>
        Garden = 1,

        /// <summary>Only on the night terrace.</summary>
        Terrace = 2
    }

    /// <summary>
    /// One thing the shop sells, or the world produces.
    ///
    /// Every entry is data, not code: the name, the price, the one-line pitch, what category it
    /// is, what interactable it places (if furniture), where it is allowed, and how many meals a
    /// food item is worth. That is what makes "every item has its own interaction" readable in
    /// one place — and testable.
    /// </summary>
    public class ShopItem
    {
        public string Id = "";
        public string Name = "";
        public string Emoji = "";

        /// <summary>Price in pet coins. For produced food this is also its sell value.</summary>
        public int Price;

        public string Blurb = "";

        public ShopCategory Category = ShopCategory.Furniture;

        /// <summary>The interactable this furniture places. Null for food and tools.</summary>
        public InteractableKind? Kind;

        /// <summary>Where a freshly placed item starts, in room metres (x, z).</summary>
        public Vector2 DefaultPosition;

        /// <summary>Where the pet stands to use it, relative to the item.</summary>
        public Vector2 ApproachOffset;

        /// <summary>How many meals one bag of food is worth.</summary>
        public int FoodUnits;

        /// <summary>Which place this furniture belongs to. Food and tools are scene-free.</summary>
        public ItemScene Scene = ItemScene.Anywhere;

        /// <summary>True for food the world produces rather than the shop (apple, fish).</summary>
        public bool Produced;

        /// <summary>
        /// What you get back for selling it. Purchased goods refund half; food the world produced
        /// (apple, fish) has no buy price, so its whole value is its sell price.
        /// </summary>
        public int SellPrice => Produced ? Price : Price / 2;

        public bool IsFood => Category == ShopCategory.Food;
        public bool IsTool => Category == ShopCategory.Tool;
        public bool IsFurniture => Category == ShopCategory.Furniture;

        /// <summary>Whether this furniture is allowed in the given place.</summary>
        public bool AllowedIn(RoomTheme theme)
        {
            if (Scene == ItemScene.Anywhere) return true;
            return theme == RoomTheme.Garden && Scene == ItemScene.Garden ||
                   theme == RoomTheme.Terrace && Scene == ItemScene.Terrace;
        }
    }

    /// <summary>
    /// The whole catalogue: food, tools and furniture.
    ///
    /// The two bowls are starter furniture, always owned and always placed, which is what "the
    /// default room has only the pet and two bowls" means. Everything else is bought here or
    /// produced in the world.
    /// </summary>
    public static class PetShop
    {
        public const string FoodBowl = "food_bowl";
        public const string WaterBowl = "water_bowl";

        public static readonly ShopItem[] All =
        {
            // ------------------------------------------------------------- 食品类
            new ShopItem
            {
                Id = "food", Name = "宠物粮", Emoji = "🦴", Price = 20, Category = ShopCategory.Food,
                Blurb = "一袋能吃 3 顿。饭碗空了宠物会挨饿，记得补货。", FoodUnits = 3
            },
            new ShopItem
            {
                Id = "water", Name = "饮用水", Emoji = "💧", Price = 12, Category = ShopCategory.Food,
                Blurb = "一瓶解渴，也能浇树。", FoodUnits = 1
            },
            new ShopItem
            {
                Id = "meat", Name = "鲜肉", Emoji = "🥩", Price = 45, Category = ShopCategory.Food,
                Blurb = "吃一顿饱很久，宠物最爱。", FoodUnits = 1
            },
            new ShopItem
            {
                Id = "apple", Name = "苹果", Emoji = "🍎", Price = 8, Category = ShopCategory.Food,
                Blurb = "花园的苹果树上摘下来的，能吃也能卖钱。", FoodUnits = 1, Produced = true
            },
            new ShopItem
            {
                Id = "fish", Name = "鱼", Emoji = "🐟", Price = 12, Category = ShopCategory.Food,
                Blurb = "池塘里钓上来的，能吃也能卖钱。", FoodUnits = 1, Produced = true
            },

            // ------------------------------------------------------------- 道具类
            new ShopItem
            {
                Id = "shovel", Name = "铲子", Emoji = "🪏", Price = 30, Category = ShopCategory.Tool,
                Blurb = "背包装备后能清理宠物的排泄物。没有它，地上的污渍只能看着。"
            },
            new ShopItem
            {
                Id = "bucket", Name = "水桶", Emoji = "🪣", Price = 40, Category = ShopCategory.Tool,
                Blurb = "背包装备后能在池塘打水，浇苹果树能让它多结果子。"
            },

            // ------------------------------------------------------------- 家具类
            new ShopItem
            {
                Id = "litter_box", Name = "猫砂盆", Emoji = "🚽", Price = 150, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Toilet,
                Blurb = "有了它宠物会自己去上厕所，每用一次还自动给你赚金币；没有它……你就得擦地板了。",
                DefaultPosition = new Vector2(5.1f, -5.0f), ApproachOffset = new Vector2(0f, 1.4f)
            },
            new ShopItem
            {
                Id = "bed", Name = "小床", Emoji = "🛏️", Price = 120, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Bed,
                Blurb = "困了会自己爬上去睡，睡醒精力满满。",
                DefaultPosition = new Vector2(-4.4f, -4.4f), ApproachOffset = new Vector2(0f, 1.5f)
            },
            new ShopItem
            {
                Id = "ball", Name = "皮球", Emoji = "⚽", Price = 60, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Ball,
                Blurb = "扔出去宠物会追着捡回来，是最快的陪玩。",
                DefaultPosition = new Vector2(1.8f, 4.2f), ApproachOffset = new Vector2(0f, -1.2f)
            },
            new ShopItem
            {
                Id = "brush", Name = "梳子", Emoji = "🪮", Price = 40, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Brush,
                Blurb = "玩脏了就梳梳毛，干净又开心。",
                DefaultPosition = new Vector2(-1.6f, 4.6f), ApproachOffset = new Vector2(0f, -1.2f)
            },
            new ShopItem
            {
                Id = "toy", Name = "玩具", Emoji = "🧸", Price = 80, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Toy,
                Blurb = "一个小玩具，宠物会自己扑过去玩。",
                DefaultPosition = new Vector2(0f, -2.5f), ApproachOffset = new Vector2(0f, 1.2f)
            },
            new ShopItem
            {
                Id = "bath", Name = "澡盆", Emoji = "🛁", Price = 180, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Bath,
                Blurb = "彻底洗个澡，脏东西一次冲干净。",
                DefaultPosition = new Vector2(-5.2f, 4.6f), ApproachOffset = new Vector2(0f, -1.2f)
            },

            // ------------------------------------------------------ 花园专属家具
            new ShopItem
            {
                Id = "apple_tree", Name = "苹果树", Emoji = "🌳", Price = 200, Category = ShopCategory.Furniture,
                Kind = InteractableKind.AppleTree, Scene = ItemScene.Garden,
                Blurb = "会自己结果子。宠物饿了会捡地上的苹果吃，你摘了能放仓库或卖钱；浇水结得更快。",
                DefaultPosition = new Vector2(-4.6f, 3.6f), ApproachOffset = new Vector2(0f, -1.4f)
            },
            new ShopItem
            {
                Id = "pond", Name = "小池塘", Emoji = "🪷", Price = 260, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Pond, Scene = ItemScene.Garden,
                Blurb = "宠物渴了会来喝水；你可以在池塘边钓鱼，钓上来的鱼能吃也能卖。",
                DefaultPosition = new Vector2(4.6f, -4.0f), ApproachOffset = new Vector2(0f, 1.2f)
            },
            new ShopItem
            {
                Id = "grass_heap", Name = "草堆", Emoji = "🌾", Price = 90, Category = ShopCategory.Furniture,
                Kind = InteractableKind.GrassHeap, Scene = ItemScene.Garden,
                Blurb = "松软的草堆，宠物困了会钻进去睡，比小床更适合花园。",
                DefaultPosition = new Vector2(-4.0f, -4.4f), ApproachOffset = new Vector2(0f, 1.4f)
            },
            new ShopItem
            {
                Id = "swing", Name = "秋千", Emoji = "🎠", Price = 140, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Swing, Scene = ItemScene.Garden,
                Blurb = "宠物会自己荡着玩，心情好得很快。",
                DefaultPosition = new Vector2(4.8f, 3.8f), ApproachOffset = new Vector2(0f, -1.3f)
            },

            // ------------------------------------------------------ 露台专属家具
            new ShopItem
            {
                Id = "telescope", Name = "天文望远镜", Emoji = "🔭", Price = 220, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Telescope, Scene = ItemScene.Terrace,
                Blurb = "露台看得见星星，宠物会凑过去看。",
                DefaultPosition = new Vector2(4.6f, 4.2f), ApproachOffset = new Vector2(0f, -1.3f)
            },
            new ShopItem
            {
                Id = "rocking_chair", Name = "摇椅", Emoji = "🪑", Price = 160, Category = ShopCategory.Furniture,
                Kind = InteractableKind.RockingChair, Scene = ItemScene.Terrace,
                Blurb = "露台上摇啊摇，宠物会蜷上去打盹。",
                DefaultPosition = new Vector2(-4.4f, -4.0f), ApproachOffset = new Vector2(0f, 1.4f)
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
