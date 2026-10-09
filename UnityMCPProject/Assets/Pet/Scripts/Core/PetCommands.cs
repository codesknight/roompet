using UnityEngine;

namespace DshPet
{
    /// <summary>What the owner just asked the pet to do.</summary>
    public enum PetOrderKind
    {
        None = 0,
        Come,
        Stay,
        Follow,
        Eat,
        Drink,
        Play,
        Fetch,
        Sleep,
        Toilet,
        Bath,
        Groom,
        AskWhere
    }

    /// <summary>One understood order.</summary>
    public struct PetOrder
    {
        public PetOrderKind Kind;

        /// <summary>Which object the order is about, when it names one.</summary>
        public InteractableKind? Target;

        /// <summary>Whether the subject was the owner rather than an object (「你在哪」).</summary>
        public bool AboutOwner;

        public string Raw;

        public bool IsOrder => Kind != PetOrderKind.None;

        public static PetOrder Nothing(string raw = null)
            => new PetOrder { Kind = PetOrderKind.None, Raw = raw };
    }

    /// <summary>
    /// Orders: 拿球, 去吃饭, 过来, 别动, 饭碗在哪.
    ///
    /// This is the half of the spatial-awareness feature the player asked for as "听得懂主人指令（拿球、
    /// 吃饭、喝水等），因为这些动作都通过空间感知完成" — and the reason it is a parser rather than a
    /// prompt instruction is the one thing the project keeps re-learning about the network brain: a
    /// model that is asked to both chat *and* reliably trigger game actions will, on some turn,
    /// decide that "好的，我去吃饭" is a lovely answer without going anywhere. An order recognised
    /// here is honoured in the same frame, on the device, for zero tokens.
    ///
    /// It is deliberately conservative, and the conservatism is the design: a false positive turns
    /// ordinary conversation ("我今天吃了饭") into the pet walking off to its bowl, which is a much
    /// worse failure than a missed order — a missed order just gets answered by the model, which is
    /// what used to happen to all of them.
    ///
    /// Three guards, in order: a question is a question ("你吃饭了吗"), a bare statement is a
    /// statement ("我吃过饭了"), and a long sentence is conversation, not a command. What is left is
    /// short, imperative, and about something the room actually has.
    /// </summary>
    public static class PetCommands
    {
        /// <summary>Longer than this and it is a sentence, not an order.</summary>
        public const int MaxOrderLength = 12;

        /// <summary>An order may be short without a marker word: 「吃饭」, 「过来」.</summary>
        public const int ShortOrderLength = 5;

        /// <summary>One row of vocabulary: a kind, and the words that mean it.</summary>
        private struct Word
        {
            public PetOrderKind Kind;
            public string[] Phrases;
            public InteractableKind? Target;
            public bool AboutOwner;
        }

        // Order matters: the first row whose phrase appears wins, so the specific rows
        // (拿球/捡球) sit above the general ones (玩球), and "去哪" style questions are
        // recognised before anything else.
        private static readonly Word[] Vocabulary =
        {
            new Word { Kind = PetOrderKind.Fetch,
                Phrases = new[] { "拿球", "捡球", "把球拿", "球拿", "捡回来", "取球", "叼球", "把球给" },
                Target = InteractableKind.Ball },

            new Word { Kind = PetOrderKind.Come,
                Phrases = new[] { "过来", "来我这", "来我这儿", "来这边", "来找我", "到我这儿", "来一下", "快来", "过来吧" } },

            new Word { Kind = PetOrderKind.Follow,
                Phrases = new[] { "跟着我", "跟我走", "跟我来", "跟着", "跟我" } },

            new Word { Kind = PetOrderKind.Stay,
                Phrases = new[] { "别动", "不要动", "不许动", "站住", "别走", "待着", "呆着", "等我", "停下" } },

            new Word { Kind = PetOrderKind.Toilet,
                Phrases = new[] { "上厕所", "去厕所", "猫砂盆", "砂盆", "去方便" },
                Target = InteractableKind.Toilet },

            new Word { Kind = PetOrderKind.Bath,
                Phrases = new[] { "洗澡", "洗个澡", "去洗" },
                Target = InteractableKind.Bath },

            new Word { Kind = PetOrderKind.Groom,
                Phrases = new[] { "梳毛", "刷毛", "梳梳", "理毛", "梳一下" },
                Target = InteractableKind.Brush },

            new Word { Kind = PetOrderKind.Eat,
                Phrases = new[] { "吃饭", "吃东西", "吃点东西", "开饭", "去吃饭" },
                Target = InteractableKind.Food },

            new Word { Kind = PetOrderKind.Drink,
                Phrases = new[] { "喝水", "喝点水", "去喝水", "喝一口" },
                Target = InteractableKind.Water },

            new Word { Kind = PetOrderKind.Sleep,
                Phrases = new[] { "睡觉", "去睡", "睡吧", "午睡", "休息一下" },
                Target = InteractableKind.Bed },

            new Word { Kind = PetOrderKind.Play,
                Phrases = new[] { "玩球", "玩玩具", "陪我玩", "一起玩", "去玩", "玩一下", "玩耍" },
                Target = InteractableKind.Ball }
        };

