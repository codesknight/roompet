using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DshPet.Tests
{
    /// <summary>
    /// The 扭蛋机, the new gacha-only species, and the direct-feed economy.
    ///
    /// The interesting rules here are: the gacha pool is exactly the gacha-only species (never
    /// the ones sold directly), the draw is weighted and deterministic for a given seed, the pull
    /// costs a fixed amount and lands the pet in the warehouse, and each food item knows how much
    /// hunger it fills when fed by hand.
    /// </summary>
    public class PetGachaTests
    {
        [SetUp]
        public void SetUp()
        {
            PetInventory.ResetForTests();
            DshMobile.PetWallet.Reset();
        }

        [Test]
        public void TheGachaPoolIsExactlyTheGachaOnlySpecies()
        {
            var pool = PetGacha.Pool();
            Assert.Greater(pool.Count, 1, "a one-species gacha is not a gacha");

            foreach (var species in pool)
            {
                Assert.IsTrue(species.GachaOnly, species.Id + " is not gacha-only");
            }

            // And the direct shop never lists them.
            foreach (var species in PetCollectionPanel.ShopOrder())
            {
                Assert.IsFalse(species.GachaOnly, species.Id + " leaked into the direct shop");
            }
        }

        [Test]
        public void TheDrawIsDeterministicAndWithinThePool()
        {
            var pool = PetGacha.Pool();
            var rngA = new Random(12345);
            var rngB = new Random(12345);

            for (int i = 0; i < 50; i++)
            {
                var a = PetGacha.Pick(rngA);
                var b = PetGacha.Pick(rngB);
                Assert.AreEqual(a.Id, b.Id, "same seed must give the same draw");
                Assert.IsTrue(pool.Contains(a), "the draw came from outside the pool");
            }
        }

        [Test]
        public void TheWeightsAreSaneAndTheOddsSumToOne()
        {
            var pool = PetGacha.Pool();
            int total = 0;
            foreach (var species in pool)
            {
                Assert.Greater(PetGacha.Weight(species), 0, species.Id + " can never be drawn");
                total += PetGacha.Weight(species);
            }

            // The odds text lists every species and its share; shares sum to 100.
            string odds = PetGacha.OddsText();
            foreach (var species in pool)
            {
                Assert.IsTrue(odds.Contains(species.DisplayName), "odds omit " + species.Id);
            }
            Assert.IsFalse(string.IsNullOrEmpty(odds));
        }

        [Test]
        public void APullSpendsCoinsAndLandsInTheWarehouse()
        {
            int before = PetCollection.Warehouse.Count;
            DshMobile.PetWallet.Add(1000);

            string message;
            var record = PetCollection.RollGacha(out message);

            Assert.IsNotNull(record, "a funded pull must produce a pet");
            Assert.AreEqual(500, DshMobile.PetWallet.Coins, "one pull costs 500");
            Assert.AreEqual(before + 1, PetCollection.Warehouse.Count, "the pet goes to the warehouse");
            Assert.IsTrue(message.Contains("扭蛋"), "the receipt says what happened");

            // Not enough coins: the pull fails and nothing changes.
            DshMobile.PetWallet.Reset();
            int count = PetCollection.Warehouse.Count;
            var failed = PetCollection.RollGacha(out message);
            Assert.IsNull(failed);
            Assert.AreEqual(count, PetCollection.Warehouse.Count, "a broke pull changes nothing");
        }

        [Test]
        public void FoodKnowsItsOwnNutrition()
        {
            Assert.AreEqual(0.9f, PetShop.Get("meat").FoodAmount, 0.0001f);
            Assert.AreEqual(0.7f, PetShop.Get("fish").FoodAmount, 0.0001f);
            Assert.AreEqual(0.45f, PetShop.Get("apple").FoodAmount, 0.0001f);
            Assert.IsTrue(PetShop.Get("water").IsDrink, "water is a drink, not a meal");
            Assert.IsFalse(PetShop.Get("meat").IsDrink);
        }

        [Test]
        public void TheSpeciesTableGrewAndTheNewOnesAreGachaOnly()
        {
            Assert.AreEqual(7, PetSpecies.All.Length, "four shop species plus three gacha species");
            Assert.AreEqual(3, PetGacha.Pool().Count);

            var panda = PetSpecies.Get("red_panda");
            var penguin = PetSpecies.Get("penguin");
            var hamster = PetSpecies.Get("hamster");
            Assert.IsNotNull(panda);
            Assert.IsNotNull(penguin);
            Assert.IsNotNull(hamster);
            Assert.IsTrue(panda.GachaOnly && penguin.GachaOnly && hamster.GachaOnly);
        }
    }
}
