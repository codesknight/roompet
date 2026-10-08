using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Conversation memory. Two tiers:
    ///  - Recent: the last N turns, sent to the model verbatim so short-range context works.
    ///  - Facts: durable notes (name, preferences, promises) captured from the player and
    ///    replayed in every system prompt, so the pet "remembers" across sessions.
    ///
    /// Persisted through PlayerPrefs as JSON. No scene dependency, so it is unit testable.
    /// </summary>
    public class PetMemory
    {
        public const int MaxRecent = 12;
        public const int MaxFacts = 20;

        private readonly List<ChatMessage> _recent = new List<ChatMessage>();
        private readonly List<string> _facts = new List<string>();

        public IReadOnlyList<ChatMessage> Recent => _recent;
        public IReadOnlyList<string> Facts => _facts;

        public void AddUser(string text) => Add(new ChatMessage("user", text, Time.realtimeSinceStartup));
        public void AddPet(string text) => Add(new ChatMessage("pet", text, Time.realtimeSinceStartup));

        public void Add(ChatMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Text)) return;
            _recent.Add(message);
            while (_recent.Count > MaxRecent) _recent.RemoveAt(0);
        }

        public void Remember(string fact)
        {
            if (string.IsNullOrWhiteSpace(fact)) return;
            string trimmed = fact.Trim();
            for (int i = 0; i < _facts.Count; i++)
            {
                if (string.Equals(_facts[i], trimmed)) return;
            }
            _facts.Add(trimmed);
            while (_facts.Count > MaxFacts) _facts.RemoveAt(0);
        }

        /// <summary>Recent turns as "role: text" lines, oldest first.</summary>
        public string[] RecentLines()
        {
            var lines = new string[_recent.Count];
            for (int i = 0; i < _recent.Count; i++)
            {
                var m = _recent[i];
                lines[i] = (m.IsUser ? "主人" : "我") + "：" + m.Text;
            }
            return lines;
        }

        public string[] FactLines() => _facts.ToArray();

        /// <summary>Recent turns as a snapshot, for the context handed to a brain.</summary>
        public ChatMessage[] HistoryArray() => _recent.ToArray();

        /// <summary>The latest user turn, used for the offline brain's intent matching.</summary>
        public string LastUserText()
        {
            for (int i = _recent.Count - 1; i >= 0; i--)
            {
                if (_recent[i].IsUser) return _recent[i].Text;
            }
            return "";
        }

        public void Clear()
        {
            _recent.Clear();
            _facts.Clear();
        }

        // ------------------------------------------------------------------ storage

        private const string RecentKey = "dshpet.memory.recent";
        private const string FactsKey = "dshpet.memory.facts";

        [System.Serializable]
        private class Bag
        {
            public string[] recent;
            public string[] facts;
        }

        public void Save(string petId)
        {
            var bag = new Bag { recent = RecentLines(), facts = _facts.ToArray() };
            PlayerPrefs.SetString(RecentKey + "." + petId, JsonUtility.ToJson(bag));
            PlayerPrefs.Save();
        }

        public void Load(string petId)
        {
            _recent.Clear();
            _facts.Clear();

            string json = PlayerPrefs.GetString(RecentKey + "." + petId, "");
            if (string.IsNullOrEmpty(json)) return;

            Bag bag;
            try { bag = JsonUtility.FromJson<Bag>(json); }
            catch { return; }
            if (bag == null) return;

            if (bag.facts != null)
            {
                foreach (var fact in bag.facts) Remember(fact);
            }

            if (bag.recent != null)
            {
                foreach (var line in bag.recent)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    // Stored as "主人：..." / "我：..." so the split is unambiguous enough.
                    bool user = line.StartsWith("主人：");
                    string text = line.Substring(line.IndexOf('：') + 1);
                    _recent.Add(new ChatMessage(user ? "user" : "pet", text, 0f));
                }
            }
        }

        public string DebugDump()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"recent={_recent.Count} facts={_facts.Count}");
            foreach (var m in _recent) sb.AppendLine("  " + (m.IsUser ? "主人" : "我") + "：" + m.Text);
            foreach (var f in _facts) sb.AppendLine("  [fact] " + f);
            return sb.ToString();
        }
    }
}