        /// <summary>The words that mean "where is X".</summary>
        private static readonly string[] WherePhrases =
        {
            "在哪", "在哪儿", "在哪里", "哪个位置", "什么地方", "放哪"
        };

        /// <summary>
        /// Words that turn a phrase into an order when they <b>open</b> it: 「去吃饭」 asks, 「我吃过饭
        /// 了」 reports, and only the leading word tells them apart.
        /// </summary>
        private static readonly string[] ImperativeMarkers =
        {
            "去", "快", "给我", "帮我", "把", "马上", "赶紧", "现在", "一起", "来", "请", "现在"
        };

        /// <summary>Endings that soften a phrase into a request: 「吃饭吧」.</summary>
        private static readonly string[] AskingSuffixes = { "吧", "一下", "好不好", "呗", "啊" };

        /// <summary>
        /// Openers that make a sentence a remark about the player rather than an order to the pet.
        ///
        /// 「我吃饭了」 and 「吃饭」 contain the same two characters, and only the subject tells them
        /// apart — so the subject is what gets checked.
        /// </summary>
        private static readonly string[] StatementOpeners =
        {
            "我", "你", "他", "她", "它", "咱", "今天", "昨天", "明天", "刚刚", "刚才", "我们", "你们"
        };

        /// <summary>Words that mark a question rather than an order.</summary>
        private static readonly string[] QuestionWords =
        {
            "吗", "呢", "什么", "为什么", "怎么", "多少", "几点", "谁", "是不是", "能不能"
        };

        /// <summary>The objects the pet can be asked about, and the words that name them.</summary>
        private static readonly Word[] Subjects =
        {
            new Word { Target = InteractableKind.Food, Phrases = new[] { "饭碗", "饭盆", "食盆", "吃的", "食物", "饭" } },
            new Word { Target = InteractableKind.Water, Phrases = new[] { "水碗", "水盆", "水" } },
            new Word { Target = InteractableKind.Toilet, Phrases = new[] { "猫砂盆", "厕所", "砂盆" } },
            new Word { Target = InteractableKind.Bed, Phrases = new[] { "小床", "床", "窝" } },
            new Word { Target = InteractableKind.Bath, Phrases = new[] { "澡盆", "浴缸" } },
            new Word { Target = InteractableKind.Brush, Phrases = new[] { "梳子", "刷子" } },
            new Word { Target = InteractableKind.Ball, Phrases = new[] { "球" } },
            new Word { Target = InteractableKind.Toy, Phrases = new[] { "玩具" } }
        };

        /// <summary>Words that mean the owner rather than an object.</summary>
        private static readonly string[] OwnerWords = { "主人", "你", "人" };

