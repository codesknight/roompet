using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Turns a <see cref="PetContext"/> into the system prompt, and turns whatever the
    /// model writes back into a <see cref="PetReply"/>.
    ///
    /// Both halves are pure string work so they can be unit tested without a network or
    /// a scene — which matters, because prompt/parse bugs are otherwise invisible until
    /// the pet starts saying nonsense.
    /// </summary>
    public static class PetPrompting
    {
        /// <summary>The action names the model is allowed to emit.</summary>
        public static string ActionList()
        {
            var names = new List<string>();
            foreach (PetAction action in Enum.GetValues(typeof(PetAction))) names.Add(action.ToString());
            return string.Join(" | ", names.ToArray());
        }

        public static string BuildSystemPrompt(PetContext ctx, int maxSentences = 2)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"你是一只名叫「{Fallback(ctx.PetName, "小家伙")}」的{ctx.SpeciesName}，是主人的宠物，不是 AI 助手。");
            sb.AppendLine();
            sb.AppendLine("【你的性格】");
            sb.AppendLine(Fallback(ctx.Personality, "活泼、亲人。"));
            if (!string.IsNullOrEmpty(ctx.Temperament))
            {
                // The per-pet temperament sits under the species persona: the species says what
                // kind of animal this is, the temperament says which individual it is.
                sb.AppendLine(ctx.Temperament);
            }
            sb.AppendLine();
            sb.AppendLine("【你说话的方式】");
            sb.AppendLine(Fallback(ctx.VoiceStyle, "用口语短句。"));
            sb.AppendLine();
            sb.AppendLine("【你现在的状态】");
            sb.AppendLine($"心情：{PetUtil.MoodLabel(ctx.Mood)}");
            sb.AppendLine($"饱食度：{ctx.Hunger:P0}　精力：{ctx.Energy:P0}　开心度：{ctx.Joy:P0}　干净：{ctx.Cleanliness:P0}");
            if (ctx.Bladder < 0.35f)
            {
                // Only worth mentioning when it is nearly urgent, and phrased as a feeling:
                // a pet that announces its bladder on every turn reads as a status readout.
                sb.AppendLine(ctx.Bladder < 0.18f
                    ? "你现在憋得很难受，坐都坐不住，想赶快去猫砂盆。"
                    : "你有点想上厕所了。");
            }
            sb.AppendLine($"和主人的亲密度：{ctx.Affection:P0}");
            if (!string.IsNullOrEmpty(ctx.DominantNeed))
            {
                sb.AppendLine($"你现在最想要的是：{ctx.DominantNeed}");
            }
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(ctx.Perception))
            {
                // The pet's eyes. Before this existed the model had no idea the room had a bowl in it,
                // so "饭碗在哪" could only be answered by inventing something — and the player's
                // report was that the pet should know where things are and act on it.
                sb.AppendLine("【你看到的东西】");
                sb.AppendLine(ctx.Perception.Trim());
                sb.AppendLine();
            }

            if (ctx.LongTermFacts != null && ctx.LongTermFacts.Length > 0)
            {
                sb.AppendLine("【你记得的事】");
                foreach (var fact in ctx.LongTermFacts) sb.AppendLine("- " + fact);
                sb.AppendLine();
            }

            if (ctx.History != null && ctx.History.Length > 0)
            {
                sb.AppendLine("【最近聊过】");
                foreach (var turn in ctx.History)
                {
                    if (turn.IsSystem) continue;   // the UI's own notes are not the pet's memory
                    sb.AppendLine((turn.IsUser ? "主人" : "我") + "：" + turn.Text);
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(ctx.DayDigest))
            {
                sb.AppendLine("【前几天的事（你的记事本）】");
                sb.AppendLine(ctx.DayDigest);
                sb.AppendLine();
            }

            sb.AppendLine("【规则】");
            sb.AppendLine($"1. 你就是这只宠物，绝对不要提自己是程序、模型或 AI，不要解释规则。");
            sb.AppendLine($"2. 每次最多说 {maxSentences} 句短句，像宠物一样，可以用拟声词和动作描写。");
            sb.AppendLine("3. 状态会影响情绪：饿了/困了/脏了/无聊时要表现出来，但不要直接报数值。");
            sb.AppendLine("4. 可以自然地提起记事本里的事，就像你真的记得。");
            sb.AppendLine("5. 必须严格按下面的格式回答，不要有别的多余内容：");
            sb.AppendLine("第一行：你对主人说的话");
            sb.AppendLine($"第二行：<action>动作</action>，动作只能从这些里选：{ActionList()}");
            sb.AppendLine("第三行（可选）：只有听到值得长期记住的事（主人的喜好、约定、重要日期）时才写");
            sb.AppendLine("<remember>一句话</remember>，其它情况不要写这一行。");
            sb.AppendLine();
            sb.AppendLine("示例：");
            sb.AppendLine("唔……你终于回来啦，我一直趴在门口等你呢。");
            sb.AppendLine("<action>Wag</action>");
            sb.AppendLine("<remember>主人说明天有考试，需要安静。</remember>");

            // Player-authored steering, appended last so it wins over the defaults above while
            // the format contract stays intact.
            if (!string.IsNullOrWhiteSpace(ctx.ExtraInstructions))
            {
                sb.AppendLine();
                sb.AppendLine("【主人的额外要求】（优先遵守，但仍必须遵守上面的回答格式）");
                sb.AppendLine(ctx.ExtraInstructions.Trim());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parses the model output. Accepts the documented two-line format, a JSON object,
        /// or plain prose; always returns something displayable rather than failing.
        /// </summary>
        public static PetReply Parse(string raw, bool fromNetwork = true)
        {
            if (string.IsNullOrWhiteSpace(raw)) return PetReply.Invalid(raw);

            string text = raw.Trim();
            string actionText = null;
            string memoryNote = null;

            // Pull the optional <remember>…</remember> line out first: it is metadata, not
            // something the pet should appear to say.
            int rememberOpen = text.IndexOf("<remember>", StringComparison.OrdinalIgnoreCase);
            if (rememberOpen >= 0)
            {
                int rememberClose = text.IndexOf("</remember>", rememberOpen, StringComparison.OrdinalIgnoreCase);
                if (rememberClose > rememberOpen)
                {
                    memoryNote = text.Substring(rememberOpen + 10, rememberClose - rememberOpen - 10).Trim();
                    text = (text.Substring(0, rememberOpen) + text.Substring(rememberClose + 11)).Trim();
                }
                else
                {
                    memoryNote = text.Substring(rememberOpen + 10).Trim();
                    text = text.Substring(0, rememberOpen).Trim();
                }
            }

            // Prefer a fenced JSON block if the model decided to be clever.
            string json = ExtractJsonObject(text);
            if (json != null)
            {
                string say = JsonField(json, "say");
                string act = JsonField(json, "action");
                string mood = JsonField(json, "mood");
                string aff = JsonField(json, "affection");

                if (!string.IsNullOrEmpty(say))
                {
                    float affection;
                    return new PetReply
                    {
                        Speech = Clean(say),
                        Action = ResolveAction(act, say),
                        MoodWord = mood ?? "",
                        AffectionDelta = float.TryParse(aff, out affection) ? Mathf.Clamp(affection, -0.2f, 0.2f) : 0.02f,
                        MemoryNote = JsonField(json, "remember") ?? memoryNote,
                        FromNetwork = fromNetwork,
                        Raw = raw
                    };
                }
            }

            // Otherwise pull an <action>…</action> tag out and treat the rest as speech.
            int open = text.IndexOf("<action>", StringComparison.OrdinalIgnoreCase);
            if (open >= 0)
            {
                int close = text.IndexOf("</action>", open, StringComparison.OrdinalIgnoreCase);
                if (close > open)
                {
                    actionText = text.Substring(open + 8, close - open - 8);
                    text = (text.Substring(0, open) + text.Substring(close + 9)).Trim();
                }
                else
                {
                    actionText = text.Substring(open + 8).Trim();
                    text = text.Substring(0, open).Trim();
                }
            }

            string speech = Clean(text);
            if (string.IsNullOrEmpty(speech)) return PetReply.Invalid(raw);

            return new PetReply
            {
                Speech = speech,
                Action = ResolveAction(actionText, speech),
                MoodWord = "",
                AffectionDelta = 0.02f,
                MemoryNote = memoryNote,
                FromNetwork = fromNetwork,
                Raw = raw
            };
        }

        private static PetAction ResolveAction(string actionText, string speech)
        {
            PetAction action;
            if (PetUtil.TryParseAction(actionText, out action)) return action;
            // The model skipped the tag: infer from what it wrote.
            if (PetUtil.TryParseAction(speech, out action)) return action;
            return PetAction.Idle;
        }

        /// <summary>Strips stray markup the model may have wrapped the line in.</summary>
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string value = text.Trim();

            // Drop a leading "第一行：" style label and markdown emphasis.
            int colon = value.IndexOf('：');
            if (colon > 0 && colon <= 4) value = value.Substring(colon + 1).Trim();
            value = value.Replace("**", "").Replace("\"", "").Trim();

            // Collapse to at most a few lines.
            var lines = value.Split('\n');
            var kept = new List<string>();
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                kept.Add(trimmed);
                if (kept.Count >= 3) break;
            }
            return string.Join(" ", kept.ToArray()).Trim();
        }

        private static string ExtractJsonObject(string text)
        {
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            return text.Substring(start, end - start + 1);
        }

        /// <summary>Minimal field reader: avoids depending on a JSON library for one shape.</summary>
        private static string JsonField(string json, string field)
        {
            string needle = "\"" + field + "\"";
            int at = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;
            int colon = json.IndexOf(':', at + needle.Length);
            if (colon < 0) return null;

            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return null;

            if (json[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < json.Length)
                {
                    char c = json[i];
                    if (c == '\\' && i + 1 < json.Length) { sb.Append(json[i + 1]); i += 2; continue; }
                    if (c == '"') break;
                    sb.Append(c);
                    i++;
                }
                return sb.ToString();
            }

            int end = i;
            while (end < json.Length && json[end] != ',' && json[end] != '}') end++;
            return json.Substring(i, end - i).Trim();
        }

        private static string Fallback(string value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
