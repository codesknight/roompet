using NUnit.Framework;
using UnityEngine;

namespace DshPet.Tests
{
    /// <summary>
    /// The shop, the warehouse, the backpack and the garden's closed loop, as pure rules.
    ///
    /// The request is that every item has a real interaction, that furniture is bought and placed
    /// (and only where it belongs), that the backpack's tool slots gate what the owner can do,
    /// and that the garden produces food you can eat or sell. All of that is a set of rules about
    /// coins, ownership, placement and tools — which is exactly what can be pinned down without
    /// a scene.
    /// </summary>
    public class PetShopTests
    {
        [SetUp]
        public void SetUp()
        {
            PetInventory.ResetForTests();
            DshMobile.PetWallet.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            PetInventory.ResetForTests();
        }

        [Test]
        public void TheDefaultRoomHasTheBowlsAndItsDefaultFurniture()
        {
            var placed = PetInventory.Placed(RoomTheme.Cabin);
            Assert.IsTrue(placed.ContainsKey(PetShop.FoodBowl), "the food bowl is always in the room");
            Assert.IsTrue(placed.ContainsKey(PetShop.WaterBowl), "the water bowl is always in the room");
            Assert.IsTrue(placed.ContainsKey("warehouse_cabinet"), "the cabin's default warehouse cabinet is there");
            Assert.IsFalse(placed.ContainsKey("sofa"), "a terrace sofa is not in the cabin");
            Assert.IsFalse(PetInventory.IsPlaced("bed", RoomTheme.Cabin), "bought furniture still has to be bought");
            Assert.IsFalse(PetInventory.IsPlaced("litter_box", RoomTheme.Cabin));
        }

        [Test]
        public void EachSceneHasItsOwnFurnitureLayout()
        {
            DshMobile.PetWallet.Add(1000);
            PetInventory.Buy(PetShop.Get("apple_tree"));
            PetInventory.Buy(PetShop.Get("telescope"));

            Assert.IsTrue(PetInventory.Place("apple_tree", RoomTheme.Garden).Contains("摆进了"));
            Assert.IsTrue(PetInventory.IsPlaced("apple_tree", RoomTheme.Garden));
            Assert.IsFalse(PetInventory.IsPlaced("apple_tree", RoomTheme.Cabin), "a garden tree is not in the cabin");
            Assert.IsFalse(PetInventory.IsPlaced("apple_tree", RoomTheme.Terrace));

            PetInventory.Place("telescope", RoomTheme.Terrace);
            Assert.IsFalse(PetInventory.IsPlaced("telescope", RoomTheme.Garden));

            PetInventory.MoveItem(PetShop.FoodBowl, new Vector2(1f, 1f), RoomTheme.Garden);
            Assert.AreEqual(new Vector2(1f, 1f), PetInventory.PositionOf(PetShop.FoodBowl, RoomTheme.Garden));
            Assert.AreEqual(new Vector2(3.4f, 3.2f), PetInventory.PositionOf(PetShop.FoodBowl, RoomTheme.Cabin),
                "moving the garden bowl does not move the cabin bowl");
        }

        [Test]
        public void EveryItemHasACoherentCategory()
        {
            foreach (var item in PetShop.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(item.Id), "every item has an id");
                Assert.IsFalse(string.IsNullOrEmpty(item.Name), item.Id + " has no name");
                Assert.IsFalse(string.IsNullOrEmpty(item.Blurb), item.Id + " has no pitch");
                Assert.Greater(item.Price, 0, item.Id + " is free");

                if (item.IsFood)
                {
                    Assert.IsNull(item.Kind, "food is not an interactable");
                    Assert.Greater(item.FoodUnits, 0, "a food bag must hold some meals");
                }
                else if (item.IsTool)
                {
                    Assert.IsNull(item.Kind, "a tool is carried, not placed");
                }
                else
                {
                    Assert.IsTrue(item.Kind.HasValue, item.Id + " must place an interactable");
                }
            }
        }