        /// <summary>
        /// Reads one message as an order, or as nothing at all.
        ///
        /// <see cref="PetOrder.Nothing"/> is the common answer and the safe one: the caller then
        /// treats the message as conversation, exactly as it did before this class existed.
        /// </summary>
        public static PetOrder Parse(string text)
        {
            string clean = Normalize(text);
            if (clean.Length == 0) return PetOrder.Nothing(text);

            // A "where is it" question is checked first: 饭碗在哪 contains no action word, but it is
            // the one question this class *does* answer, from the pet's own perception.
            string where = FindPhrase(clean, WherePhrases);
            if (where != null)
            {
                var asked = ResolveSubject(clean);
                return new PetOrder
                {
                    Kind = PetOrderKind.AskWhere,
                    Target = asked.Target,
                    AboutOwner = asked.Target == null && ContainsAny(clean, OwnerWords),
                    Raw = text
                };
            }

            if (clean.Length > MaxOrderLength) return PetOrder.Nothing(text);
            if (ContainsAny(clean, QuestionWords)) return PetOrder.Nothing(text);
            if (IsStatement(clean)) return PetOrder.Nothing(text);

            for (int i = 0; i < Vocabulary.Length; i++)
            {
                var row = Vocabulary[i];
                if (FindPhrase(clean, row.Phrases) == null) continue;

                // A phrase that merely mentions an action is only an order when the sentence is
                // short or is clearly addressed to the pet: 「吃饭」 and 「快去吃饭」 are orders,
                // 「球」 is not.
                bool marked = StartsWithAny(clean, ImperativeMarkers) || ContainsAny(clean, AskingSuffixes);
                if (!marked && clean.Length > ShortOrderLength) continue;

                return new PetOrder { Kind = row.Kind, Target = row.Target, Raw = text };
            }

            return PetOrder.Nothing(text);
        }

        /// <summary>Which object a sentence names, if any.</summary>
        public static InteractableKind? TargetOf(string text)
            => ResolveSubject(Normalize(text)).Target;

        private static Word ResolveSubject(string clean)
        {
            for (int i = 0; i < Subjects.Length; i++)
            {
                if (FindPhrase(clean, Subjects[i].Phrases) != null) return Subjects[i];
            }
            return default;
        }

        private static bool IsStatement(string clean)
        {
            // An order can open with a marker word; nothing else can rescue a sentence from being a
            // report about the player.
            if (StartsWithAny(clean, ImperativeMarkers)) return false;

            // Past tense or experience: 「我吃过饭了」 is a report, not a request. 过 is only a tense
            // marker when it is not part of 过来/过去 — the first version of this treated 「过来」 as a
            // past-tense sentence and quietly stopped understanding the single most common order
            // there is. (Caught by the test, which is the only reason it is not in the build.)
            bool past = clean.Contains("了")
                || (clean.Contains("过") && !clean.Contains("过来") && !clean.Contains("过去"));

            if (past) return true;

            for (int i = 0; i < StatementOpeners.Length; i++)
            {
                if (clean.StartsWith(StatementOpeners[i])) return true;
            }

            return false;
        }

        /// <summary>Strips punctuation and spaces so the phrase table does not need them.</summary>
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;
                if (char.IsPunctuation(c) || char.IsSymbol(c)) continue;   // ，。！？… etc.
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static string FindPhrase(string clean, string[] phrases)
        {
            for (int i = 0; i < phrases.Length; i++)
            {
                if (clean.Contains(phrases[i])) return phrases[i];
            }
            return null;
        }

        private static bool ContainsAny(string clean, string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (clean.Contains(words[i])) return true;
            }
            return false;
        }

