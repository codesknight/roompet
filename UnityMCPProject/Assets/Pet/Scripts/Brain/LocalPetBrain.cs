using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The offline brain. It is what runs before an API key is configured, and it is also
    /// the fallback when a request fails — so the demo is never a black screen waiting on
    /// a credential.
    ///
    /// It matches the player's line against a small set of intents and answers from
    /// per-intent pools, biased by the pet's live needs. Not a language model, but enough
    /// that the loop (talk → pet reacts → needs move) is demonstrable.
    /// </summary>
    public class LocalPetBrain : IPetBrain
    {
        private enum Intent
        {
            Greeting, Food, Water, Play, Sleep, Touch, Name, Affection,
            Praise, Scold, Status, Goodbye, SmallTalk
        }

        private readonly System.Random _rng = new System.Random();
        private readonly string _petName;

        public LocalPetBrain(string petName) => _petName = petName;

        public string Name => "离线 · 本地规则";
        public bool IsNetwork => false;

        public IEnumerator Think(PetContext context, string userMessage, Action<PetReply> onDone)
        {
            // Small delay so the UI shows a "thinking" state exactly like the online path.
            yield return new WaitForSecondsRealtime(0.25f);

            Intent intent = Classify(userMessage);
            string speech = Compose(intent, context, userMessage);
            PetAction action = ActionFor(intent, context);

            onDone?.Invoke(new PetReply
            {
                Speech = speech,
                Action = action,
                MoodWord = PetUtil.MoodLabel(context.Mood),
                AffectionDelta = intent == Intent.Scold ? 0f : 0.02f,
                FromNetwork = false,
                Raw = "[local:" + intent + "]"
            });
        }

        private static Intent Classify(string raw)
        {
            string text = (raw ?? "").ToLowerInvariant();
            if (text.Length == 0) return Intent.SmallTalk;

            if (Has(text, "再见", "拜拜", "走了", "出门", "一会见", "bye")) return Intent.Goodbye;
            if (Has(text, "名字", "叫什么", "你是谁")) return Intent.Name;
            if (Has(text, "心情", "怎么样", "好吗", "状态", "开心吗", "饿不饿", "困不困")) return Intent.Status;
            if (Has(text, "吃", "喂", "饿", "饭", "苹果", "胡萝卜", "零食", "food", "eat")) return Intent.Food;
            if (Has(text, "喝", "水", "渴", "drink", "water")) return Intent.Water;
            if (Has(text, "玩", "球", "陪", "一起", "play", "ball")) return Intent.Play;
            if (Has(text, "睡", "晚安", "困", "休息", "sleep", "nap")) return Intent.Sleep;
            if (Has(text, "摸", "抱", "rua", "亲", "挠", "梳", "touch", "pet")) return Intent.Touch;
            if (Has(text, "喜欢", "爱你", "最好了", "想你", "love")) return Intent.Affection;
            if (Has(text, "可爱", "乖", "好棒", "厉害", "聪明", "真棒", "good", "cute")) return Intent.Praise;
            if (Has(text, "坏", "笨", "讨厌", "烦", "闭嘴", "bad", "stupid")) return Intent.Scold;
            if (Has(text, "你好", "hi", "hello", "在吗", "早", "嗨")) return Intent.Greeting;
            return Intent.SmallTalk;
        }

        private static bool Has(string text, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (text.Contains(needles[i])) return true;
            }
            return false;
        }

        private string Compose(Intent intent, PetContext ctx, string userMessage)
        {
            // Needs override the topic: a starving pet mostly talks about being starving.
            if (ctx.Hunger < 0.18f && intent != Intent.Food && _rng.NextDouble() < 0.55)
            {
                return Pick("肚子……好空啊……", "我闻到你手上有吃的味道了。", "那个……可以先吃饭吗？");
            }
            if (ctx.Energy < 0.18f && intent != Intent.Sleep && _rng.NextDouble() < 0.55)
            {
                return Pick("眼皮好重……我趴一会儿。", "嗯……让我靠一下你。", "我快睡着了……别走开。");
            }

            switch (intent)
            {
                case Intent.Greeting:
                    return Pick(
                        "诶？你回来啦！", "嗨！我一直盯着门口呢。",
                        "你来啦——今天怎么这么晚。", "唔，是你呀。……我才没有等你。");

                case Intent.Food:
                    return ctx.Hunger < 0.5f
                        ? Pick("要要要！", "快给我，我饿了半天了！", "闻起来好香，是我的对不对？")
                        : Pick("嗯……好吧，我勉强再吃一点。", "现在不太饿，但可以陪你吃。", "放着吧，我一会儿吃。");

                case Intent.Water:
                    return Pick("咕嘟咕嘟……好喝。", "水碗空了你知道吗？", "谢谢，正好有点渴。");

                case Intent.Play:
                    return ctx.Energy < 0.35f
                        ? Pick("唔……我今天有点累，轻轻地玩一下可以吗？", "我不想跑，但可以陪你坐着。")
                        : Pick("球球球球球！", "来呀！这次我一定接得住！", "说好了，不许中途走开。");

                case Intent.Sleep:
                    return Pick("那我睡啦……你不许走。", "嗯……晚安。", "把灯关小一点就更好了。");

                case Intent.Touch:
                    return ctx.Affection > 0.5f
                        ? Pick("……再摸一会儿。", "呼噜呼噜——", "这里，耳朵后面，对。")
                        : Pick("唔？", "……可以，但是只能一下。", "你手好凉。");

                case Intent.Name:
                    return $"我是{_petName}呀，你取的名字，忘啦？";

                case Intent.Affection:
                    return Pick($"我也最喜欢你了。", "嗯……我知道。", "那你不许再消失那么久。");

                case Intent.Praise:
                    return ctx.Affection > 0.4f
                        ? Pick("嘿嘿。", "那当然。", "再说一遍，我没听够。")
                        : Pick("……真的吗？", "你是不是有事求我。", "唔，谢谢。");

                case Intent.Scold:
                    return Pick("……", "我错了嘛。", "哼，不理你了。", "那你别管我了。");

                case Intent.Status:
                    return StatusLine(ctx);

                case Intent.Goodbye:
                    return Pick("早点回来。", "我会等你的。", "……路上小心。");

                default:
                    return SmallTalk(ctx, userMessage);
            }
        }

        private string StatusLine(PetContext ctx)
        {
            string need = string.IsNullOrEmpty(ctx.DominantNeed) ? "现在挺好的" : "有点" + ctx.DominantNeed;
            string bond = ctx.Affection > 0.6f ? "我很黏你" : (ctx.Affection > 0.3f ? "我在慢慢喜欢你" : "我们还不算太熟");
            return $"{PetUtil.MoodLabel(ctx.Mood)}，{need}。{bond}。";
        }

        private string SmallTalk(PetContext ctx, string userMessage)
        {
            if (ctx.Joy > 0.7f)
            {
                return Pick("唔？你说什么我都爱听。", "然后呢然后呢？", "我在听，你继续。");
            }
            if (ctx.Joy < 0.3f)
            {
                return Pick("……我今天有点提不起劲。", "你陪我坐会儿好不好。", "嗯。");
            }
            return Pick("唔……", "你说这个我不太懂，但我记住了。", "那你呢？", "我在看你。");
        }

        private PetAction ActionFor(Intent intent, PetContext ctx)
        {
            switch (intent)
            {
                case Intent.Food: return PetAction.Eat;
                case Intent.Water: return PetAction.Drink;
                case Intent.Play: return ctx.Energy < 0.35f ? PetAction.Sit : PetAction.Play;
                case Intent.Sleep: return PetAction.Sleep;
                case Intent.Touch: return PetAction.Wag;
                case Intent.Affection: return PetAction.Happy;
                case Intent.Praise: return PetAction.Jump;
                case Intent.Scold: return PetAction.Sad;
                case Intent.Goodbye: return PetAction.Sit;
                case Intent.Status: return ctx.Mood == PetMood.Hungry ? PetAction.Beg : PetAction.Curious;
                case Intent.Greeting: return PetAction.Wag;
                default: return PetAction.Curious;
            }
        }

        private string Pick(params string[] options)
            => options[_rng.Next(options.Length)];
    }
}