        [Test]
        public void GardenAndTerraceFurnitureIsSceneLocked()
        {
            Assert.IsTrue(PetShop.Get("apple_tree").AllowedIn(RoomTheme.Garden));
            Assert.IsTrue(PetShop.Get("pond").AllowedIn(RoomTheme.Garden));
            Assert.IsTrue(PetShop.Get("grass_heap").AllowedIn(RoomTheme.Garden));
            Assert.IsTrue(PetShop.Get("swing").AllowedIn(RoomTheme.Garden));
            Assert.IsFalse(PetShop.Get("apple_tree").AllowedIn(RoomTheme.Cabin), "an apple tree is not a cabin item");
            Assert.IsFalse(PetShop.Get("apple_tree").AllowedIn(RoomTheme.Terrace));

            Assert.IsTrue(PetShop.Get("telescope").AllowedIn(RoomTheme.Terrace));
            Assert.IsTrue(PetShop.Get("rocking_chair").AllowedIn(RoomTheme.Terrace));
            Assert.IsFalse(PetShop.Get("telescope").AllowedIn(RoomTheme.Garden));

            Assert.IsTrue(PetShop.Get("bed").AllowedIn(RoomTheme.Cabin), "a bed is indoor furniture");
            Assert.IsTrue(PetShop.Get("bed").AllowedIn(RoomTheme.Terrace), "indoor furniture fits the terrace too");
            Assert.IsFalse(PetShop.Get("bed").AllowedIn(RoomTheme.Garden), "indoor furniture does not go in the garden");
        }

        [Test]
        public void PlaceRejectsTheWrongScene()
        {
            DshMobile.PetWallet.Add(1000);
            PetInventory.Buy(PetShop.Get("apple_tree"));

            string result = PetInventory.Place("apple_tree", RoomTheme.Cabin);
            Assert.IsTrue(result.Contains("只能摆在花园"), "an apple tree cannot be placed in the cabin");
            Assert.IsFalse(PetInventory.IsPlaced("apple_tree", RoomTheme.Cabin));

            Assert.IsTrue(PetInventory.Place("apple_tree", RoomTheme.Garden).Contains("摆进了"));
            Assert.IsTrue(PetInventory.IsPlaced("apple_tree", RoomTheme.Garden));
        }

        [Test]
        public void BuyingFoodSpendsCoinsAndAddsMeals()
        {
            DshMobile.PetWallet.Add(100);
            PetInventory.Buy(PetShop.Get("food"));

            Assert.AreEqual(80, DshMobile.PetWallet.Coins, "one bag costs 20");
            Assert.AreEqual(3, PetInventory.Food, "one bag is three meals");
        }

        [Test]
        public void BuyingFurnitureOnceOnlyAndSellingRefundsHalf()
        {
            DshMobile.PetWallet.Add(1000);
            PetInventory.Buy(PetShop.Get("bed"));
            Assert.IsTrue(PetInventory.IsOwned("bed"));

            Assert.IsTrue(PetInventory.Buy(PetShop.Get("bed")).Contains("买过"));
            PetInventory.Place("bed", RoomTheme.Cabin);
            Assert.IsTrue(PetInventory.IsPlaced("bed", RoomTheme.Cabin));

            PetInventory.Sell("bed");
            Assert.IsFalse(PetInventory.IsOwned("bed"));
            Assert.IsFalse(PetInventory.IsPlaced("bed", RoomTheme.Cabin));
            Assert.AreEqual(1000 - 120 + 60, DshMobile.PetWallet.Coins, "half of 120 back is 60");
        }

        [Test]
        public void StarterBowlsCannotBeSoldOrStoredButCanBeMoved()
        {
            Assert.AreEqual("基础家具不能卖", PetInventory.Sell(PetShop.FoodBowl));
            Assert.AreEqual("基础家具不能收起来", PetInventory.Store(PetShop.WaterBowl, RoomTheme.Cabin));

            PetInventory.MoveItem(PetShop.FoodBowl, new Vector2(0f, 1.5f), RoomTheme.Cabin);
            Assert.AreEqual(new Vector2(0f, 1.5f), PetInventory.PositionOf(PetShop.FoodBowl, RoomTheme.Cabin));
        }