        private static bool StartsWithAny(string clean, string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (clean.StartsWith(words[i])) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the answers

        /// <summary>
        /// What the pet says when an order lands, before it does anything.
        ///
        /// Local lines, on purpose: they have to be immediate (the pet is already walking) and they
        /// must never be the thing that fails. The personality still colours them a little through
        /// <see cref="Flavour"/>, so the same order does not produce the identical sentence forever.
        /// </summary>
        public static string Reply(PetOrder order, int flavour = 0)
        {
            int pick = Mathf.Abs(flavour);

            switch (order.Kind)
            {
                case PetOrderKind.Come:
                    return Pick(pick, "来啦来啦！", "我在这儿呢！", "（小跑着过来）来了——");
                case PetOrderKind.Stay:
                    return Pick(pick, "好，我就在这儿不动。", "（乖乖坐好）不动。", "嗯，我等你。");
                case PetOrderKind.Follow:
                    return Pick(pick, "好，我跟着你。", "（跟在你脚边）走吧走吧。", "我踩着你脚印走。");
                case PetOrderKind.Eat:
                    return Pick(pick, "开饭啦！", "（尾巴先跑过去了）", "我这就去吃！");
                case PetOrderKind.Drink:
                    return Pick(pick, "正好渴了，我去喝水。", "（嗒嗒嗒地喝水）", "水！我去了。");
                case PetOrderKind.Play:
                    return Pick(pick, "玩起来！", "球球球球！", "（已经扑过去了）");
                case PetOrderKind.Fetch:
                    return Pick(pick, "球球！我这就去捡！", "（箭一样冲出去）等等我——", "看我的！");
                case PetOrderKind.Sleep:
                    return Pick(pick, "那我先去睡一会儿……", "（打了个哈欠）好困。", "窝里见。");
                case PetOrderKind.Toilet:
                    return Pick(pick, "我先去一下猫砂盆。", "（原地转了两圈）等一下下。", "马上回来！");
                case PetOrderKind.Bath:
                    return Pick(pick, "唔……好吧，洗澡就洗澡。", "（抖了抖毛）我不太想洗……", "轻一点哦。");
                case PetOrderKind.Groom:
                    return Pick(pick, "舒服～再梳两下。", "（眯起眼睛）就这儿。", "毛毛顺顺的！");
                default:
                    return "";
            }
        }

        /// <summary>What the pet plays while doing it, so the answer is visible as well as audible.</summary>
        public static PetAction Action(PetOrder order)
        {
            switch (order.Kind)
            {
                case PetOrderKind.Come: return PetAction.Jump;
                case PetOrderKind.Stay: return PetAction.Sit;
                case PetOrderKind.Follow: return PetAction.Wag;
                case PetOrderKind.Eat: return PetAction.Eat;
                case PetOrderKind.Drink: return PetAction.Drink;
                case PetOrderKind.Play:
                case PetOrderKind.Fetch: return PetAction.Play;
                case PetOrderKind.Sleep: return PetAction.Sleep;
                case PetOrderKind.Toilet: return PetAction.Sit;
                case PetOrderKind.Bath: return PetAction.Jump;
                case PetOrderKind.Groom: return PetAction.Wag;
                default: return PetAction.Curious;
            }
        }

        /// <summary>
        /// What the pet says when it cannot do as it was asked — usually because the room has nothing
        /// of that kind in it.
        ///
        /// Honest rather than silent: an order that produces no answer and no movement is the same
        /// experience as a broken microphone, and this project has already been round that loop once.
        /// </summary>
        public static string CannotDo(PetOrder order)
        {
            switch (order.Kind)
            {
                case PetOrderKind.Eat: return "唔……碗里好像没有东西，你帮我放点吃的？";
                case PetOrderKind.Drink: return "水碗是空的……你给我倒点水吧。";
                case PetOrderKind.Play: return "球好像不在，我先自己转两圈。";
                case PetOrderKind.Fetch: return "球在哪呀？我没看到。";
                case PetOrderKind.Toilet: return "猫砂盆不在……我尽量忍住。";
                case PetOrderKind.Bath: return "这里没有澡盆呀。";
                case PetOrderKind.Groom: return "梳子不在手边呢。";
                case PetOrderKind.Come:
                case PetOrderKind.Follow: return "找不到你的位置，你走近一点？";
                default: return "唔……这个我好像做不了。";
            }
        }

        private static string Pick(int seed, params string[] options)
            => options.Length == 0 ? "" : options[seed % options.Length];
    }
}
