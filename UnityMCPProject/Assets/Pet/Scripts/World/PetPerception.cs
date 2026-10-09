using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Something in the room, as a fact rather than as a component.
    ///
    /// The point of the separation is that perception becomes arithmetic: a room full of
    /// <see cref="Interactable"/> MonoBehaviours cannot be reasoned about in a unit test without
    /// building a scene, and "does the pet know where its bowl is" is exactly the kind of question
    /// that should be answerable in one.
    /// </summary>
    public struct PetTarget
    {
        /// <summary>What the player calls it: 「饭碗」.</summary>
        public string Name;

        public InteractableKind Kind;

        public Vector3 Position;

        /// <summary>Whether it can be used right now (the interactables have their own cooldown).</summary>
        public bool Ready;
    }

    /// <summary>
    /// What the pet can see: the room's objects and its owner, in the pet's own frame of reference.
    ///
    /// This is the feature the player asked for by name — 增强宠物的位置感知能力 — and the reason it
    /// is a class of pure functions rather than a few lines inside the controller is that it has
    /// three jobs at once:
    ///
    /// <list type="bullet">
    /// <item><b>It writes the prompt block.</b> The model can only talk about where the bowl is if
    /// somebody tells it; before this existed, the pet's spatial knowledge was implicit in a
    /// behaviour table and invisible to the conversation, so "饭碗在哪" had to be answered by
    /// guessing.</item>
    /// <item><b>It answers direction questions on the device.</b> 「饭碗在哪」 is answered from here,
    /// with no network round trip and no tokens, which is also why the answer is always right.</item>
    /// <item><b>It is the vocabulary the orders use</b> (<c>PetCommands</c>), so "去吃饭" and
    /// "饭碗在哪" name the same object the same way.</item>
    /// </list>
    ///
    /// The directions are relative to the pet's own facing rather than to the camera, because that is
    /// the only frame a pet can honestly speak from — and because the pet turns to face the player
    /// when spoken to, so "在你左边" is stable while the camera is not.
    /// </summary>
    public static class PetPerception
    {
        /// <summary>How long a step is, for the "大概 3 步" phrasing. Meters: the room is ~14 wide.</summary>
        public const float StepLength = 0.9f;

        /// <summary>Things further away than this are not worth mentioning.</summary>
        public const float NoticeRange = 18f;

        /// <summary>The name the pet has for a kind of object.</summary>
        public static string NameOf(InteractableKind kind)
        {
            switch (kind)
            {
                case InteractableKind.Food: return "饭碗";
                case InteractableKind.Water: return "水碗";
                case InteractableKind.Ball: return "球";
                case InteractableKind.Bed: return "小床";
                case InteractableKind.Brush: return "梳子";
                case InteractableKind.Toy: return "玩具";
                case InteractableKind.Door: return "门";
                case InteractableKind.Toilet: return "猫砂盆";
                case InteractableKind.Bath: return "澡盆";
                case InteractableKind.Mess: return "地上的水渍";
                case InteractableKind.HidingSpot: return "灌木丛";
                case InteractableKind.AppleTree: return "苹果树";
                case InteractableKind.Pond: return "小池塘";
                case InteractableKind.GrassHeap: return "草堆";
                case InteractableKind.Swing: return "秋千";
                case InteractableKind.Telescope: return "望远镜";
                case InteractableKind.RockingChair: return "摇椅";
                default: return "东西";
            }
        }

        /// <summary>
        /// Which way something is, in the pet's own frame: 正前方, 右前方, 右边, 右后方, 正后方, and
        /// the mirror of each on the left.
        ///
        /// Eight sectors of 45°, because that is the finest distinction a sentence can carry without
        /// becoming a bearing — "右边偏前 12 度" is a compass, not a pet.
        /// </summary>
        public static string DirectionOf(Vector3 from, Vector3 facing, Vector3 to)
        {
            var flat = to - from;
            flat.y = 0f;

            // Close enough that "which way" is the wrong question.
            if (flat.magnitude < 0.35f) return "就在你脚边";

            var look = facing;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f) look = Vector3.forward;

            // SignedAngle is positive clockwise seen from above, which is "to the right" for a
            // character standing in the room.
            float angle = Vector3.SignedAngle(look, flat, Vector3.up);
            int sector = Mathf.Clamp(Mathf.RoundToInt(angle / 45f), -4, 4);

            switch (sector)
            {
                case 0: return "正前方";
                case 1: return "右前方";
                case 2: return "右边";
                case 3: return "右后方";
                case 4: return "正后方";
                case -1: return "左前方";
                case -2: return "左边";
                case -3: return "左后方";
                default: return "正后方";
            }
        }

        /// <summary>How many steps a distance is, never fewer than one.</summary>
        public static int StepsTo(float distance)
            => Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0f, distance) / StepLength));

        /// <summary>"在你右前方，大概 3 步" — the phrase every spatial answer is built from.</summary>
        public static string WhereSentence(Vector3 from, Vector3 facing, Vector3 to)
        {
            var direction = DirectionOf(from, facing, to);

            float flat = new Vector2(to.x - from.x, to.z - from.z).magnitude;
            if (flat < 0.35f) return direction;

            return "在你" + direction + "，大概 " + StepsTo(flat) + " 步";
        }

        /// <summary>
        /// Where the pet is in the room, in words.
        ///
        /// The room's own axes are used rather than "your left", because the pet's left changes every
        /// time it turns; +Z is the far side (the wall behind the furniture) and -Z is the side the
        /// camera and the door are on.
        /// </summary>
        public static string RoomPlace(Vector3 position, float roomSize)
        {
            if (roomSize <= 0.2f) return "房间正中间";

            float half = roomSize * 0.5f;
            string side = position.x < -half * 0.33f ? "左边"
                : position.x > half * 0.33f ? "右边" : "";

            string depth = position.z < -half * 0.25f ? "靠门那侧"
                : position.z > half * 0.25f ? "靠里那侧" : "";

            if (side.Length == 0 && depth.Length == 0) return "房间正中间";
            if (side.Length == 0) return "房间的" + depth;
            if (depth.Length == 0) return "房间的" + side;
            return "房间的" + side + "、" + depth;
        }

        /// <summary>
        /// Everything the pet can see, nearest first.
        ///
        /// Sorted, because "nearest first" is how a pet actually thinks about a room, and because it
        /// makes the prompt stable: the same room produces the same lines in the same order.
        /// </summary>
        public static List<PetTarget> Visible(Vector3 from, Vector3 facing, IList<PetTarget> targets)
        {
            var visible = new List<PetTarget>();
            if (targets == null) return visible;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                float flat = new Vector2(target.Position.x - from.x, target.Position.z - from.z).magnitude;
                if (flat > NoticeRange) continue;
                visible.Add(target);
            }

            visible.Sort((a, b) =>
            {
                float da = new Vector2(a.Position.x - from.x, a.Position.z - from.z).magnitude;
                float db = new Vector2(b.Position.x - from.x, b.Position.z - from.z).magnitude;
                return da.CompareTo(db);
            });

            return visible;
        }

        /// <summary>
        /// The 【你看到的东西】 block: the room, where the pet is standing in it, every object with a
        /// direction and distance, and the owner.
        ///
        /// Written as facts with an instruction attached, because a list of coordinates would make the
        /// model recite them and a list of adjectives would make it invent them. What the model needs
        /// is the same thing the pet needs: what is where, and that it is allowed to act on it.
        /// </summary>
        public static string Describe(Vector3 petPosition, Vector3 facing, IList<PetTarget> targets,
            float roomSize, bool hasOwner, Vector3 ownerPosition)
        {
            var sb = new StringBuilder();

            sb.Append("房间大约 ").Append(Mathf.Max(1, Mathf.RoundToInt(roomSize)))
              .Append(" 米见方，你现在在")
              .Append(RoomPlace(petPosition, roomSize)).Append("。\n");

            var visible = Visible(petPosition, facing, targets);
            if (visible.Count == 0)
            {
                sb.Append("（房间里没有你够得着的东西）\n");
            }
            else
            {
                for (int i = 0; i < visible.Count; i++)
                {
                    var target = visible[i];
                    sb.Append("- 「").Append(string.IsNullOrEmpty(target.Name)
                            ? NameOf(target.Kind) : target.Name).Append("」")
                      .Append(WhereSentence(petPosition, facing, target.Position));
                    if (!target.Ready) sb.Append("（刚用过，等一会儿才能再用）");
                    sb.Append('\n');
                }
            }

            if (hasOwner)
            {
                sb.Append("主人：").Append(WhereSentence(petPosition, facing, ownerPosition))
                  .Append("。你随时可以自己走过去——蹭蹭他、坐到他脚边、或者把玩具叼过去。\n");
            }

            sb.Append("这些都是你亲眼看到的，不是猜的：主人问「XX 在哪」，就照上面回答，" +
                      "不要说自己不知道，也不要把数值念出来。");

            return sb.ToString();
        }
    }
}