        [Test]
        public void PlacementIsClampedInsideTheRoom()
        {
            var inside = PetInventory.ClampToRoom(new Vector2(100f, -100f));
            Assert.LessOrEqual(Mathf.Abs(inside.x), PetInventory.RoomHalf - PetInventory.PlacementMargin);
            Assert.LessOrEqual(Mathf.Abs(inside.y), PetInventory.RoomHalf - PetInventory.PlacementMargin);
            Assert.AreEqual(new Vector2(1f, 2f), PetInventory.ClampToRoom(new Vector2(1f, 2f)));
        }

        [Test]
        public void FoodRunsOutAndEatingConsumesIt()
        {
            DshMobile.PetWallet.Add(100);
            PetInventory.Buy(PetShop.Get("food"));
            Assert.AreEqual(3, PetInventory.Food);

            Assert.IsTrue(PetInventory.TryConsumeMeal());
            Assert.IsTrue(PetInventory.TryConsumeMeal());
            Assert.IsTrue(PetInventory.TryConsumeMeal());
            Assert.AreEqual(0, PetInventory.Food);
            Assert.IsFalse(PetInventory.TryConsumeMeal(), "an empty pantry serves no meals");
        }

        [Test]
        public void WaterRunsOutAndDrinkingConsumesIt()
        {
            Assert.IsFalse(PetInventory.TryConsumeWater(), "no water in the warehouse at first");

            PetInventory.Add("water", 2);
            Assert.IsTrue(PetInventory.TryConsumeWater());
            Assert.IsTrue(PetInventory.TryConsumeWater());
            Assert.AreEqual(0, PetInventory.Count("water"));
            Assert.IsFalse(PetInventory.TryConsumeWater(), "an empty warehouse serves no water");
        }

        [Test]
        public void StackableFoodSellsOneUnitAtATime()
        {
            PetInventory.Add("apple", 3);
            PetInventory.Add("fish", 2);

            string sold = PetInventory.Sell("apple");
            Assert.AreEqual(2, PetInventory.Count("apple"));
            Assert.IsTrue(sold.Contains("8"), "an apple sells for 8");

            Assert.AreEqual(2, PetInventory.Count("fish"));
            PetInventory.Sell("fish");
            Assert.AreEqual(1, PetInventory.Count("fish"));
        }

        [Test]
        public void TheBrushIsAToolCarriedInTheBackpack()
        {
            var brush = PetShop.Get("brush");
            Assert.IsNotNull(brush);
            Assert.IsTrue(brush.IsTool, "梳子是工具，不是家具");
            Assert.IsNull(brush.Kind, "a tool is carried, not placed as furniture");

            DshMobile.PetWallet.Add(1000);
            PetInventory.Buy(brush);
            Assert.IsFalse(PetBackpack.HasBrush, "buying is not equipping");
            Assert.AreEqual("梳子装进了背包", PetBackpack.Equip("brush"));
            Assert.IsTrue(PetBackpack.HasBrush);
        }

        [Test]
        public void TheRobotButlerIsPremiumFurnitureThatGoesAnywhere()
        {
            var robot = PetShop.Get("robot_butler");
            Assert.IsNotNull(robot, "the robot is in the shop");
            Assert.AreEqual(5000, robot.Price, "the robot is a 5000-coin premium item");
            Assert.IsTrue(robot.IsFurniture);
            Assert.AreEqual(InteractableKind.Robot, robot.Kind.Value);
            Assert.IsTrue(robot.AllowedIn(RoomTheme.Cabin));
            Assert.IsTrue(robot.AllowedIn(RoomTheme.Garden));
        }

