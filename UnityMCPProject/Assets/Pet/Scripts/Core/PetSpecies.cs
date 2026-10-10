using System;
using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// One swappable pet. Everything that makes the animals feel different lives here:
    /// the colours and body proportions the avatar builds from, and — more importantly —
    /// the personality and voice lines that steer the language model.
    /// </summary>
    [Serializable]
    public class PetSpecies
    {
        public string Id = "fox";
        public string DisplayName = "小狐狸";
        public string Blurb = "";

        // ---- look ----
        public Color Fur = new Color(0.93f, 0.45f, 0.22f);
        public Color Belly = new Color(0.97f, 0.87f, 0.74f);
        public Color Accent = new Color(0.26f, 0.16f, 0.14f);
        public float BodyScale = 1f;
        public float BodyLength = 0.34f;
        public float HeadScale = 0.30f;
        public EarStyle Ears = EarStyle.Pointy;
        public TailStyle Tail = TailStyle.Bushy;
        public float EarLength = 0.17f;
        public float TailLength = 0.30f;

        /// <summary>
        /// The imported Cube Pets model for this species, under `Kenney/Pets/`. Empty keeps the
        /// old procedural body, so the swap can be done one species at a time and rolled back.
        /// </summary>
        public string ModelName = "";

        // ---- character (drives the prompt) ----
        /// <summary>Injected into the system prompt as "your character".</summary>
        public string Personality = "";
        /// <summary>How the pet talks, also injected into the prompt.</summary>
        public string VoiceStyle = "";
        public string FavoriteFood = "苹果";

        /// <summary>
        /// Shop price in pet coins. The starting species is free; the rest have to be earned.
        ///
        /// The first animal is free on purpose: a pet game that opens with a paywall has no
        /// first five minutes. Everything after that is a goal, and the runner already pays
        /// coins for distance, so the two halves of the game feed each other.
        /// </summary>
        public int Price;

        /// <summary>True when a fresh save already owns this species.</summary>
        public bool Starter;

        /// <summary>
        /// True for the gacha-only species: not sold directly, only won from the 扭蛋机.
        /// </summary>
        public bool GachaOnly;

        /// <summary>
        /// A private copy, for storing in a serialized field.
        ///
        /// Never assign an entry of <see cref="All"/> directly to a MonoBehaviour's field:
        /// Unity deserializes a non-null reference-type field IN PLACE, so the next scene
        /// load would overwrite the shared static entry with whatever the scene saved —
        /// which silently corrupts the species table for the whole session (two rabbits in
        /// the switcher and no fox, is how this shows up).
        /// </summary>
        public PetSpecies Copy()
        {
            var clone = (PetSpecies)MemberwiseClone();
            clone.Id = Id;
            clone.DisplayName = DisplayName;
            return clone;
        }

        /// <summary>
        /// Every species the player can swap to, in the order the switcher shows them.
        ///
        /// Two positional invariants, both load-bearing: <c>All[0]</c> is the default for a
        /// fresh save, and a saved id is looked up by <see cref="Get(string)"/> rather than by
        /// index so reordering this table cannot resurrect the wrong animal. Do not reorder
        /// without checking the switcher's index mapping as well.
        /// </summary>
        public static readonly PetSpecies[] All =
        {
            new PetSpecies
            {
                Id = "fox", DisplayName = "小狐狸", Blurb = "机灵、好奇、有点小得意",
                Fur = new Color(0.93f, 0.45f, 0.22f),
                Belly = new Color(0.97f, 0.87f, 0.74f),
                Accent = new Color(0.26f, 0.16f, 0.14f),
                BodyScale = 1f, BodyLength = 0.34f, HeadScale = 0.30f,
                Ears = EarStyle.Pointy, Tail = TailStyle.Bushy,
                EarLength = 0.17f, TailLength = 0.32f, ModelName = "animal-fox",
                FavoriteFood = "苹果", Price = 0, Starter = true,
                Personality = "机灵、好奇心重、喜欢探索新东西，偶尔有点小得意和逞强，但很在意主人。",
                VoiceStyle = "说话轻快、句子短，爱用「诶？」「你看！」这类语气，偶尔自夸一句。"
            },
            new PetSpecies
            {
                Id = "cat", DisplayName = "小猫咪", Blurb = "高冷傲娇，其实很黏人",
                Fur = new Color(0.58f, 0.58f, 0.66f),
                Belly = new Color(0.95f, 0.93f, 0.90f),
                Accent = new Color(0.95f, 0.62f, 0.66f),
                BodyScale = 0.94f, BodyLength = 0.32f, HeadScale = 0.29f,
                Ears = EarStyle.Pointy, Tail = TailStyle.Curly,
                EarLength = 0.14f, TailLength = 0.34f, ModelName = "animal-cat",
                FavoriteFood = "小鱼干", Price = 260,
                Personality = "表面高冷、爱答不理，其实很黏人。被摸的时候会假装不情愿，但尾巴会出卖它。",
                VoiceStyle = "说话很短，常带「哼」「随便」「才不是」这类傲娇口吻，很少连续说三句以上。"
            },
            new PetSpecies
            {
                Id = "rabbit", DisplayName = "小兔子", Blurb = "温柔胆小，容易紧张",
                Fur = new Color(0.93f, 0.91f, 0.89f),
                Belly = new Color(0.99f, 0.96f, 0.94f),
                Accent = new Color(0.95f, 0.66f, 0.70f),
                BodyScale = 0.86f, BodyLength = 0.28f, HeadScale = 0.27f,
                Ears = EarStyle.Long, Tail = TailStyle.Puff,
                EarLength = 0.26f, TailLength = 0.12f, ModelName = "animal-bunny",
                FavoriteFood = "胡萝卜", Price = 420,
                Personality = "温柔、胆小，容易被突然的动静吓到，需要慢慢哄。熟悉之后会非常依赖你。",
                VoiceStyle = "说话软软的、句子短，常用「嗯…」「那个…」，紧张时会重复你的话。"
            },
            new PetSpecies
            {
                Id = "bear", DisplayName = "小熊", Blurb = "憨厚贪吃，反应慢半拍",
                Fur = new Color(0.56f, 0.37f, 0.23f),
                Belly = new Color(0.85f, 0.72f, 0.55f),
                Accent = new Color(0.24f, 0.17f, 0.13f),
                BodyScale = 1.18f, BodyLength = 0.38f, HeadScale = 0.34f,
                Ears = EarStyle.Round, Tail = TailStyle.Short,
                EarLength = 0.09f, TailLength = 0.10f, ModelName = "animal-polar",
                FavoriteFood = "蜂蜜", Price = 680,
                Personality = "憨厚、慢性子、非常贪吃。思考事情要慢半拍，但脾气特别好，被欺负也不生气。",
                VoiceStyle = "说话慢、爱用「唔…」「那个…那个…」，三句里有两句会提到吃的。"
            },

            // ---------------------------------------------------------------- 第 27 轮新增
            // 扭蛋机的奖池：这些是抽出来的稀有宠物，不在普通商城直售（商城只有 fox/cat/rabbit/bear）。
            new PetSpecies
            {
                Id = "red_panda", DisplayName = "小熊猫", Blurb = "圆滚滚的，尾巴像条大围巾",
                Fur = new Color(0.82f, 0.33f, 0.16f),
                Belly = new Color(0.24f, 0.16f, 0.12f),
                Accent = new Color(0.94f, 0.86f, 0.72f),
                BodyScale = 0.95f, BodyLength = 0.34f, HeadScale = 0.31f,
                Ears = EarStyle.Round, Tail = TailStyle.Bushy,
                EarLength = 0.12f, TailLength = 0.40f, ModelName = "animal-panda",
                FavoriteFood = "苹果", Price = 560, GachaOnly = true,
                Personality = "慢吞吞、喜欢晒太阳，毛茸茸的尾巴是它的宝贝。有点迷糊，但特别温柔。",
                VoiceStyle = "说话慢悠悠、句子短，爱用「呼啊～」「软软的」这类词，喜欢描述舒服的感觉。"
            },
            new PetSpecies
            {
                Id = "penguin", DisplayName = "小企鹅", Blurb = "摇摇摆摆，一本正经",
                Fur = new Color(0.18f, 0.20f, 0.26f),
                Belly = new Color(0.94f, 0.95f, 0.97f),
                Accent = new Color(0.95f, 0.72f, 0.22f),
                BodyScale = 0.96f, BodyLength = 0.30f, HeadScale = 0.28f,
                Ears = EarStyle.Small, Tail = TailStyle.Short,
                EarLength = 0.06f, TailLength = 0.10f, ModelName = "animal-penguin",
                FavoriteFood = "鱼", Price = 640, GachaOnly = true,
                Personality = "一本正经、爱整洁，走路摇摇摆摆但很守规矩。高兴了会扑扇翅膀（其实是鳍）。",
                VoiceStyle = "说话正式、句子完整，爱用「在下」「这便」这类古早口吻，偶尔冒出「啪嗒啪嗒」。"
            },
            new PetSpecies
            {
                Id = "hamster", DisplayName = "小仓鼠", Blurb = "腮帮子鼓鼓的，囤粮冠军",
                Fur = new Color(0.88f, 0.72f, 0.42f),
                Belly = new Color(0.99f, 0.95f, 0.86f),
                Accent = new Color(0.62f, 0.42f, 0.24f),
                BodyScale = 0.60f, BodyLength = 0.22f, HeadScale = 0.24f,
                Ears = EarStyle.Round, Tail = TailStyle.Puff,
                EarLength = 0.08f, TailLength = 0.06f, ModelName = "animal-hog",
                FavoriteFood = "宠物粮", Price = 520, GachaOnly = true,
                Personality = "个子小、精力旺盛，最爱把食物塞进腮帮子。有点神经质，但特别会哄自己开心。",
                VoiceStyle = "说话又快又碎，爱用「咕」「啾」这类拟声词，一句话常拆成好几段。"
            }
        };

        public static int Count => All.Length;

        /// <summary>
        /// A fresh copy of a species, safe to hang off a serialized field. Use this rather
        /// than <see cref="Get(string)"/> anywhere the result is stored — see
        /// <see cref="Copy"/> for why the distinction matters.
        /// </summary>
        public static PetSpecies Copy(string id) => Get(id).Copy();

        public static PetSpecies Get(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase)) return All[i];
            }
            return All[0];
        }

        public static PetSpecies Get(int index) => All[Mathf.Clamp(index, 0, All.Length - 1)];

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return 0;
        }

        public static IEnumerable<string> Ids()
        {
            for (int i = 0; i < All.Length; i++) yield return All[i].Id;
        }
    }
}
