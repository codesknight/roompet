using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DshPet.Tests
{
    /// <summary>
    /// Covers the journal (the "smart notebook") and the proactive/passive behaviour table.
    /// Both are pure logic, and both fail silently in ways that are hard to spot by eye:
    /// a journal that forgets the player's birthday, or a pet whose "proactive" behaviour
    /// never actually fires.
    /// </summary>
    public class PetMemoryAndBehaviorTests
    {
        // ------------------------------------------------------------------- journal

        private static PetJournal NewJournal()
        {
            var journal = new PetJournal();
            journal.Bind("test");
            journal.Clear();
            return journal;
        }

        [Test]
        public void Journal_FilesEntriesUnderTheirDay()
        {
            var journal = NewJournal();
            var day = new DateTime(2026, 3, 14, 9, 30, 0);
            var other = new DateTime(2026, 3, 15, 9, 30, 0);

            journal.Add(MemoryKind.Care, "吃了东西", "", 0.3f, false, day);
            journal.Add(MemoryKind.Play, "玩了球", "", 0.3f, false, day);
            journal.Add(MemoryKind.Rest, "去睡觉了", "", 0.3f, false, other);

            Assert.AreEqual(2, journal.CountOn(day));
            Assert.AreEqual(1, journal.CountOn(other));
            Assert.AreEqual(0, journal.CountOn(new DateTime(2026, 3, 16)));
            Assert.AreEqual(3, journal.ForMonth(2026, 3).Count, "all three entries are in March");
            Assert.AreEqual(0, journal.ForMonth(2026, 4).Count, "April must not report March's entries");
            Assert.AreEqual(0, journal.ForMonth(2025, 3).Count, "the year has to match too");
        }

        // ------------------------------------------------------------------ deleting

        [Test]
        public void Journal_DeletingOneEntryUpdatesEveryIndex()
        {
            // The day index is what the calendar reads, so a delete that only touched the flat
            // list would leave the notebook claiming a day has entries it no longer has.
            var journal = NewJournal();
            var day = new DateTime(2026, 3, 14, 9, 30, 0);
            journal.Add(MemoryKind.Care, "吃了东西", "", 0.3f, false, day);
            var second = journal.Add(MemoryKind.Play, "玩了球", "", 0.3f, false, day);
            journal.Add(MemoryKind.Rest, "去睡觉了", "", 0.3f, false, day);

            Assert.AreEqual(3, journal.Count);

            Assert.IsTrue(journal.Delete(second));
            Assert.AreEqual(2, journal.Count);
            Assert.AreEqual(2, journal.CountOn(day), "the day index has to agree");
            Assert.AreEqual(0, journal.ForDay(day).FindAll(e => e.Title == "玩了球").Count);
            Assert.AreEqual(2, journal.ForMonth(2026, 3).Count);

            // Deleting something that is not there is a no-op, not a crash.
            Assert.IsFalse(journal.Delete(second));
            Assert.AreEqual(2, journal.Count);
        }

        [Test]
        public void Journal_PinnedEntriesAreDeletable()
        {
            // "Pinned" means "never pruned automatically" — it must not mean "cannot be removed".
            // A row that quietly refuses to be deleted is worse than either alternative.
            var journal = NewJournal();
            var day = new DateTime(2026, 3, 14);
            var pinned = journal.Add(MemoryKind.Preference, "主人喜欢草莓", "", 0.9f, true, day);
            Assert.AreEqual(1, journal.PinnedCount);

            Assert.IsTrue(journal.Delete(pinned));
            Assert.AreEqual(0, journal.Count);
            Assert.AreEqual(0, journal.PinnedCount);
        }

        [Test]
        public void Journal_DeletingADayAndAMonthTakesExactlyTheirEntries()
        {
            var journal = NewJournal();
            journal.Add(MemoryKind.Care, "3 月 14 日", "", 0.3f, false, new DateTime(2026, 3, 14, 8, 0, 0));
            journal.Add(MemoryKind.Care, "3 月 14 日晚", "", 0.3f, false, new DateTime(2026, 3, 14, 20, 0, 0));
            journal.Add(MemoryKind.Care, "3 月 15 日", "", 0.3f, false, new DateTime(2026, 3, 15, 8, 0, 0));
            journal.Add(MemoryKind.Care, "4 月 1 日", "", 0.3f, false, new DateTime(2026, 4, 1, 8, 0, 0));

            Assert.AreEqual(2, journal.DeleteDay(new DateTime(2026, 3, 14)));
            Assert.AreEqual(2, journal.Count, "only that day went");
            Assert.AreEqual(0, journal.CountOn(new DateTime(2026, 3, 14)));
            Assert.AreEqual(0, journal.DeleteDay(new DateTime(2026, 3, 14)), "deleting an empty day is a no-op");

            Assert.AreEqual(1, journal.DeleteMonth(2026, 3));
            Assert.AreEqual(1, journal.Count);
            Assert.AreEqual(1, journal.ForMonth(2026, 4).Count, "April survived the March sweep");
        }

        [Test]
        public void Journal_StorageSizeIsReportedInReadableUnits()
        {
            Assert.AreEqual("0 B", PetJournal.FormatBytes(0));
            Assert.AreEqual("512 B", PetJournal.FormatBytes(512));
            Assert.AreEqual("1.0 KB", PetJournal.FormatBytes(1024));
            Assert.AreEqual("1.0 MB", PetJournal.FormatBytes(1024 * 1024));
        }

        [Test]
        public void Journal_DaysAreOrderedChronologically()
        {
            var journal = NewJournal();
            var day = new DateTime(2026, 3, 14);
            journal.Add(MemoryKind.Rest, "晚上", "", 0.3f, false, day.AddHours(22));
            journal.Add(MemoryKind.Care, "早上", "", 0.3f, false, day.AddHours(8));

            var entries = journal.ForDay(day);
            Assert.AreEqual("早上", entries[0].Title);
            Assert.AreEqual("晚上", entries[1].Title);
        }

        [Test]
        public void Journal_CollapsesAChattyDayIntoOneChatRow()
        {
            var journal = NewJournal();
            var day = new DateTime(2026, 3, 14, 10, 0, 0);

            journal.NoteConversation("你好", "嗨！", day);
            journal.NoteConversation("在干嘛", "在等你", day.AddMinutes(5));
            journal.NoteConversation("晚安", "晚安", day.AddMinutes(10));

            var chatEntries = 0;
            foreach (var entry in journal.ForDay(day))
            {
                if (entry.Kind == MemoryKind.Chat) chatEntries++;
            }

            Assert.AreEqual(1, chatEntries, "a chatty day must not bury the calendar in rows");
            Assert.IsTrue(journal.ForDay(day)[0].Detail.Contains("晚安"), "the transcript should keep the latest turn");
        }

        [Test]
        public void Journal_ConversationsOnDifferentDaysStaySeparate()
        {
            var journal = NewJournal();
            journal.NoteConversation("a", "b", new DateTime(2026, 3, 14, 10, 0, 0));
            journal.NoteConversation("c", "d", new DateTime(2026, 3, 15, 10, 0, 0));

            Assert.AreEqual(1, journal.CountOn(new DateTime(2026, 3, 14)));
            Assert.AreEqual(1, journal.CountOn(new DateTime(2026, 3, 15)));
        }

        [Test]
        public void Journal_RememberedFactsArePinnedAndDeduped()
        {
            var journal = NewJournal();
            journal.Remember("主人说明天有考试");
            journal.Remember("主人说明天有考试");
            journal.Remember("主人喜欢喝美式");

            Assert.AreEqual(2, journal.LongTerm().Count);
            foreach (var entry in journal.LongTerm())
            {
                Assert.IsTrue(entry.Pinned, "long-term memory must be pinned");
                Assert.AreEqual(MemoryKind.Preference, entry.Kind);
            }
        }

        [Test]
        public void Journal_PruningNeverDropsPinnedEntries()
        {
            var journal = NewJournal();
            journal.Remember("这是必须记住的");

            var day = new DateTime(2026, 3, 14);
            for (int i = 0; i < PetJournal.DefaultMaxEntries + 60; i++)
            {
                journal.Add(MemoryKind.Mood, "琐事 " + i, "", 0.1f, false, day.AddSeconds(i));
            }

            Assert.LessOrEqual(journal.Count, PetJournal.DefaultMaxEntries);
            Assert.IsTrue(journal.LongTerm().Exists(e => e.Title == "这是必须记住的"),
                "pruning dropped a pinned fact");
        }

        [Test]
        public void Journal_SearchLooksInTitlesAndDetails()
        {
            var journal = NewJournal();
            journal.Add(MemoryKind.Play, "玩了球", "追着球跑了两圈", 0.3f);

            Assert.AreEqual(1, journal.Search("球").Count);
            Assert.AreEqual(1, journal.Search("两圈").Count);
            Assert.AreEqual(0, journal.Search("不存在的东西").Count);
            Assert.AreEqual(0, journal.Search("").Count);
        }

        [Test]
        public void Journal_DigestCoversPreviousDaysAndToday()
        {
            var journal = NewJournal();
            var yesterday = DateTime.Now.Date.AddDays(-1).AddHours(15);
            journal.Add(MemoryKind.Care, "吃了东西", "", 0.3f, false, yesterday);
            journal.Add(MemoryKind.Play, "玩了球", "", 0.3f, false, yesterday);
            journal.Add(MemoryKind.Rest, "今天睡的", "", 0.3f, false, DateTime.Now.Date.AddHours(9));

            string digest = journal.RecentDigest(3);
            Assert.IsTrue(digest.Contains("吃了东西"), digest);
            Assert.IsTrue(digest.Contains("玩了球"), digest);
            Assert.IsTrue(digest.Contains("昨天"), "previous days are labelled");
            Assert.IsTrue(digest.Contains("今天"), digest);
            Assert.IsTrue(digest.Contains("今天睡的"), "today is included so a new pet has context");
        }

        [Test]
        public void Journal_DigestSkipsChatAndDuplicateTitles()
        {
            var journal = NewJournal();
            var today = DateTime.Now.Date.AddHours(10);
            journal.NoteConversation("你好", "嗨", today);
            journal.Add(MemoryKind.Care, "吃了东西", "", 0.3f, false, today.AddMinutes(1));
            journal.Add(MemoryKind.Care, "吃了东西", "", 0.3f, false, today.AddMinutes(2));

            string digest = journal.RecentDigest(1);
            Assert.IsFalse(digest.Contains("嗨"), "the transcript already carries the chat");
            int first = digest.IndexOf("吃了东西", StringComparison.Ordinal);
            int second = digest.IndexOf("吃了东西", first + 1, StringComparison.Ordinal);
            Assert.AreEqual(-1, second, "the same title should not repeat inside the digest");
        }

        [Test]
        public void Journal_KindsOnFeedsTheCalendarDots()
        {
            var journal = NewJournal();
            var day = new DateTime(2026, 3, 14);
            journal.Add(MemoryKind.Care, "a", "", 0.3f, false, day);
            journal.Add(MemoryKind.Care, "b", "", 0.3f, false, day);
            journal.Add(MemoryKind.Play, "c", "", 0.3f, false, day);

            var kinds = journal.KindsOn(day);
            Assert.AreEqual(2, kinds.Count, "distinct kinds only");
            Assert.Contains(MemoryKind.Care, kinds);
            Assert.Contains(MemoryKind.Play, kinds);
        }

        // ------------------------------------------------------------------ behaviour

        private static PetBehaviorContext Context(float hunger = 0.9f, float energy = 0.9f,
            float joy = 0.9f, float clean = 0.9f, float affection = 0.5f, float hour = 12f,
            bool ballLoose = false, params InteractableKind[] targets)
        {
            return new PetBehaviorContext
            {
                Hunger = hunger,
                Energy = energy,
                Joy = joy,
                Cleanliness = clean,
                Affection = affection,
                Mood = PetMood.Content,
                HourOfDay = hour,
                PlayerPresent = true,
                BallLoose = ballLoose,
                AvailableTargets = targets
            };
        }

        [Test]
        public void Behavior_PassiveRowsNeverScore()
        {
            var context = Context(0.1f, 0.1f, 0.1f, 0.1f, 0.1f);
            foreach (var behavior in PetBehaviorLibrary.All)
            {
                if (behavior.Drive != BehaviorDrive.Passive) continue;
                Assert.AreEqual(0f, behavior.Score(context), behavior.Id + " is passive but scored");
            }
        }

        [Test]
        public void Behavior_HungerActivatesEatAndScalesWithUrgency()
        {
            var eat = PetBehaviorLibrary.Get("eat");
            Assert.IsNotNull(eat);

            var hungry = Context(hunger: 0.15f, targets: InteractableKind.Food);
            var starving = Context(hunger: 0.02f, targets: InteractableKind.Food);
            var full = Context(hunger: 0.9f, targets: InteractableKind.Food);

            Assert.Greater(eat.Score(hungry), 0f, "a hungry pet should want to eat");
            Assert.Greater(eat.Score(starving), eat.Score(hungry), "more hungry means more urgent");
            Assert.AreEqual(0f, eat.Score(full), "a full pet should not go to the bowl");
        }

        [Test]
        public void Behavior_NeedsAnAvailableTarget()
        {
            var eat = PetBehaviorLibrary.Get("eat");
            Assert.AreEqual(0f, eat.Score(Context(hunger: 0.1f)), "no reachable bowl means no trip");
            Assert.Greater(eat.Score(Context(hunger: 0.1f, targets: InteractableKind.Food)), 0f);
        }

        [Test]
        public void Behavior_TimeWindowsIncludeMidnightWrap()
        {
            var nightNap = PetBehaviorLibrary.Get("nap_night");
            Assert.IsNotNull(nightNap);
            Assert.AreEqual(22f, nightNap.MinHour);
            Assert.AreEqual(6f, nightNap.MaxHour);

            var targets = new[] { InteractableKind.Bed };
            Assert.Greater(nightNap.Score(Context(energy: 0.5f, hour: 23f, targets: targets)), 0f, "23:00 is inside");
            Assert.Greater(nightNap.Score(Context(energy: 0.5f, hour: 2f, targets: targets)), 0f, "02:00 wraps into the window");
            Assert.AreEqual(0f, nightNap.Score(Context(energy: 0.5f, hour: 14f, targets: targets)), "14:00 is outside");
        }

        [Test]
        public void Behavior_PassiveGatesRespectNeeds()
        {
            var grumble = PetBehaviorLibrary.Get("grumble");
            Assert.IsNotNull(grumble);
            Assert.IsTrue(grumble.IsEligible(Context(joy: 0.1f)), "miserable pets may grumble");
            Assert.IsFalse(grumble.IsEligible(Context(joy: 0.9f)), "happy pets must not grumble");
        }

        [Test]
        public void Behavior_LibraryIsWellFormed()
        {
            var ids = new HashSet<string>();
            foreach (var behavior in PetBehaviorLibrary.All)
            {
                Assert.IsTrue(ids.Add(behavior.Id), "duplicate behaviour id: " + behavior.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(behavior.Label), behavior.Id + " has no label");
                Assert.Greater(behavior.Cooldown, 0f, behavior.Id + " has no cooldown");
                Assert.Greater(behavior.Weight, 0f, behavior.Id + " has no weight");
            }

            Assert.GreaterOrEqual(PetBehaviorLibrary.ProactiveCount, 5, "proactive table is too thin");
            Assert.GreaterOrEqual(PetBehaviorLibrary.PassiveCount, 5, "passive table is too thin");
        }

        [Test]
        public void Scheduler_PicksTheMostUrgentProactiveRow()
        {
            var scheduler = new PetBehaviorScheduler(1234);
            var pool = new List<PetBehavior>(PetBehaviorLibrary.All);

            // Starving but rested: eat should beat the others.
            var hungry = Context(hunger: 0.05f, energy: 0.9f, joy: 0.9f, clean: 0.9f,
                targets: new[] { InteractableKind.Food, InteractableKind.Bed, InteractableKind.Ball });

            var chosen = scheduler.PickBestProactive(hungry, pool);
            Assert.IsNotNull(chosen);
            Assert.AreEqual("eat", chosen.Id, "starvation should outrank everything else");
        }

        [Test]
        public void Scheduler_RespectsCooldowns()
        {
            var scheduler = new PetBehaviorScheduler(99);
            var pool = new List<PetBehavior>(PetBehaviorLibrary.All);
            var hungry = Context(hunger: 0.05f, targets: new[] { InteractableKind.Food });

            var first = scheduler.PickBestProactive(hungry, pool);
            Assert.IsNotNull(first);

            // Burn the proactive timer so the next Tick is allowed to choose again.
            scheduler.Tick(10f, hungry, pool, true);
            var second = scheduler.PickBestProactive(hungry, pool);

            Assert.AreNotSame(first, second, "a behaviour must not repeat while on cooldown");
            Assert.IsFalse(scheduler.IsReady(first));
        }

        [Test]
        public void Scheduler_TimersDoNotBurnWhileThePetIsBusy()
        {
            var scheduler = new PetBehaviorScheduler(7);
            var pool = new List<PetBehavior>(PetBehaviorLibrary.All);
            var context = Context(hunger: 0.05f, targets: new[] { InteractableKind.Food });

            // 60 seconds of "busy" must not silently consume the whole proactive budget.
            for (int i = 0; i < 60; i++) scheduler.Tick(1f, context, pool, false);

            Assert.IsNull(scheduler.LastProactive, "nothing should have fired while busy");
        }

        [Test]
        public void Scheduler_PassiveNeverRepeatsBackToBack()
        {
            var scheduler = new PetBehaviorScheduler(4242);
            var pool = new List<PetBehavior>(PetBehaviorLibrary.All);
            var context = Context();

            for (int i = 0; i < 40; i++)
            {
                scheduler.Tick(30f, context, pool, true);
                var last = scheduler.LastPassive;
                if (last == null) continue;

                var next = scheduler.PickPassive(context, pool);
                if (next != null)
                {
                    Assert.AreNotSame(last, next,
                        "the same ambient quirk played twice in a row: " + last.Id);
                }
            }
        }

        [Test]
        public void Scheduler_FiresPassiveQuirksOverTime()
        {
            var scheduler = new PetBehaviorScheduler(2026);
            var pool = new List<PetBehavior>(PetBehaviorLibrary.All);
            var context = Context();
            int fired = 0;

            for (int i = 0; i < 200; i++)
            {
                var chosen = scheduler.Tick(1f, context, pool, true);
                if (chosen != null && chosen.Drive == BehaviorDrive.Passive) fired++;
            }

            Assert.Greater(fired, 3, "ambient quirks should actually fire during a long idle stretch");
        }

        // -------------------------------------------------------------- throw & fetch

        private static PetBall NewBall()
        {
            var go = new GameObject("TestBall");
            var ball = go.AddComponent<PetBall>();
            ball.SnapToRest(Vector3.zero);
            return ball;
        }

        [Test]
        public void Fetch_NeedsALooseBall()
        {
            var fetch = PetBehaviorLibrary.Get("fetch");
            Assert.IsNotNull(fetch, "the fetch row must exist for a thrown ball to be chased");
            Assert.IsTrue(fetch.Fetch, "fetch must be flagged so the controller runs the retrieve loop");

            var resting = Context(ballLoose: false, targets: InteractableKind.Ball);
            var loose = Context(ballLoose: true, targets: InteractableKind.Ball);

            Assert.AreEqual(0f, fetch.Score(resting),
                "a ball parked in its corner is not worth crossing the room for");
            Assert.Greater(fetch.Score(loose), 0f, "a loose ball should be chased");
        }

        [Test]
        public void Fetch_OutranksBoredomAndGroomingWhileTheBallIsLoose()
        {
            var pool = new List<PetBehavior>(PetBehaviorLibrary.All);
            // Hungry for play, a bit grubby, a bit short on attention — the ordinary state in
            // which several activities compete and the ball should win.
            var context = Context(0.75f, 0.75f, 0.40f, 0.40f, 0.45f, 12f, true,
                new[]
                {
                    InteractableKind.Food, InteractableKind.Water, InteractableKind.Ball,
                    InteractableKind.Bed, InteractableKind.Brush
                });

            var chosen = new PetBehaviorScheduler(11).PickBestProactive(context, pool);

            Assert.IsNotNull(chosen);
            Assert.AreEqual("fetch", chosen.Id,
                "a thrown ball should beat boredom, grooming and a bid for attention");
        }

        [Test]
        public void Fetch_YieldsToCollapseAndStarvation()
        {
            var pool = new List<PetBehavior>(PetBehaviorLibrary.All);
            // Deliberate exception: a ball in the air is not worth more than the pet's own
            // survival, so exhaustion and hunger still outrank the retrieve.
            var context = Context(0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 12f, true,
                new[]
                {
                    InteractableKind.Food, InteractableKind.Water, InteractableKind.Ball,
                    InteractableKind.Bed, InteractableKind.Brush
                });

            var chosen = new PetBehaviorScheduler(11).PickBestProactive(context, pool);

            Assert.IsNotNull(chosen);
            Assert.AreEqual("sleep", chosen.Id, "a pet about to drop should sleep, not fetch");
        }

        [Test]
        public void Ball_CannotBeThrownUnlessItIsHeld()
        {
            var ball = NewBall();
            try
            {
                ball.Throw(Vector3.forward);
                Assert.AreEqual(BallState.Resting, ball.State, "a resting ball must not launch itself");
                Assert.IsFalse(ball.IsLoose,
                    "a ball nobody has thrown is not a fetch target, or the pet loops forever");
            }
            finally { UnityEngine.Object.DestroyImmediate(ball.gameObject); }
        }

        [Test]
        public void Ball_ThrowFliesForwardAndLandsOnTheFloor()
        {
            var ball = NewBall();
            try
            {
                ball.PickUp(ball.transform);
                Assert.AreEqual(BallState.Held, ball.State);

                ball.BeginCharge(10f); // long enough to saturate
                Assert.AreEqual(1f, ball.Charge, 0.001f, "charge must clamp to 1");

                ball.Throw(Vector3.forward);
                Assert.AreEqual(BallState.Flying, ball.State);
                Assert.IsTrue(ball.IsLoose, "a thrown ball must count as loose, or nobody fetches it");

                Vector3 landing = ball.PredictLanding();
                Assert.AreEqual(0f, landing.y, 0.0001f, "the landing spot must be on the floor");
                Assert.Greater(landing.z, 1f, "a full-power throw should travel forward");
                Assert.Less(Mathf.Abs(landing.x), 0.01f, "a straight throw must not drift sideways");

                ball.Drop(new Vector3(3f, 5f, -2f));
                Assert.AreEqual(BallState.Resting, ball.State);
                Assert.AreEqual(0f, ball.transform.position.y, 0.0001f,
                    "the pivot sits on the floor; the visual sphere rides above it");
            }
            finally { UnityEngine.Object.DestroyImmediate(ball.gameObject); }
        }

        [Test]
        public void Ball_ChargeIsSpentOnceThrown()
        {
            var ball = NewBall();
            try
            {
                ball.PickUp(ball.transform);
                ball.BeginCharge(0.55f);
                Assert.Greater(ball.Charge, 0f);

                ball.Throw(Vector3.right);
                Assert.AreEqual(0f, ball.Charge, "the charge meter must reset, or the next throw is free");
            }
            finally { UnityEngine.Object.DestroyImmediate(ball.gameObject); }
        }

        [Test]
        public void Ball_CarriedBallReturnsToRestWhenDropped()
        {
            var go = new GameObject("Mouth");
            var ball = NewBall();
            try
            {
                ball.PickUp(ball.transform);
                ball.Carry(go.transform);
                Assert.AreEqual(BallState.Carried, ball.State);
                Assert.IsFalse(ball.IsAtRest, "a carried ball is not at rest");
                Assert.IsFalse(ball.IsLoose,
                    "the pet already has the ball, so a second fetch must not start");

                ball.Drop(new Vector3(1f, 0f, 1f));
                Assert.AreEqual(BallState.Resting, ball.State);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ball.gameObject);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
