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

        /// <summary>Indoor furniture: the cabin and the night terrace, mutually placeable.</summary>
        Indoor = 2,

        /// <summary>Only in the cabin.</summary>
        Cabin = 3
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

        /// <summary>How much hunger one unit of this food fills, for direct feeding.</summary>
        public float FoodAmount = 0.5f;

        /// <summary>True when this food is a drink (water) rather than a meal.</summary>
        public bool IsDrink;

        /// <summary>Which place this furniture belongs to. Food and tools are scene-free.</summary>
        public ItemScene Scene = ItemScene.Anywhere;

        /// <summary>For starter furniture: the one scene it is auto-placed in by default. Null means
        /// "every scene it is allowed in" (used by the indoor starter furniture so the terrace sofa
        /// does not also appear in the cabin on its own).</summary>
        public RoomTheme? HomeTheme = null;

        /// <summary>True for food the world produces rather than the shop (apple, fish).</summary>
        public bool Produced;

        /// <summary>
        /// True for furniture the room starts with: always owned, placed by default in its own
        /// scene, movable and storable, but not sellable — so the default room is always restorable.
        /// </summary>
        public bool Starter;

        /// <summary>Rough radius used to keep placed furniture from overlapping, in room metres.</summary>
        public float Footprint = 0.9f;

        /// <summary>How to actually use it, shown alongside the pitch in the shop / warehouse.</summary>
        public string Usage = "";

        /// <summary>Furniture that can be bought and placed any number of times (walls, fences).</summary>
        public bool Stackable = false;

        /// <summary>Resource path for decor props ("" for procedurally-built decor).</summary>
        public string Model = "";

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
            switch (Scene)
            {
                case ItemScene.Garden: return theme == RoomTheme.Garden;
                case ItemScene.Indoor: return theme == RoomTheme.Cabin || theme == RoomTheme.Terrace;
                case ItemScene.Cabin: return theme == RoomTheme.Cabin;
                default: return true;
            }
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
                Blurb = "一袋能吃 3 顿。饭碗空了宠物会挨饿，记得补货。",
                Usage = "放进仓库；宠物饿了会自己去饭碗吃。", FoodUnits = 3, FoodAmount = 0.55f
            },
            new ShopItem
            {
                Id = "water", Name = "饮用水", Emoji = "💧", Price = 12, Category = ShopCategory.Food,
                Blurb = "一瓶解渴，也能浇树。",
                Usage = "放进仓库；宠物渴了会去水碗喝。", FoodUnits = 1, IsDrink = true
            },
            new ShopItem
            {
                Id = "meat", Name = "鲜肉", Emoji = "🥩", Price = 45, Category = ShopCategory.Food,
                Blurb = "吃一顿饱很久，宠物最爱。",
                Usage = "放进仓库，点「投喂」直接喂给宠物。", FoodUnits = 1, FoodAmount = 0.9f
            },
            new ShopItem
            {
                Id = "apple", Name = "苹果", Emoji = "🍎", Price = 8, Category = ShopCategory.Food,
                Blurb = "花园的苹果树上摘下来的，能吃也能卖钱。",
                Usage = "点「投喂」给宠物吃，或在仓库卖掉。", FoodUnits = 1, FoodAmount = 0.45f, Produced = true
            },
            new ShopItem
            {
                Id = "fish", Name = "鱼", Emoji = "🐟", Price = 12, Category = ShopCategory.Food,
                Blurb = "池塘里钓上来的，能吃也能卖钱。",
                Usage = "点「投喂」给宠物吃，或在仓库卖掉。", FoodUnits = 1, FoodAmount = 0.7f, Produced = true
            },

            // ------------------------------------------------------------- 道具类
            new ShopItem
            {
                Id = "shovel", Name = "铲子", Emoji = "🪏", Price = 30, Category = ShopCategory.Tool,
                Blurb = "背包装备后能清理宠物的排泄物。没有它，地上的污渍只能看着。",
                Usage = "买下后到「背包」装备，再点地上的便便清理。"
            },
            new ShopItem
            {
                Id = "bucket", Name = "水桶", Emoji = "🪣", Price = 40, Category = ShopCategory.Tool,
                Blurb = "背包装备后能在池塘打水，浇苹果树能让它多结果子。",
                Usage = "买下后到「背包」装备，去池塘打水、浇苹果树。"
            },
            new ShopItem
            {
                Id = "brush", Name = "梳子", Emoji = "🪮", Price = 40, Category = ShopCategory.Tool,
                Blurb = "背包装备后，说「梳毛」就能帮宠物梳毛——它是随身工具，不用摆在房间里。",
                Usage = "买下后到「背包」装备，对宠物说「梳毛」。"
            },

            // ------------------------------------------------------------- 家具类
            new ShopItem
            {
                Id = "litter_box", Name = "猫砂盆", Emoji = "🚽", Price = 150, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Toilet, Scene = ItemScene.Indoor,
                Blurb = "有了它宠物会自己去上厕所，每用一次还自动给你赚金币；没有它……你就得擦地板了。",
                Usage = "摆进小屋或露台；宠物会自己进去上厕所。",
                DefaultPosition = new Vector2(5.1f, -5.0f), ApproachOffset = new Vector2(0f, 1.4f)
            },
            new ShopItem
            {
                Id = "bed", Name = "小床", Emoji = "🛏️", Price = 120, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Bed, Scene = ItemScene.Indoor,
                Blurb = "困了会自己爬上去睡，睡醒精力满满。",
                Usage = "摆进小屋或露台；宠物困了会自己爬上去睡。",
                DefaultPosition = new Vector2(-4.4f, -4.4f), ApproachOffset = new Vector2(0f, 1.5f)
            },
            new ShopItem
            {
                Id = "ball", Name = "皮球", Emoji = "⚽", Price = 60, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Ball, Scene = ItemScene.Indoor,
                Blurb = "扔出去宠物会追着捡回来，是最快的陪玩。",
                Usage = "摆进小屋或露台；点它让宠物去玩，也能扔出去。",
                DefaultPosition = new Vector2(1.8f, 4.2f), ApproachOffset = new Vector2(0f, -1.2f)
            },
            new ShopItem
            {
                Id = "toy", Name = "玩具", Emoji = "🧸", Price = 80, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Toy, Scene = ItemScene.Indoor,
                Blurb = "一个小玩具，宠物会自己扑过去玩。",
                Usage = "摆进小屋或露台；宠物无聊会自己去扑玩。",
                DefaultPosition = new Vector2(0f, -2.5f), ApproachOffset = new Vector2(0f, 1.2f)
            },
            new ShopItem
            {
                Id = "bath", Name = "澡盆", Emoji = "🛁", Price = 180, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Bath, Scene = ItemScene.Indoor,
                Blurb = "彻底洗个澡，脏东西一次冲干净。",
                Usage = "摆进小屋或露台；宠物脏了会去洗澡。",
                DefaultPosition = new Vector2(-5.2f, 4.6f), ApproachOffset = new Vector2(0f, -1.2f)
            },
            new ShopItem
            {
                Id = "robot_butler", Name = "管家机器人", Emoji = "🤖", Price = 5000, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Robot, Footprint = 0.7f,
                Blurb = "智能家具。点一下开机：它会自动跟随状态最差的宠物，喂食、哄睡、陪玩、清洁、清理排泄物；再点一下待机（摆放模式可挪位置）。开机眼睛亮绿灯，待机变黑。",
                Usage = "点一下开机照顾宠物，再点待机；摆放模式可拖位置。",
                DefaultPosition = new Vector2(0f, 0f), ApproachOffset = new Vector2(0f, -1.2f)
            },

            // ------------------------------------------------------ 花园专属家具
            new ShopItem
            {
                Id = "apple_tree", Name = "苹果树", Emoji = "🌳", Price = 200, Category = ShopCategory.Furniture,
                Kind = InteractableKind.AppleTree, Scene = ItemScene.Garden,
                Blurb = "会自己结果子。宠物饿了会捡地上的苹果吃，你摘了能放仓库或卖钱；浇水结得更快。",
                Usage = "摆进花园；点它摘苹果放仓库，浇水结得更快。",
                DefaultPosition = new Vector2(-4.6f, 3.6f), ApproachOffset = new Vector2(0f, -1.4f)
            },
            new ShopItem
            {
                Id = "pond", Name = "小池塘", Emoji = "🪷", Price = 260, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Pond, Scene = ItemScene.Garden,
                Blurb = "宠物渴了会来喝水；你可以在池塘边钓鱼，钓上来的鱼能吃也能卖。",
                Usage = "摆进花园；点它钓鱼或打水（要背包装备水桶）。",
                DefaultPosition = new Vector2(4.6f, -4.0f), ApproachOffset = new Vector2(0f, 1.2f)
            },
            new ShopItem
            {
                Id = "grass_heap", Name = "草堆", Emoji = "🌾", Price = 90, Category = ShopCategory.Furniture,
                Kind = InteractableKind.GrassHeap, Scene = ItemScene.Garden,
                Blurb = "松软的草堆，宠物困了会钻进去睡，比小床更适合花园。",
                Usage = "摆进花园；宠物困了会钻进去睡。",
                DefaultPosition = new Vector2(-4.0f, -4.4f), ApproachOffset = new Vector2(0f, 1.4f)
            },
            new ShopItem
            {
                Id = "swing", Name = "秋千", Emoji = "🎠", Price = 140, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Swing, Scene = ItemScene.Garden,
                Blurb = "宠物会自己荡着玩，心情好得很快。",
                Usage = "摆进花园；宠物会自己荡着玩。",
                DefaultPosition = new Vector2(4.8f, 3.8f), ApproachOffset = new Vector2(0f, -1.3f)
            },

            // ------------------------------------------------------ 室内（小屋 + 露台）家具
            new ShopItem
            {
                Id = "telescope", Name = "天文望远镜", Emoji = "🔭", Price = 220, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Telescope, Scene = ItemScene.Indoor,
                Blurb = "露台看得见星星，宠物会凑过去看。",
                Usage = "摆进小屋或露台；宠物会凑过去看星星。",
                DefaultPosition = new Vector2(4.6f, 4.2f), ApproachOffset = new Vector2(0f, -1.3f)
            },
            new ShopItem
            {
                Id = "rocking_chair", Name = "摇椅", Emoji = "🪑", Price = 160, Category = ShopCategory.Furniture,
                Kind = InteractableKind.RockingChair, Scene = ItemScene.Indoor,
                Blurb = "露台上摇啊摇，宠物会蜷上去打盹。",
                Usage = "摆进小屋或露台；宠物会蜷上去打盹。",
                DefaultPosition = new Vector2(-4.4f, -4.0f), ApproachOffset = new Vector2(0f, 1.4f)
            },

            // -------------------------------------------- 可无限购买的结构（墙 / 栅栏）
            new ShopItem
            {
                Id = "indoor_wall", Name = "室内短墙", Emoji = "🧱", Price = 60, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Wall, Scene = ItemScene.Indoor, Stackable = true, Footprint = 1.3f,
                Blurb = "一段矮墙，可以隔出小屋里的区域，也能靠墙摆。",
                Usage = "摆进小屋或露台；不设购买上限，多买几段拼成隔断。",
                DefaultPosition = new Vector2(2.6f, -2.2f), ApproachOffset = new Vector2(0f, 1.2f)
            },
            new ShopItem
            {
                Id = "outdoor_fence", Name = "户外栅栏", Emoji = "🚧", Price = 50, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Fence, Scene = ItemScene.Garden, Stackable = true, Footprint = 1.3f,
                Blurb = "一段可移动的栅栏，用来圈出花园里的区域。",
                Usage = "摆进花园；不设购买上限，多买几段围成圈。",
                DefaultPosition = new Vector2(0f, 3.8f), ApproachOffset = new Vector2(0f, -1.2f)
            },

            // ------------------------------------------------------ 默认家具（房间自带，可挪可收，不可卖）
            new ShopItem
            {
                Id = "sofa", Name = "沙发", Emoji = "🛋️", Price = 160, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Sofa, Scene = ItemScene.Indoor, HomeTheme = RoomTheme.Terrace, Starter = true, Footprint = 1.0f,
                Blurb = "露台自带的沙发，宠物会坐上去休息。",
                Usage = "摆进小屋或露台；宠物坐上去休息。",
                DefaultPosition = new Vector2(-2.8f, 1.9f), ApproachOffset = new Vector2(0f, -1.3f)
            },
            new ShopItem
            {
                Id = "sofa_long", Name = "长沙发", Emoji = "🛋️", Price = 180, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Sofa, Scene = ItemScene.Indoor, HomeTheme = RoomTheme.Terrace, Starter = true, Footprint = 1.1f,
                Blurb = "露台自带的长沙发，宠物会坐上去休息。",
                Usage = "摆进小屋或露台；宠物坐上去休息。",
                DefaultPosition = new Vector2(2.9f, 1.9f), ApproachOffset = new Vector2(0f, -1.3f)
            },
            new ShopItem
            {
                Id = "lamp", Name = "落地灯", Emoji = "💡", Price = 100, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Lamp, Scene = ItemScene.Indoor, HomeTheme = RoomTheme.Terrace, Starter = true, Footprint = 0.5f,
                Blurb = "露台自带的落地灯，点一下开、再点一下关。",
                Usage = "摆进小屋或露台；点一下开关灯。",
                DefaultPosition = new Vector2(-5.6f, 4.6f), ApproachOffset = new Vector2(0f, -0.6f)
            },
            new ShopItem
            {
                Id = "bench", Name = "长椅", Emoji = "🪑", Price = 90, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Sofa, Scene = ItemScene.Indoor, HomeTheme = RoomTheme.Terrace, Starter = true, Footprint = 0.9f,
                Blurb = "露台自带的长椅，宠物会坐上去休息。",
                Usage = "摆进小屋或露台；宠物坐上去休息。",
                DefaultPosition = new Vector2(4.6f, -3.4f), ApproachOffset = new Vector2(0f, -1.2f)
            },
            new ShopItem
            {
                Id = "warehouse_cabinet", Name = "仓库柜", Emoji = "🗄️", Price = 140, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Warehouse, Scene = ItemScene.Indoor, HomeTheme = RoomTheme.Cabin, Starter = true, Footprint = 0.9f,
                Blurb = "小屋自带的仓库柜，点它直接打开仓库页。",
                Usage = "点它直接打开仓库页。",
                DefaultPosition = new Vector2(-3.4f, 6.0f), ApproachOffset = new Vector2(0f, -1.7f)
            },

            // --------------------------------------- 默认装饰物（可移动 / 带碰撞盒 / 可收纳）
            new ShopItem
            {
                Id = "rug", Name = "地毯", Emoji = "🟥", Price = 120, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Decor, Scene = ItemScene.Cabin, HomeTheme = RoomTheme.Cabin, Starter = true, Footprint = 0.25f,
                Blurb = "小屋自带的旧地毯，踩上去软软的。",
                Usage = "装饰地面；摆放模式可拖动、可收回仓库。",
                DefaultPosition = new Vector2(0f, 0.5f), ApproachOffset = new Vector2(0f, -1.5f)
            },
            new ShopItem
            {
                Id = "coffee_table", Name = "茶几", Emoji = "🪵", Price = 130, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Decor, Scene = ItemScene.Indoor, HomeTheme = RoomTheme.Terrace, Starter = true, Footprint = 1.0f,
                Blurb = "露台自带的茶几，配沙发刚刚好。",
                Usage = "纯装饰；摆放模式可拖动、可收回仓库。",
                DefaultPosition = new Vector2(0f, 2.4f), ApproachOffset = new Vector2(0f, -1.4f),
                Model = "Kenney/Furniture/tableCoffee"
            },
            new ShopItem
            {
                Id = "potted_plant", Name = "盆栽", Emoji = "🪴", Price = 90, Category = ShopCategory.Furniture,
                Kind = InteractableKind.Decor, Scene = ItemScene.Indoor, HomeTheme = RoomTheme.Terrace, Starter = true, Footprint = 0.6f,
                Blurb = "露台自带的绿植，让角落不那么空。",
                Usage = "纯装饰；摆放模式可拖动、可收回仓库。",
                DefaultPosition = new Vector2(-5.8f, -5.3f), ApproachOffset = new Vector2(0f, -1.2f),
                Model = "Kenney/Furniture/pottedPlant"
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

        /// <summary>Default furniture the room starts with: always owned, movable and storable,
        /// but not sellable. <see cref="IsStarter"/> (the bowls) is the stricter always-placed case.</summary>
        public static bool IsDefault(string id)
        {
            var item = Get(id);
            return item != null && item.Starter;
        }
    }
}