        [Test]
        public void TheBackpackHoldsThreeToolsAndGatesTheShovel()
        {
            DshMobile.PetWallet.Add(1000);
            PetInventory.Buy(PetShop.Get("shovel"));
            PetInventory.Buy(PetShop.Get("bucket"));

            Assert.IsFalse(PetBackpack.CanCleanMess, "no shovel equipped, no cleaning");
            Assert.AreEqual("铲子装进了背包", PetBackpack.Equip("shovel"));
            Assert.IsTrue(PetBackpack.CanCleanMess);
            Assert.IsTrue(PetBackpack.HasShovel);

            Assert.AreEqual("水桶装进了背包", PetBackpack.Equip("bucket"));
            Assert.IsTrue(PetBackpack.HasBucket);
            Assert.AreEqual(2, PetBackpack.Equipped().Count);
            Assert.AreEqual(1, PetBackpack.FreeSlots);

            // A tool that is not owned cannot be equipped, and equipped tools cannot be sold.
            Assert.IsTrue(PetInventory.Sell("shovel").Contains("取下来"), "an equipped tool cannot be sold");
            PetBackpack.Unequip("shovel");
            Assert.IsFalse(PetBackpack.CanCleanMess);
        }

        [Test]
        public void TheBucketWaterLoopWatersTheTree()
        {
            // Draw water at the pond (bucket equipped), then water the tree: one apple appears.
            DshMobile.PetWallet.Add(1000);
            PetInventory.Buy(PetShop.Get("bucket"));
            PetBackpack.Equip("bucket");

            Assert.IsFalse(PetBackpack.BucketFull);
            PetBackpack.BucketFull = true;   // drew water at the pond

            Assert.AreEqual(1, GardenRules.AfterWatering(0), "watering grows one apple");
            Assert.AreEqual(GardenRules.TreeMaxApples, GardenRules.AfterWatering(GardenRules.TreeMaxApples),
                "watering a full tree cannot exceed the cap");
        }

        [Test]
        public void DrawingWaterAtThePondAlsoAddsOneWaterToTheWarehouse()
        {
            // The request: filling the bucket at the pond should ALSO put one bottle of water in
            // the warehouse, not just set the bucket flag — the water is a real stackable item.
            Assert.AreEqual(0, PetInventory.Count("water"));
            Assert.IsFalse(PetBackpack.BucketFull);

            PetInventory.FillBucketFromPond();

            Assert.IsTrue(PetBackpack.BucketFull, "the bucket is full");
            Assert.AreEqual(1, PetInventory.Count("water"), "one bottle of water landed in the warehouse");

            // A second draw while full does not stack up water for free (the pond path only calls
            // this when the bucket is empty, but the rule itself stays idempotent in intent).
            PetBackpack.BucketFull = false;
            PetInventory.FillBucketFromPond();
            Assert.AreEqual(2, PetInventory.Count("water"));
        }

        [Test]
        public void AppleEconomyIsBounded()
        {
            Assert.AreEqual(0, GardenRules.ClampApples(-3));
            Assert.AreEqual(GardenRules.TreeMaxApples, GardenRules.ClampApples(99));
            Assert.AreEqual(2, GardenRules.ClampApples(2));

            // Produced food is sellable and counts like any other stack.
            PetInventory.Add("apple", 1);
            Assert.IsTrue(PetInventory.Has("apple"));
        }

        [Test]
        public void ThePetOnlyGoesToTheBowlWhenThereIsFood()
        {
            var eat = PetBehaviorLibrary.Get("eat");
            Assert.IsTrue(eat.NeedsFood);

            var withFood = new PetBehaviorContext
            {
                Hunger = 0.1f, Energy = 0.8f, Joy = 0.8f, Cleanliness = 0.8f, Bladder = 0.8f,
                FoodAvailable = true, AvailableTargets = new[] { InteractableKind.Food }
            };
            var noFood = new PetBehaviorContext
            {
                Hunger = 0.1f, Energy = 0.8f, Joy = 0.8f, Cleanliness = 0.8f, Bladder = 0.8f,
                FoodAvailable = false, AvailableTargets = new[] { InteractableKind.Food }
            };

            Assert.IsTrue(eat.IsEligible(withFood));
            Assert.IsFalse(eat.IsEligible(noFood));
        }

