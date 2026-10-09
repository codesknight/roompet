using NUnit.Framework;
using UnityEngine;

namespace DshPet.Tests
{
    /// <summary>
    /// The shop, the warehouse and the food economy, as pure rules.
    ///
    /// The request is that every shop item has a real interaction, that furniture is bought and
    /// placed rather than assumed, and that a pet without a litter box makes work you get paid
    /// for cleaning. All of that is a set of rules about coins, ownership and placement — which
    /// is exactly what can be pinned down without a scene.
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
        public void TheDefaultRoomHasOnlyTheTwoBowls()
        {
            var placed = PetInventory.Placed();
            Assert.AreEqual(2, placed.Count, "the starter room is the pet and the two bowls, nothing else");
            Assert.IsTrue(placed.ContainsKey(PetShop.FoodBowl), "the food bowl is always placed");
            Assert.IsTrue(placed.ContainsKey(PetShop.WaterBowl), "the water bowl is always placed");
            Assert.IsFalse(PetInventory.IsPlaced("bed"), "the bed must be bought before it appears");
            Assert.IsFalse(PetInventory.IsPlaced("litter_box"), "the litter box must be bought first");
        }

        [Test]
        public void BuyingFoodSpendsCoinsAndAddsMeals()
        {
            DshMobile.PetWallet.Add(100);
            var food = PetShop.Get("food");

            string result = PetInventory.Buy(food);

            Assert.AreEqual(80, DshMobile.PetWallet.Coins, "one bag costs 20");
            Assert.AreEqual(3, PetInventory.Food, "one bag is three meals");
            Assert.IsTrue(result.Contains("3"), "the receipt says how many meals");
        }

        [Test]
        public void BuyingFurnitureOnceOnlyAndSellingRefundsHalf()
        {
            DshMobile.PetWallet.Add(1000);
            var bed = PetShop.Get("bed");

            PetInventory.Buy(bed);
            Assert.IsTrue(PetInventory.IsOwned("bed"), "bought furniture is owned");

            string again = PetInventory.Buy(bed);
            Assert.IsTrue(again.Contains("买过"), "you cannot buy the same bed twice");

            // Place it, then sell it: selling clears both the warehouse and the room.
            PetInventory.Place("bed");
            Assert.IsTrue(PetInventory.IsPlaced("bed"));

            string sold = PetInventory.Sell("bed");
            Assert.IsFalse(PetInventory.IsOwned("bed"), "sold furniture leaves the warehouse");
            Assert.IsFalse(PetInventory.IsPlaced("bed"), "sold furniture leaves the room");
            Assert.IsTrue(sold.Contains("60"), "half of 120 back is 60");
            Assert.AreEqual(1000 - 120 + 60, DshMobile.PetWallet.Coins);
        }

        [Test]
        public void PlaceAndStoreMoveBetweenWarehouseAndRoom()
        {
            DshMobile.PetWallet.Add(1000);
            PetInventory.Buy(PetShop.Get("ball"));

            Assert.AreEqual("还没有这件家具，先去商城买", PetInventory.Place("bath"),
                "an unowned bath cannot be placed");

            PetInventory.Place("ball");
            Assert.IsTrue(PetInventory.IsPlaced("ball"));

            Assert.AreEqual("收回了仓库", PetInventory.Store("ball"));
            Assert.IsFalse(PetInventory.IsPlaced("ball"));
            Assert.IsTrue(PetInventory.IsOwned("ball"), "storing keeps the item");
        }

        [Test]
        public void StarterBowlsCannotBeSoldOrStoredButCanBeMoved()
        {
            Assert.AreEqual("基础家具不能卖", PetInventory.Sell(PetShop.FoodBowl));
            Assert.AreEqual("基础家具不能收起来", PetInventory.Store(PetShop.WaterBowl));

            PetInventory.MoveItem(PetShop.FoodBowl, new Vector2(0f, 1.5f));
            Assert.AreEqual(new Vector2(0f, 1.5f), PetInventory.PositionOf(PetShop.FoodBowl));
        }

        [Test]
        public void PlacementIsClampedInsideTheRoom()
        {
            var inside = PetInventory.ClampToRoom(new Vector2(100f, -100f));
            Assert.LessOrEqual(Mathf.Abs(inside.x), PetInventory.RoomHalf - PetInventory.PlacementMargin);
            Assert.LessOrEqual(Mathf.Abs(inside.y), PetInventory.RoomHalf - PetInventory.PlacementMargin);

            var unchanged = PetInventory.ClampToRoom(new Vector2(1f, 2f));
            Assert.AreEqual(new Vector2(1f, 2f), unchanged, "a legal spot is left alone");
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
        public void EveryShopPropPlacesAnInteractableAndFoodIsConsumable()
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
                else
                {
                    Assert.IsTrue(item.Kind.HasValue, item.Id + " must place an interactable");
                }
            }
        }

        [Test]
        public void ThePetOnlyGoesToTheBowlWhenThereIsFood()
        {
            // 「饭碗空了宠物会挨饿」: the eat behaviour is gated on the pantry, so the pet does
            // not keep trotting to an empty bowl.
            var eat = PetBehaviorLibrary.Get("eat");
            Assert.IsNotNull(eat);
            Assert.IsTrue(eat.NeedsFood, "eat is gated on the pantry");

            var withFood = new PetBehaviorContext
            {
                Hunger = 0.1f, Energy = 0.8f, Joy = 0.8f, Cleanliness = 0.8f, Bladder = 0.8f,
                FoodAvailable = true,
                AvailableTargets = new[] { InteractableKind.Food }
            };
            var noFood = new PetBehaviorContext
            {
                Hunger = 0.1f, Energy = 0.8f, Joy = 0.8f, Cleanliness = 0.8f, Bladder = 0.8f,
                FoodAvailable = false,
                AvailableTargets = new[] { InteractableKind.Food }
            };

            Assert.IsTrue(eat.IsEligible(withFood), "a stocked bowl is dinner");
            Assert.IsFalse(eat.IsEligible(noFood), "an empty bowl is not a destination");
        }
    }
}
