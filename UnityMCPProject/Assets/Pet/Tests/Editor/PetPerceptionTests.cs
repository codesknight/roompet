using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DshPet.Tests
{
    /// <summary>
    /// The pet's spatial awareness and the orders it understands.
    ///
    /// Both are the feature the player asked for in one sentence — 增强宠物的位置感知能力…听得懂主人
    /// 指令 — and both fail in the same invisible way if they are wrong: the pet answers a direction
    /// question confidently and points the wrong way, or it walks off to its bowl in the middle of a
    /// conversation about the weather. Neither shows up as an error, so both are pinned down here.
    /// </summary>
    public class PetPerceptionTests
    {
        private static readonly Vector3 Origin = Vector3.zero;
        private static readonly Vector3 North = Vector3.forward;   // the pet's own forward

        // ------------------------------------------------------------------ directions

        [Test]
        public void Direction_SpeaksInTheEightSectorsAPetWouldUse()
        {
            Assert.AreEqual("正前方", PetPerception.DirectionOf(Origin, North, new Vector3(0f, 0f, 3f)));
            Assert.AreEqual("右前方", PetPerception.DirectionOf(Origin, North, new Vector3(3f, 0f, 3f)));
            Assert.AreEqual("右边", PetPerception.DirectionOf(Origin, North, new Vector3(3f, 0f, 0f)));
            Assert.AreEqual("右后方", PetPerception.DirectionOf(Origin, North, new Vector3(3f, 0f, -3f)));
            Assert.AreEqual("正后方", PetPerception.DirectionOf(Origin, North, new Vector3(0f, 0f, -3f)));
            Assert.AreEqual("左后方", PetPerception.DirectionOf(Origin, North, new Vector3(-3f, 0f, -3f)));
            Assert.AreEqual("左边", PetPerception.DirectionOf(Origin, North, new Vector3(-3f, 0f, 0f)));
            Assert.AreEqual("左前方", PetPerception.DirectionOf(Origin, North, new Vector3(-3f, 0f, 3f)));
        }

        [Test]
        public void Direction_FollowsThePetsOwnFacing()
        {
            // The pet turns to face the owner when spoken to, so "your left" has to be the pet's left:
            // a bowl that was on the right of a pet facing +Z is on the left of one facing -Z.
            var backwards = new Vector3(0f, 0f, -1f);
            Assert.AreEqual("左边", PetPerception.DirectionOf(Origin, backwards, new Vector3(3f, 0f, 0f)));
            Assert.AreEqual("右边", PetPerception.DirectionOf(Origin, backwards, new Vector3(-3f, 0f, 0f)));
        }

        [Test]
        public void Direction_HandlesTheDegenerateCases()
        {
            // Standing on it: "which way" is the wrong question, and answering 正前方 would be a lie.
            Assert.AreEqual("就在你脚边", PetPerception.DirectionOf(Origin, North, new Vector3(0.1f, 0f, 0.05f)));

            // A pet with no facing at all (freshly spawned, before the first Turn) must not produce
            // a NaN direction or an index out of range.
            Assert.IsNotEmpty(PetPerception.DirectionOf(Origin, Vector3.zero, new Vector3(2f, 0f, 0f)));
            Assert.IsNotEmpty(PetPerception.DirectionOf(Origin, North, new Vector3(2f, 5f, 0f)),
                "height must not change which way something is");
        }

        [Test]
        public void Steps_AreWholeNumbersAndNeverZero()
        {
            Assert.AreEqual(1, PetPerception.StepsTo(0f), "something at your feet is one step, not none");
            Assert.AreEqual(1, PetPerception.StepsTo(0.4f));
            Assert.GreaterOrEqual(PetPerception.StepsTo(PetPerception.StepLength * 4f), 3);

            int previous = 0;
            for (float distance = 0f; distance < 12f; distance += 0.3f)
            {
                int steps = PetPerception.StepsTo(distance);
                Assert.GreaterOrEqual(steps, previous, "further away cannot be fewer steps");
                previous = steps;
            }
        }

        [Test]
        public void Where_ReadsLikeASentence()
        {
            string line = PetPerception.WhereSentence(Origin, North, new Vector3(3f, 0f, 3f));
            Assert.IsTrue(line.Contains("右前方"), line);
            Assert.IsTrue(line.Contains("步"), line);
            Assert.IsFalse(line.Contains("0 步"), "nobody walks zero steps: " + line);
        }

        [Test]
        public void RoomPlace_SaysWhereInTheRoomThePetIs()
        {
            Assert.AreEqual("房间正中间", PetPerception.RoomPlace(Vector3.zero, 14f));

            string corner = PetPerception.RoomPlace(new Vector3(-6f, 0f, 6f), 14f);
            Assert.IsTrue(corner.Contains("左边"), corner);
            Assert.IsTrue(corner.Contains("靠里那侧"), corner);

            string door = PetPerception.RoomPlace(new Vector3(6f, 0f, -6f), 14f);
            Assert.IsTrue(door.Contains("右边"), door);
            Assert.IsTrue(door.Contains("靠门那侧"), door);

            Assert.IsNotEmpty(PetPerception.RoomPlace(Vector3.zero, 0f), "an unknown room size is not a crash");
        }

        // ------------------------------------------------------------------ what it sees

        private static List<PetTarget> Room() => new List<PetTarget>
        {
            new PetTarget { Name = "饭碗", Kind = InteractableKind.Food,
                Position = new Vector3(3f, 0f, 3f), Ready = true },
            new PetTarget { Name = "水碗", Kind = InteractableKind.Water,
                Position = new Vector3(-3f, 0f, 1f), Ready = true },
            new PetTarget { Name = "猫砂盆", Kind = InteractableKind.Toilet,
                Position = new Vector3(1f, 0f, -4f), Ready = false }
        };

        [Test]
        public void Visible_IsNearestFirstAndForgetsTheFarSideOfTheWorld()
        {
            var targets = Room();
            targets.Add(new PetTarget
            {
                Name = "很远的东西", Kind = InteractableKind.Toy,
                Position = new Vector3(0f, 0f, PetPerception.NoticeRange + 5f), Ready = true
            });

            var visible = PetPerception.Visible(Origin, North, targets);

            Assert.AreEqual(3, visible.Count, "the far one is out of sight");
            Assert.AreEqual("水碗", visible[0].Name, "nearest first");

            // Sorted, not merely as luck would have it: the order of the lines is the order the pet
            // thinks about the room in, and a stable one keeps the prompt stable.
            float previous = -1f;
            foreach (var sighting in visible)
            {
                float distance = new Vector2(sighting.Position.x, sighting.Position.z).magnitude;
                Assert.GreaterOrEqual(distance, previous, "the list is not sorted by distance");
                previous = distance;
            }

            Assert.AreEqual(0, PetPerception.Visible(Origin, North, null).Count, "no room is not a crash");
        }

        [Test]
        public void Describe_TellsTheModelWhereEverythingIs()
        {
            string block = PetPerception.Describe(Origin, North, Room(), 14f, true,
                new Vector3(0f, 0f, 1.5f));

            Assert.IsTrue(block.Contains("14"), "the size of the room is part of knowing it: " + block);
            Assert.IsTrue(block.Contains("房间正中间"), block);

            foreach (var target in Room())
            {
                Assert.IsTrue(block.Contains("「" + target.Name + "」"), target.Name + " is missing:\n" + block);
            }

            Assert.IsTrue(block.Contains("右前方"), "the bowl's direction is missing:\n" + block);
            Assert.IsTrue(block.Contains("刚用过"), "a bowl on cooldown has to say so:\n" + block);
            Assert.IsTrue(block.Contains("主人"), "the owner's position is missing:\n" + block);
            Assert.IsTrue(block.Contains("不是猜的"), "the model has to be told to trust this:\n" + block);
        }

        [Test]
        public void Describe_SurvivesAnEmptyRoom()
        {
            string empty = PetPerception.Describe(Origin, North, new List<PetTarget>(), 0f, false, Vector3.zero);

            Assert.IsNotEmpty(empty);
            Assert.IsTrue(empty.Contains("没有"), "an empty room says so rather than listing nothing: " + empty);
            Assert.IsFalse(empty.Contains("主人："), "there is no owner in this case");
        }

        [Test]
        public void Describe_NamesEveryKindOfThingEvenWhenTheRoomDidNotLabelIt()
        {
            // The room labels its objects, but a new kind of object added later may not be labelled
            // yet: the pet must still have a word for it rather than saying 「东西」 about its dinner.
            foreach (InteractableKind kind in System.Enum.GetValues(typeof(InteractableKind)))
            {
                string name = PetPerception.NameOf(kind);
                Assert.IsFalse(string.IsNullOrEmpty(name), kind + " has no name");
            }

            Assert.AreEqual("饭碗", PetPerception.NameOf(InteractableKind.Food));
            Assert.AreNotEqual(PetPerception.NameOf(InteractableKind.Food),
                PetPerception.NameOf(InteractableKind.Water), "the two bowls must not share a word");
        }

        // ------------------------------------------------------------------ orders

        [Test]
        public void Orders_AreUnderstood()
        {
            AssertOrder("过来", PetOrderKind.Come);
            AssertOrder("快过来", PetOrderKind.Come);
            AssertOrder("主人过来一下", PetOrderKind.Come);
            AssertOrder("吃饭", PetOrderKind.Eat);
            AssertOrder("去吃饭吧", PetOrderKind.Eat);
            AssertOrder("喝水", PetOrderKind.Drink);
            AssertOrder("去喝水", PetOrderKind.Drink);
            AssertOrder("拿球", PetOrderKind.Fetch);
            AssertOrder("把球拿过来", PetOrderKind.Fetch);
            AssertOrder("捡球", PetOrderKind.Fetch);
            AssertOrder("陪我玩", PetOrderKind.Play);
            AssertOrder("睡觉", PetOrderKind.Sleep);
            AssertOrder("去睡觉", PetOrderKind.Sleep);
            AssertOrder("上厕所", PetOrderKind.Toilet);
            AssertOrder("去洗澡", PetOrderKind.Bath);
            AssertOrder("给我梳毛", PetOrderKind.Groom);
            AssertOrder("跟着我", PetOrderKind.Follow);
            AssertOrder("别动", PetOrderKind.Stay);
            AssertOrder("站住", PetOrderKind.Stay);
        }

        [Test]
        public void Conversation_IsNotAnOrder()
        {
            // The whole risk of a parser like this is the false positive: the player says something
            // ordinary and the pet walks off to its bowl mid-sentence.
            AssertNotAnOrder("我今天吃了饭");
            AssertNotAnOrder("你吃饭了吗");
            AssertNotAnOrder("我吃过饭了");
            AssertNotAnOrder("我在外面吃饭了");
            AssertNotAnOrder("为什么不吃东西");
            AssertNotAnOrder("你喜欢吃什么");
            AssertNotAnOrder("今天天气不错，我们聊点什么吧");
            AssertNotAnOrder("讲个故事吧");
            AssertNotAnOrder("我想你了");
            AssertNotAnOrder("我今天有点累，陪我聊聊天好吗");
            AssertNotAnOrder("");
            AssertNotAnOrder(null);
        }

        [Test]
        public void Orders_CarryTheObjectTheyAreAbout()
        {
            Assert.AreEqual(InteractableKind.Food, PetCommands.Parse("去吃饭").Target);
            Assert.AreEqual(InteractableKind.Water, PetCommands.Parse("喝水").Target);
            Assert.AreEqual(InteractableKind.Ball, PetCommands.Parse("拿球").Target);
            Assert.AreEqual(InteractableKind.Toilet, PetCommands.Parse("去猫砂盆").Target);
        }

        [Test]
        public void Where_QuestionsAreAnsweredFromPerception()
        {
            var food = PetCommands.Parse("饭碗在哪");
            Assert.AreEqual(PetOrderKind.AskWhere, food.Kind);
            Assert.AreEqual(InteractableKind.Food, food.Target);

            Assert.AreEqual(InteractableKind.Water, PetCommands.Parse("水碗在哪里").Target);
            Assert.AreEqual(InteractableKind.Ball, PetCommands.Parse("球在哪儿").Target);

            var owner = PetCommands.Parse("主人在哪");
            Assert.AreEqual(PetOrderKind.AskWhere, owner.Kind);
            Assert.IsTrue(owner.AboutOwner, "that question is about the owner, not about an object");

            // A bare "在哪" is a question about the room, and the pet answers it as one.
            var vague = PetCommands.Parse("东西都在哪");
            Assert.AreEqual(PetOrderKind.AskWhere, vague.Kind);
        }

        [Test]
        public void EveryOrderHasSomethingToSay()
        {
            foreach (PetOrderKind kind in System.Enum.GetValues(typeof(PetOrderKind)))
            {
                if (kind == PetOrderKind.None || kind == PetOrderKind.AskWhere) continue;

                var order = new PetOrder { Kind = kind };
                Assert.IsFalse(string.IsNullOrEmpty(PetCommands.Reply(order)), kind + " has no reply");
                Assert.IsFalse(string.IsNullOrEmpty(PetCommands.CannotDo(order)), kind + " has no refusal");
                Assert.AreNotEqual(PetAction.Idle, PetCommands.Action(order), kind + " does nothing visible");
            }
        }

        [Test]
        public void Replies_VaryWithTheFlavourWithoutChangingTheAnswer()
        {
            var first = PetCommands.Reply(new PetOrder { Kind = PetOrderKind.Come }, 0);
            Assert.IsNotEmpty(first);
            for (int i = 0; i < 12; i++)
            {
                Assert.IsNotEmpty(PetCommands.Reply(new PetOrder { Kind = PetOrderKind.Come }, i),
                    "every flavour has to produce a line");
            }
        }

        // ------------------------------------------------------------------ the prompt

        [Test]
        public void Prompt_CarriesWhatThePetSees()
        {
            var ctx = new PetContext
            {
                PetName = "豆豆",
                SpeciesName = "狐狸",
                Perception = PetPerception.Describe(Origin, North, Room(), 14f, true,
                    new Vector3(0f, 0f, 1.5f))
            };

            string prompt = PetPrompting.BuildSystemPrompt(ctx);

            Assert.IsTrue(prompt.Contains("【你看到的东西】"), prompt);
            Assert.IsTrue(prompt.Contains("「饭碗」"), "the bowl has to be in the prompt:\n" + prompt);
            Assert.IsTrue(prompt.Contains("主人："), prompt);

            // And a build with no perception at all does not advertise an empty section.
            string bare = PetPrompting.BuildSystemPrompt(new PetContext { PetName = "豆豆" });
            Assert.IsFalse(bare.Contains("【你看到的东西】"), bare);
        }

        // ------------------------------------------------------------------ the table

        [Test]
        public void ThePetCanGoToItsOwnerOnItsOwn()
        {
            // "知道主人的位置并能和主人互动" is only true if the pet actually does it unprompted:
            // the behaviour has to exist in the table, be ambient, and target the owner.
            var row = PetBehaviorLibrary.Get("come_to_owner");
            Assert.IsNotNull(row, "the pet has no behaviour for approaching the owner");
            Assert.AreEqual(BehaviorDrive.Passive, row.Drive);
            Assert.AreEqual("Player", row.TargetKind);
            Assert.IsTrue(row.AllowsSpeech, "approaching quietly reads as a pathing bug");
            Assert.Greater(row.AmbientWeight(new PetBehaviorContext()), 0f);
        }

        [Test]
        public void EveryBehaviourTargetsSomethingThatExists()
        {
            foreach (var row in PetBehaviorLibrary.All)
            {
                if (string.IsNullOrEmpty(row.TargetKind)) continue;

                if (row.TargetKind == "Player") continue;

                InteractableKind parsed;
                Assert.IsTrue(System.Enum.TryParse(row.TargetKind, out parsed),
                    row.Id + " targets 「" + row.TargetKind + "」, which is not a kind of object");
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void AssertOrder(string text, PetOrderKind expected)
        {
            var order = PetCommands.Parse(text);
            Assert.AreEqual(expected, order.Kind, "「" + text + "」 was read as " + order.Kind);
        }

        private static void AssertNotAnOrder(string text)
        {
            var order = PetCommands.Parse(text);
            Assert.AreEqual(PetOrderKind.None, order.Kind,
                "「" + text + "」 was taken as an order (" + order.Kind + ")");
        }
    }
}