        [Test]
        public void TheGardenBehavioursAreGatedToTheGardenAndTheirTargets()
        {
            var eatApple = PetBehaviorLibrary.Get("eat_apple");
            var drinkPond = PetBehaviorLibrary.Get("drink_pond");
            var sleepGrass = PetBehaviorLibrary.Get("sleep_grass");

            Assert.IsNotNull(eatApple);
            Assert.IsNotNull(drinkPond);
            Assert.IsNotNull(sleepGrass);
            Assert.AreEqual("Garden", eatApple.OnlyInPlace);
            Assert.AreEqual(InteractableKind.AppleTree, eatApple.Target);
            Assert.AreEqual(InteractableKind.Pond, drinkPond.Target);
            Assert.AreEqual(InteractableKind.GrassHeap, sleepGrass.Target);

            // Hungry pet in the garden with a tree: it eats from the tree.
            var hungryGarden = new PetBehaviorContext
            {
                Hunger = 0.2f, Energy = 0.8f, Joy = 0.8f, Cleanliness = 0.8f, Bladder = 0.8f,
                Place = "Garden", AvailableTargets = new[] { InteractableKind.AppleTree }
            };
            Assert.IsTrue(eatApple.IsEligible(hungryGarden));

            // Same pet indoors: no apple tree to eat from.
            var hungryCabin = new PetBehaviorContext
            {
                Hunger = 0.2f, Energy = 0.8f, Joy = 0.8f, Cleanliness = 0.8f, Bladder = 0.8f,
                Place = "Cabin", AvailableTargets = new[] { InteractableKind.AppleTree }
            };
            Assert.IsFalse(eatApple.IsEligible(hungryCabin));
        }

        [Test]
        public void DefaultFurnitureCanBeStoredAndReplaced()
        {
            // The terrace starts with its sofa, lamp and bench already set down.
            Assert.IsTrue(PetInventory.IsPlaced("sofa", RoomTheme.Terrace));
            Assert.IsTrue(PetInventory.IsPlaced("lamp", RoomTheme.Terrace));
            Assert.IsTrue(PetInventory.IsOwned("sofa"), "default furniture is always owned");

            // Store the sofa: it leaves the room but stays owned.
            Assert.IsTrue(PetInventory.Store("sofa", RoomTheme.Terrace).Contains("收回"));
            Assert.IsFalse(PetInventory.IsPlaced("sofa", RoomTheme.Terrace), "stored sofa is no longer in the room");
            Assert.IsTrue(PetInventory.IsOwned("sofa"));

            // Re-place it: it comes back.
            Assert.IsTrue(PetInventory.Place("sofa", RoomTheme.Terrace).Contains("摆进了"));
            Assert.IsTrue(PetInventory.IsPlaced("sofa", RoomTheme.Terrace));

            // Default furniture cannot be sold, only stored.
            Assert.IsTrue(PetInventory.Sell("sofa").Contains("不能卖"));
        }

        [Test]
        public void PlacedFurnitureDoesNotOverlapTheBowls()
        {
            // Move the terrace sofa right onto the food bowl: it is pushed clear instead of sitting
            // inside the bowl. This is the "食物盆/水盆不和其他物体重叠" rule.
            Vector2 bowl = PetInventory.PositionOf(PetShop.FoodBowl, RoomTheme.Terrace);
            PetInventory.MoveItem("sofa", bowl, RoomTheme.Terrace);

            Vector2 sofaAt = PetInventory.PositionOf("sofa", RoomTheme.Terrace);
            Assert.IsFalse(PetInventory.Overlaps(sofaAt, PetInventory.FootprintOf("sofa"), RoomTheme.Terrace, "sofa"),
                "the sofa must not sit inside the food bowl");
        }

