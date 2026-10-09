using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The pet's behaviour table. Proactive rows are need- or environment-driven; passive
    /// rows are the ambient quirks that fire on a random timer so the pet never looks
    /// scripted.
    ///
    /// This is the extension point for the "virtual town" direction: a new activity is a
    /// new row here plus (optionally) a new <see cref="InteractableKind"/>.
    /// </summary>
    public static class PetBehaviorLibrary
    {
        private static PetBehavior[] _all;

        public static PetBehavior[] All => _all ?? (_all = Build());

        private static PetBehavior[] Build()
        {
            var list = new List<PetBehavior>
            {
                // ------------------------------------------------------- 主动 / proactive

                new PetBehavior
                {
                    Id = "eat", Label = "去吃饭", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Eat, Duration = 2.6f, Cooldown = 45f, Weight = 1.4f,
                    RequiresNeed = "Hunger", NeedBelow = 0.40f, TargetKind = "Food",
                    // The bowl is always there; the *food* in it is a shop item. No pantry, no
                    // trip to the bowl — which is how an empty bowl becomes the shop's job.
                    NeedsFood = true,
                    Lines = new[] { "肚子在叫了……", "我去看看碗里还有没有。" }
                },
                new PetBehavior
                {
                    Id = "drink", Label = "去喝水", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Drink, Duration = 2.2f, Cooldown = 60f, Weight = 0.7f,
                    RequiresNeed = "Hunger", NeedBelow = 0.65f, TargetKind = "Water",
                    Lines = new[] { "有点渴。", "喝口水。" }
                },
                new PetBehavior
                {
                    Id = "sleep", Label = "去睡觉", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Sleep, Duration = 8f, Cooldown = 90f, Weight = 1.6f,
                    RequiresNeed = "Energy", NeedBelow = 0.32f, TargetKind = "Bed",
                    Lines = new[] { "眼皮好重……我趴一会儿。", "我先睡一下下。" }
                },
                new PetBehavior
                {
                    Id = "nap_night", Label = "夜里犯困", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Sleep, Duration = 6f, Cooldown = 120f, Weight = 1.1f,
                    RequiresNeed = "Energy", NeedBelow = 0.60f, TargetKind = "Bed",
                    MinHour = 22f, MaxHour = 6f,
                    Lines = new[] { "已经这么晚了呀……" }
                },
                new PetBehavior
                {
                    Id = "play", Label = "去玩球", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Play, Duration = 2.8f, Cooldown = 50f, Weight = 1.1f,
                    RequiresNeed = "Joy", NeedBelow = 0.45f, TargetKind = "Ball",
                    Lines = new[] { "好无聊啊——", "球！我看到球了！" }
                },
                new PetBehavior
                {
                    // Fires only while the ball is loose, and outranks everything else then:
                    // a thrown ball is the one thing a pet will not ignore.
                    Id = "fetch", Label = "去捡球", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Play, Duration = 4f, Cooldown = 90f, Weight = 1.9f,
                    TargetKind = "Ball", Fetch = true,
                    Lines = new[] { "球！我去捡！", "（耳朵一竖就冲了出去）", "等等我——" }
                },
                new PetBehavior
                {
                    Id = "groom", Label = "去梳毛", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Wag, Duration = 2.6f, Cooldown = 90f, Weight = 1.0f,
                    RequiresNeed = "Cleanliness", NeedBelow = 0.45f, TargetKind = "Brush",
                    Lines = new[] { "我身上有点乱……", "毛打结了。" }
                },
                new PetBehavior
                {
                    // A real bath, as opposed to a quick groom: only when genuinely filthy,
                    // and heavy enough that it beats wandering off to play.
                    Id = "bathe", Label = "去洗澡", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Play, Duration = 4.2f, Cooldown = 200f, Weight = 1.5f,
                    RequiresNeed = "Cleanliness", NeedBelow = 0.28f, TargetKind = "Bath",
                    Lines = new[] { "我身上都是灰……我要洗澡！", "（抖了抖毛，扬起一小片灰）" }
                },
                new PetBehavior
                {
                    Id = "use_toilet", Label = "去上厕所", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Sit, Duration = 3.0f, Cooldown = 30f, Weight = 2.2f,
                    RequiresNeed = "Bladder", NeedBelow = 0.35f, TargetKind = "Toilet",
                    Lines = new[] { "（原地转了两圈）……等一下。", "我先去一下猫砂盆。" }
                },
                new PetBehavior
                {
                    // The pet's own tidy-up after an accident, and only after one: `NeedsMess`
                    // gates it, so it never fires just because a mess object exists.
                    Id = "hide_mess", Label = "心虚地扒拉", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Sad, Duration = 2.8f, Cooldown = 120f, Weight = 1.2f,
                    NeedsMess = true, CleansMess = true,
                    Lines = new[] { "（用爪子扒拉着地面，假装什么都没发生）", "……不是我干的。" }
                },
                new PetBehavior
                {
                    Id = "seek_attention", Label = "来找你", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Sit, Duration = 3.5f, Cooldown = 45f, Weight = 1.3f,
                    RequiresNeed = "Affection", NeedBelow = 0.5f, TargetKind = "Player",
                    Lines = new[] { "（走到你旁边坐下）", "（用鼻子碰了碰你的脚）", "陪我一会儿嘛。" }
                },
                new PetBehavior
                {
                    Id = "bask", Label = "晒会儿太阳", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Happy, Duration = 4f, Cooldown = 140f, Weight = 0.5f,
                    RequiresNeed = "Energy", NeedAbove = 0.55f,
                    MinHour = 9f, MaxHour = 17f,
                    Lines = new[] { "（摊在地毯上晒太阳，眯起了眼睛）" }
                },

                // ------------------------------------------------------- 花园里才有的两件事
                //
                // 「在花园心情好会上窜下跳、玩捉迷藏」 is a request about what a *place* does to a pet,
                // and it is the whole reason the map is worth unlocking: the same animal in the
                // same mood behaves differently in a garden than in a room. Both rows are gated by
                // OnlyInPlace, so the cabin and the terrace keep the behaviour they had.
                new PetBehavior
                {
                    Id = "garden_zoomies", Label = "在草地上上窜下跳", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Jump, Duration = 4.2f, Cooldown = 55f, Weight = 1.9f,
                    RequiresNeed = "Joy", NeedAbove = 0.58f,
                    OnlyInPlace = "Garden", Hop = true,
                    Lines = new[]
                    {
                        "（在草地上蹦得老高，尾巴甩成了螺旋）",
                        "（绕着花坛跑了一圈，又蹦回来）",
                        "好开心！你也来蹦一下嘛！"
                    }
                },
                new PetBehavior
                {
                    Id = "hide_and_seek", Label = "躲起来让你找", Drive = BehaviorDrive.Proactive,
                    Action = PetAction.Sit, Duration = 4.6f, Cooldown = 90f, Weight = 1.5f,
                    RequiresNeed = "Joy", NeedAbove = 0.45f,
                    OnlyInPlace = "Garden", TargetKind = "HidingSpot",
                    Lines = new[]
                    {
                        "（一头钻进灌木后面，只露出半只耳朵）",
                        "我躲好了，你快来找我！",
                        "（屏住呼吸，尾尖还在晃）"
                    }
                },

                // ------------------------------------------------------- 被动 / ambient quirks

                new PetBehavior
                {
                    Id = "stretch", Label = "伸懒腰", Drive = BehaviorDrive.Passive,
                    Action = PetAction.Happy, Duration = 2.2f, Cooldown = 25f, Weight = 1.2f,
                    Lines = new[] { "（伸了个长长的懒腰）" }
                },
                new PetBehavior
                {
                    Id = "look_around", Label = "四处张望", Drive = BehaviorDrive.Passive,
                    Action = PetAction.Curious, Duration = 2.4f, Cooldown = 18f, Weight = 1.4f
                },
                new PetBehavior
                {
                    Id = "wag", Label = "摇尾巴", Drive = BehaviorDrive.Passive,
                    Action = PetAction.Wag, Duration = 1.8f, Cooldown = 20f, Weight = 1.1f
                },
                new PetBehavior
                {
                    Id = "hop", Label = "蹦一下", Drive = BehaviorDrive.Passive,
                    Action = PetAction.Jump, Duration = 1.2f, Cooldown = 30f, Weight = 0.8f,
                    Lines = new[] { "（没来由地蹦了一下）" }
                },
                new PetBehavior
                {
                    Id = "sit_stare", Label = "盯着你看", Drive = BehaviorDrive.Passive,
                    Action = PetAction.Sit, Duration = 3.5f, Cooldown = 45f, Weight = 0.9f,
                    Lines = new[] { "（一直安静地看着你）", "……你在忙什么呀？" }
                },
                new PetBehavior
                {
                    Id = "beg", Label = "讨东西", Drive = BehaviorDrive.Passive,
                    Action = PetAction.Beg, Duration = 2.4f, Cooldown = 55f, Weight = 0.7f,
                    Lines = new[] { "（眼巴巴地看着你）", "你手上那个……是给我的吗？" }
                },
                new PetBehavior
                {
                    // Ambient, unlike seek_attention: this one is not asking for anything, it just
                    // wants to be where the owner is. It is what "知道主人的位置" looks like from the
                    // outside — the pet noticing where you are and acting on it without being told.
                    Id = "come_to_owner", Label = "凑到主人身边", Drive = BehaviorDrive.Passive,
                    Action = PetAction.Wag, Duration = 2.6f, Cooldown = 70f, Weight = 0.8f,
                    TargetKind = "Player",
                    Lines = new[] { "（走过来蹭了蹭你的腿）", "你去哪我就去哪。", "（在你脚边趴下）" }
                },
                new PetBehavior
                {
                    Id = "grumble", Label = "闹脾气", Drive = BehaviorDrive.Passive,
                    Action = PetAction.Sad, Duration = 2.6f, Cooldown = 120f, Weight = 0.4f,
                    RequiresNeed = "Joy", NeedBelow = 0.22f,
                    Lines = new[] { "……哼。", "（背过身去）" }
                },
                new PetBehavior
                {
                    Id = "speak_up", Label = "主动找你说话", Drive = BehaviorDrive.Passive,
                    Action = PetAction.Curious, Duration = 2.5f, Cooldown = 150f, Weight = 0.35f,
                    SpeakUp = true
                }
            };

            return list.ToArray();
        }

        public static PetBehavior Get(string id)
        {
            var all = All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Id == id) return all[i];
            }
            return null;
        }

        public static int ProactiveCount
        {
            get
            {
                int count = 0;
                var all = All;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].Drive == BehaviorDrive.Proactive) count++;
                }
                return count;
            }
        }

        public static int PassiveCount
        {
            get
            {
                int count = 0;
                var all = All;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].Drive == BehaviorDrive.Passive) count++;
                }
                return count;
            }
        }
    }
}
