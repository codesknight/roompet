using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DshPet
{
    /// <summary>What a journal entry records. Kept coarse on purpose: the calendar colours
    /// days by kind, so the list has to stay readable at a glance.</summary>
    public enum MemoryKind
    {
        /// <summary>The player and the pet talked.</summary>
        Chat,
        /// <summary>Fed, watered, brushed, put to bed.</summary>
        Care,
        /// <summary>Played, chased the ball.</summary>
        Play,
        /// <summary>Slept, napped, woke up.</summary>
        Rest,
        /// <summary>Notable mood swings worth remembering.</summary>
        Mood,
        /// <summary>The pet promised something, or the player did.</summary>
        Promise,
        /// <summary>A durable fact about the player ("主人在准备考试").</summary>
        Preference,
        /// <summary>Something that only happens once: first meeting, level ups.</summary>
        Milestone
    }

    /// <summary>
    /// One dated row in the pet's notebook.
    ///
    /// Serialised with JsonUtility, so it is a plain class of primitives. <see cref="Id"/>
    /// is stable across saves, which is what a future cloud sync would key on.
    /// </summary>
    [Serializable]
    public class JournalEntry
    {
        public string Id = "";
        public string PetId = "";
        /// <summary>Local time, as ticks. Local because the calendar is a human-facing view.</summary>
        public long Ticks;
        public MemoryKind Kind = MemoryKind.Chat;
        public string Title = "";
        public string Detail = "";
        /// <summary>0..1. High-importance and pinned entries survive pruning.</summary>
        public float Importance = 0.4f;
        /// <summary>Pinned entries are long-term memory and are never dropped.</summary>
        public bool Pinned;

        public DateTime When => new DateTime(Ticks);
        public string DayKey => When.ToString("yyyy-MM-dd");

        public static string MakeId(DateTime when, MemoryKind kind, string title)
        {
            // Deterministic-ish, but unique enough for a single-player notebook.
            int hash = (title ?? "").GetHashCode();
            return $"{when:yyyyMMddHHmmss}-{(int)kind}-{hash & 0xFFFF:x4}";
        }
    }

    /// <summary>
    /// The pet's memory, filed by date. This is the "smart notebook": an append-only list
    /// of dated entries plus a day index so the calendar can be drawn without scanning.
    ///
    /// Persisted to a JSON file under <see cref="Application.persistentDataPath"/> rather
    /// than PlayerPrefs, because a journal is expected to grow past what the registry
    /// comfortably holds. The wrapper carries a schema version so a future cloud store can
    /// migrate it.
    /// </summary>
    [Serializable]
    public class PetJournal
    {
        public const int CurrentVersion = 1;
        public const int DefaultMaxEntries = 600;

        [Serializable]
        private class Bag
        {
            public int version = CurrentVersion;
            public string petId = "";
            public List<JournalEntry> entries = new List<JournalEntry>();
        }

        [SerializeField] private string _petId = "fox";
        private readonly List<JournalEntry> _entries = new List<JournalEntry>();
        private readonly Dictionary<string, List<JournalEntry>> _byDay = new Dictionary<string, List<JournalEntry>>();

        public string PetId => _petId;
        public int Count => _entries.Count;
        public IReadOnlyList<JournalEntry> Entries => _entries;

        /// <summary>Raised whenever something is filed, so the UI can refresh.</summary>
        public event Action Changed;

        public void Bind(string petId)
        {
            _petId = string.IsNullOrEmpty(petId) ? "fox" : petId;
            Load();
        }

        // ------------------------------------------------------------------- writing

        public JournalEntry Add(MemoryKind kind, string title, string detail = "",
            float importance = 0.4f, bool pinned = false, DateTime? when = null)
        {
            var stamp = when ?? DateTime.Now;
            var entry = new JournalEntry
            {
                Id = JournalEntry.MakeId(stamp, kind, title),
                PetId = _petId,
                Ticks = stamp.Ticks,
                Kind = kind,
                Title = title ?? "",
                Detail = detail ?? "",
                Importance = Mathf.Clamp01(importance),
                Pinned = pinned
            };

            _entries.Add(entry);
            Index(entry);
            Prune();
            Changed?.Invoke();
            return entry;
        }

        /// <summary>
        /// Files a conversation turn. Several turns on the same day collapse into one Chat
        /// entry so a chatty afternoon does not bury the rest of the calendar; the detail
        /// keeps the running transcript.
        /// </summary>
        public JournalEntry NoteConversation(string userLine, string petLine, DateTime? when = null)
        {
            var stamp = when ?? DateTime.Now;
            string dayKey = stamp.ToString("yyyy-MM-dd");

            var existing = TodayEntry(dayKey, MemoryKind.Chat);
            if (existing != null)
            {
                existing.Detail = Trim(existing.Detail + "\n主人：" + userLine + "\n" + _petId + "：" + petLine, 1200);
                existing.Ticks = stamp.Ticks;
                existing.Importance = Mathf.Min(0.7f, existing.Importance + 0.03f);
                Changed?.Invoke();
                return existing;
            }

            return Add(MemoryKind.Chat, "聊天", "主人：" + userLine + "\n" + _petId + "：" + petLine,
                0.35f, false, stamp);
        }

        public JournalEntry Remember(string fact, MemoryKind kind = MemoryKind.Preference)
        {
            if (string.IsNullOrWhiteSpace(fact)) return null;

            string trimmed = fact.Trim();
            // Do not file the same fact twice.
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Pinned && string.Equals(_entries[i].Title, trimmed, StringComparison.Ordinal))
                {
                    return _entries[i];
                }
            }

            return Add(kind, trimmed, "", 0.9f, true);
        }

        private JournalEntry TodayEntry(string dayKey, MemoryKind kind)
        {
            if (!_byDay.TryGetValue(dayKey, out var list)) return null;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Kind == kind && !list[i].Pinned) return list[i];
            }
            return null;
        }

        // ------------------------------------------------------------------- reading

        public List<JournalEntry> ForDay(DateTime day)
        {
            string key = day.ToString("yyyy-MM-dd");
            if (_byDay.TryGetValue(key, out var list))
            {
                var copy = new List<JournalEntry>(list);
                copy.Sort((a, b) => a.Ticks.CompareTo(b.Ticks));
                return copy;
            }
            return new List<JournalEntry>();
        }

        public List<JournalEntry> ForMonth(int year, int month)
        {
            var result = new List<JournalEntry>();
            string prefix = $"{year:D4}-{month:D2}-";
            foreach (var entry in _entries)
            {
                if (entry.DayKey.StartsWith(prefix, StringComparison.Ordinal)) result.Add(entry);
            }
            result.Sort((a, b) => a.Ticks.CompareTo(b.Ticks));
            return result;
        }

        /// <summary>Distinct kinds active on a day — drives the calendar's colour dots.</summary>
        public List<MemoryKind> KindsOn(DateTime day)
        {
            var kinds = new List<MemoryKind>();
            var entries = ForDay(day);
            for (int i = 0; i < entries.Count; i++)
            {
                if (!kinds.Contains(entries[i].Kind)) kinds.Add(entries[i].Kind);
            }
            return kinds;
        }

        public int CountOn(DateTime day) => ForDay(day).Count;

        /// <summary>Every pinned entry, oldest first. This is the model's long-term memory.</summary>
        public List<JournalEntry> LongTerm()
        {
            var result = new List<JournalEntry>();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Pinned) result.Add(_entries[i]);
            }
            result.Sort((a, b) => a.Ticks.CompareTo(b.Ticks));
            return result;
        }

        public string[] LongTermLines(int max = 20)
        {
            var pinned = LongTerm();
            int start = Mathf.Max(0, pinned.Count - max);
            var lines = new List<string>();
            for (int i = start; i < pinned.Count; i++) lines.Add(pinned[i].Title);
            return lines.ToArray();
        }

        public List<JournalEntry> Search(string query)
        {
            var result = new List<JournalEntry>();
            if (string.IsNullOrWhiteSpace(query)) return result;

            string needle = query.Trim().ToLowerInvariant();
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Title.ToLowerInvariant().Contains(needle) ||
                    entry.Detail.ToLowerInvariant().Contains(needle))
                {
                    result.Add(entry);
                }
            }
            return result;
        }

        /// <summary>A compact digest of recent days, injected into the prompt so the pet can
        /// refer to what already happened without shipping the whole notebook.
        ///
        /// Today is included (as "今天：") because otherwise a brand-new pet has nothing to
        /// refer to at all, and because it gives the model context beyond the 12-turn
        /// window for anything that scrolled out of the transcript.</summary>
        public string RecentDigest(int days = 3, int maxChars = 700)
        {
            var sb = new StringBuilder();
            var today = DateTime.Now.Date;

            for (int offset = days; offset >= 0; offset--)
            {
                var day = today.AddDays(-offset);
                var entries = ForDay(day);
                if (entries.Count == 0) continue;

                // Chat rows are already in the transcript; the digest is about events.
                var titles = new List<string>();
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Kind == MemoryKind.Chat) continue;
                    string title = entries[i].Title;
                    if (title.Length == 0 || titles.Contains(title)) continue;
                    titles.Add(title);
                }
                if (titles.Count == 0) continue;

                string label = offset == 0 ? "今天：" : (offset == 1 ? "昨天：" : $"{offset} 天前：");
                sb.Append(label);
                sb.AppendLine(string.Join("、", titles.ToArray()));
            }

            string text = sb.ToString().Trim();
            return text.Length <= maxChars ? text : text.Substring(text.Length - maxChars);
        }

        public void Clear()
        {
            _entries.Clear();
            _byDay.Clear();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ deleting

        /// <summary>
        /// Removes one entry and drops it from the day index.
        ///
        /// Pinned entries are deletable too, deliberately: "pinned" means "never pruned
        /// automatically", not "impossible to get rid of". A player who wants a memory gone
        /// wants it gone, and the alternative — a row that quietly refuses to be deleted — is
        /// worse than either.
        /// </summary>
        public bool Delete(JournalEntry entry)
        {
            if (entry == null) return false;

            int at = _entries.IndexOf(entry);
            if (at < 0)
            {
                // Fall back to the id: the UI may hold a copy from a previous list.
                at = _entries.FindIndex(e => e != null && e.Id == entry.Id);
                if (at < 0) return false;
                entry = _entries[at];
            }

            _entries.RemoveAt(at);

            if (_byDay.TryGetValue(entry.DayKey, out var list))
            {
                list.Remove(entry);
                if (list.Count == 0) _byDay.Remove(entry.DayKey);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes every entry filed under one day. Returns how many went.</summary>
        public int DeleteDay(DateTime day)
        {
            string key = day.ToString("yyyy-MM-dd");
            if (!_byDay.TryGetValue(key, out var list) || list.Count == 0) return 0;

            int removed = list.Count;
            var doomed = new List<JournalEntry>(list);
            for (int i = 0; i < doomed.Count; i++) _entries.Remove(doomed[i]);
            _byDay.Remove(key);

            Changed?.Invoke();
            return removed;
        }

        /// <summary>Removes everything filed under one month. Returns how many went.</summary>
        public int DeleteMonth(int year, int month)
        {
            var doomed = ForMonth(year, month);
            if (doomed.Count == 0) return 0;

            for (int i = 0; i < doomed.Count; i++) _entries.Remove(doomed[i]);

            // Rebuilding the index is cheaper than patching every affected day.
            _byDay.Clear();
            for (int i = 0; i < _entries.Count; i++) Index(_entries[i]);

            Changed?.Invoke();
            return doomed.Count;
        }

        /// <summary>How many entries are pinned — the ones the model asked to keep.</summary>
        public int PinnedCount
        {
            get
            {
                int pinned = 0;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i] != null && _entries[i].Pinned) pinned++;
                }
                return pinned;
            }
        }

        /// <summary>
        /// Bytes the journal file occupies on disk, or 0 when it has never been written.
        ///
        /// Read from the file rather than estimated from the entry count: the point of showing
        /// it is to answer "what is this costing me", and an estimate that disagrees with the
        /// file manager is worse than no number.
        /// </summary>
        public long StorageBytes
        {
            get
            {
                try
                {
                    string path = Path(_petId);
                    return System.IO.File.Exists(path) ? new System.IO.FileInfo(path).Length : 0L;
                }
                catch
                {
                    return 0L;
                }
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("F1") + " KB";
            return (bytes / (1024f * 1024f)).ToString("F1") + " MB";
        }

        // ------------------------------------------------------------------ internals

        private void Index(JournalEntry entry)
        {
            string key = entry.DayKey;
            if (!_byDay.TryGetValue(key, out var list))
            {
                list = new List<JournalEntry>();
                _byDay[key] = list;
            }
            list.Add(entry);
        }

        /// <summary>Drops the least important old entries. Pinned entries are never removed,
        /// which is what makes "remember this" actually durable.</summary>
        private void Prune(int max = DefaultMaxEntries)
        {
            if (_entries.Count <= max) return;

            _entries.Sort((a, b) =>
            {
                if (a.Pinned != b.Pinned) return a.Pinned ? -1 : 1;
                int byImportance = b.Importance.CompareTo(a.Importance);
                return byImportance != 0 ? byImportance : b.Ticks.CompareTo(a.Ticks);
            });

            var kept = _entries.GetRange(0, max);
            kept.Sort((a, b) => a.Ticks.CompareTo(b.Ticks));

            _entries.Clear();
            _entries.AddRange(kept);

            _byDay.Clear();
            for (int i = 0; i < _entries.Count; i++) Index(_entries[i]);
        }

        private static string Trim(string value, int max)
            => string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(value.Length - max);

        // ------------------------------------------------------------------- storage

        private string Path(string petId) =>
            System.IO.Path.Combine(Application.persistentDataPath, $"dshpet-journal-{petId}.json");

        public void Save()
        {
            try
            {
                var bag = new Bag { version = CurrentVersion, petId = _petId, entries = _entries };
                System.IO.File.WriteAllText(Path(_petId), JsonUtility.ToJson(bag), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DshPet] Could not save the journal: " + e.Message);
            }
        }

        public void Load()
        {
            _entries.Clear();
            _byDay.Clear();

            try
            {
                string file = Path(_petId);
                if (!System.IO.File.Exists(file)) return;

                var bag = JsonUtility.FromJson<Bag>(System.IO.File.ReadAllText(file));
                if (bag == null || bag.entries == null) return;

                if (bag.version > CurrentVersion)
                {
                    Debug.LogWarning($"[DshPet] Journal version {bag.version} is newer than {CurrentVersion}; " +
                                     "reading what we understand.");
                }

                for (int i = 0; i < bag.entries.Count; i++)
                {
                    var entry = bag.entries[i];
                    if (entry == null || entry.Ticks <= 0) continue;
                    _entries.Add(entry);
                    Index(entry);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DshPet] Could not load the journal: " + e.Message);
            }
        }

        public string DebugDump(int days = 5)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"journal '{_petId}': {_entries.Count} entries, {_byDay.Count} days");
            var today = DateTime.Now.Date;
            for (int offset = 0; offset < days; offset++)
            {
                var day = today.AddDays(-offset);
                var entries = ForDay(day);
                if (entries.Count == 0) continue;
                sb.AppendLine($"  {day:yyyy-MM-dd}  ({entries.Count})");
                foreach (var entry in entries) sb.AppendLine($"     [{entry.Kind}] {entry.Title}");
            }
            return sb.ToString();
        }
    }
}