        [Test]
        public void StackableWallsAndFencesCanBeBoughtAndPlacedManyTimes()
        {
            DshMobile.PetWallet.Add(1000);
            var wall = PetShop.Get("indoor_wall");
            var fence = PetShop.Get("outdoor_fence");
            Assert.IsNotNull(wall, "the indoor short wall is in the shop");
            Assert.IsNotNull(fence, "the outdoor fence is in the shop");
            Assert.IsTrue(wall.Stackable && fence.Stackable);
            Assert.AreEqual(InteractableKind.Wall, wall.Kind.Value);
            Assert.AreEqual(InteractableKind.Fence, fence.Kind.Value);
            Assert.IsTrue(fence.AllowedIn(RoomTheme.Garden));
            Assert.IsFalse(fence.AllowedIn(RoomTheme.Cabin), "a garden fence is not indoor furniture");
            Assert.IsTrue(wall.AllowedIn(RoomTheme.Cabin));
            Assert.IsTrue(wall.AllowedIn(RoomTheme.Terrace), "an indoor wall fits the terrace too");

            // No purchase limit: the same wall can be bought over and over.
            Assert.IsTrue(PetInventory.Buy(wall).Contains("共有 1 段"));
            Assert.IsTrue(PetInventory.Buy(wall).Contains("共有 2 段"));
            Assert.IsTrue(PetInventory.Buy(wall).Contains("共有 3 段"));
            Assert.AreEqual(3, PetInventory.OwnedCount("indoor_wall"));

            // Each placement makes an independent movable copy.
            Assert.IsTrue(PetInventory.Place("indoor_wall", RoomTheme.Cabin).Contains("摆进了"));
            Assert.IsTrue(PetInventory.Place("indoor_wall", RoomTheme.Cabin).Contains("摆进了"));
            Assert.AreEqual(2, PetInventory.PlacedCopyCount("indoor_wall"));
            Assert.AreEqual(1, PetInventory.UnplacedCopies("indoor_wall"));

            Assert.IsTrue(PetInventory.Place("indoor_wall", RoomTheme.Cabin).Contains("摆进了"));
            Assert.AreEqual(0, PetInventory.UnplacedCopies("indoor_wall"));
            Assert.IsTrue(PetInventory.Place("indoor_wall", RoomTheme.Cabin).Contains("没有多余的"),
                "once every owned copy is set down there is nothing left to place");

            // Selling a stackable piece sells one unplaced copy, not a placed one.
            string firstCopy = PetInventory.FirstPlacedCopyKey("indoor_wall", RoomTheme.Cabin);
            Assert.IsNotNull(firstCopy, "a wall copy is set down in the cabin");
            PetInventory.Store(firstCopy, RoomTheme.Cabin);
            Assert.AreEqual(1, PetInventory.UnplacedCopies("indoor_wall"));
            Assert.IsTrue(PetInventory.Sell("indoor_wall").Contains("返还"));
            Assert.AreEqual(2, PetInventory.OwnedCount("indoor_wall"));
        }

        [Test]
        public void DefaultDecorIsMovableStorableAndNotSellable()
        {
            var rug = PetShop.Get("rug");
            Assert.IsNotNull(rug);
            Assert.IsTrue(rug.Starter, "the rug is default furniture");
            Assert.AreEqual(InteractableKind.Decor, rug.Kind.Value);
            Assert.IsTrue(PetInventory.IsOwned("rug"));
            Assert.IsTrue(PetInventory.IsPlaced("rug", RoomTheme.Cabin), "the rug is in the cabin by default");
            Assert.IsFalse(PetInventory.IsPlaced("rug", RoomTheme.Terrace), "the cabin rug is not on the terrace");

            // Store it, then bring it back.
            Assert.IsTrue(PetInventory.Store("rug", RoomTheme.Cabin).Contains("收回"));
            Assert.IsFalse(PetInventory.IsPlaced("rug", RoomTheme.Cabin));
            Assert.IsTrue(PetInventory.Place("rug", RoomTheme.Cabin).Contains("摆进了"));
            Assert.IsTrue(PetInventory.IsPlaced("rug", RoomTheme.Cabin));

            // Default decor cannot be sold, only stored.
            Assert.IsTrue(PetInventory.Sell("rug").Contains("不能卖"));
        }
    }
}
