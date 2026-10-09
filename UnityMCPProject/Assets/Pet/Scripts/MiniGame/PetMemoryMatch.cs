using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>How hard the memory board is, and therefore what finishing it is worth.</summary>
    public enum MemoryDifficulty { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>
    /// 记忆配对: turn over two cards, keep the ones that match.
    ///
    /// A pure state machine, like the puzzle: a deck of pairs, two face-up slots, and a rule for
    /// what happens on the third flip. The cards are the animals from around the room, so the game
    /// reads as part of the pet's world rather than as a minigame bolted on.
    ///
    /// The faces used to be text — a Chinese character per card — which worked and looked like a
    /// spreadsheet. They are drawn animals now (<see cref="PetAvatarArt"/>), and the vocabulary
    /// below stays as the *name* of each face: it is what the tests count, what a message can say,
    /// and what a player who cannot make out a 60px cartoon can still be told.
    ///
    /// Three difficulties, and they are not cosmetic: a bigger board needs more attention and pays
    /// more, which is the only reason to offer a harder one.
    /// </summary>
    public class PetMemoryMatch
    {
        /// <summary>The deck's vocabulary: eight animals, named. Pairs of these make the deck.</summary>
        public static readonly string[] SafeFaces = { "猫", "狗", "兔", "熊", "狐狸", "熊猫", "猪", "青蛙" };

        public const int Columns = 4;

        /// <summary>Cards a board can hold at most, for the UI's per-slot bookkeeping.</summary>
        public const int MaxSlots = 16;

        public int Pairs { get; private set; }
        public int Moves { get; private set; }
        public int Matched { get; private set; }

        /// <summary>Which difficulty this board was dealt for.</summary>
        public MemoryDifficulty Difficulty { get; private set; } = MemoryDifficulty.Normal;

        private readonly List<int> _faces = new List<int>();
        private readonly List<bool> _faceUp = new List<bool>();
        private readonly List<bool> _taken = new List<bool>();

        private int _first = -1;
        private int _second = -1;

        /// <summary>Set for one flip after a pair is found, so the UI can celebrate.</summary>
        public bool LastFlipMatched { get; private set; }

        /// <summary>Set while two cards are up and do not match; the UI waits for the reveal.</summary>
        public bool WaitingToHide => _first >= 0 && _second >= 0 && _faces[_first] != _faces[_second];

        public int Count => _faces.Count;
        public int SlotCount => _faces.Count;

        public int FaceAt(int index) => index >= 0 && index < _faces.Count ? _faces[index] : -1;
        public bool IsFaceUp(int index) => index >= 0 && index < _faces.Count && _faceUp[index];
        public bool IsTaken(int index) => index >= 0 && index < _faces.Count && _taken[index];

        /// <summary>Whether this card may be turned right now.</summary>
        public bool CanFlip(int index)
        {
            if (index < 0 || index >= _faces.Count) return false;
            if (_taken[index] || _faceUp[index]) return false;
            if (WaitingToHide) return false;
            return _first < 0 || _second < 0;
        }

        public bool IsSolved => Matched == Pairs && Pairs > 0;

        /// <summary>A fresh deck: <paramref name="pairs"/> pairs, shuffled with the given seed.</summary>
        public static PetMemoryMatch Start(int pairs, int seed)
        {
            var game = new PetMemoryMatch();
            pairs = Mathf.Clamp(pairs, 2, SafeFaces.Length);
            game.Pairs = pairs;
            game.Difficulty = DifficultyFor(pairs);

            var deck = new List<int>();
            for (int i = 0; i < pairs; i++)
            {
                deck.Add(i);
                deck.Add(i);
            }

            // Fisher-Yates with an injected seed, so a test can reproduce a layout exactly.
            var rng = new System.Random(seed);
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int swap = deck[i];
                deck[i] = deck[j];
                deck[j] = swap;
            }

            game._faces.AddRange(deck);
            for (int i = 0; i < deck.Count; i++)
            {
                game._faceUp.Add(false);
                game._taken.Add(false);
            }

            return game;
        }

        /// <summary>
        /// Turns a card over. Returns true when the flip was legal.
        ///
        /// Two cards up and matching are banked immediately; two that differ stay up until
        /// <see cref="HideMismatch"/> is called, so the player actually gets to see them.
        /// </summary>
        public bool Flip(int index)
        {
            if (!CanFlip(index)) return false;

            LastFlipMatched = false;
            _faceUp[index] = true;

            if (_first < 0)
            {
                _first = index;
                return true;
            }

            _second = index;
            Moves++;

            if (_faces[_first] == _faces[_second])
            {
                _taken[_first] = true;
                _taken[_second] = true;
                Matched++;
                LastFlipMatched = true;
                _first = -1;
                _second = -1;
            }

            return true;
        }

        /// <summary>Puts a mismatched pair back face down. Called by the UI after its pause.</summary>
        public bool HideMismatch()
        {
            if (!WaitingToHide) return false;

            _faceUp[_first] = false;
            _faceUp[_second] = false;
            _first = -1;
            _second = -1;
            return true;
        }

        /// <summary>A fresh deck at a chosen difficulty.</summary>
        public static PetMemoryMatch Start(MemoryDifficulty difficulty, int seed)
            => Start(PairsFor(difficulty), seed);

        // ----------------------------------------------------------------- difficulty

        /// <summary>How many pairs each difficulty deals.</summary>
        public static int PairsFor(MemoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case MemoryDifficulty.Easy: return 4;
                case MemoryDifficulty.Hard: return 8;
                default: return 6;
            }
        }

        /// <summary>The difficulty a board of this size is.</summary>
        public static MemoryDifficulty DifficultyFor(int pairs)
        {
            if (pairs <= 4) return MemoryDifficulty.Easy;
            if (pairs >= 8) return MemoryDifficulty.Hard;
            return MemoryDifficulty.Normal;
        }

        public static string NameOf(MemoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case MemoryDifficulty.Easy: return "简单";
                case MemoryDifficulty.Hard: return "困难";
                default: return "中等";
            }
        }

        /// <summary>A one-line description, for the button's tooltip-sized caption.</summary>
        public static string Describe(MemoryDifficulty difficulty)
            => $"{NameOf(difficulty)}：{PairsFor(difficulty)} 对，最多 {BaseRewardFor(difficulty)} 个宠物币";

        /// <summary>Stable key for saving the chosen difficulty.</summary>
        public static string KeyOf(MemoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case MemoryDifficulty.Easy: return "easy";
                case MemoryDifficulty.Hard: return "hard";
                default: return "normal";
            }
        }

        /// <summary>Reads back a saved key, falling back rather than throwing on junk.</summary>
        public static MemoryDifficulty ParseDifficulty(string saved, MemoryDifficulty fallback)
        {
            if (string.IsNullOrEmpty(saved)) return fallback;
            switch (saved.Trim().ToLowerInvariant())
            {
                case "easy": return MemoryDifficulty.Easy;
                case "normal": return MemoryDifficulty.Normal;
                case "hard": return MemoryDifficulty.Hard;
                default: return fallback;
            }
        }

        /// <summary>Any int, as a valid difficulty.</summary>
        public static MemoryDifficulty ClampDifficulty(int value)
            => (MemoryDifficulty)Mathf.Clamp(value, 0, 2);

        // --------------------------------------------------------------------- reward

        /// <summary>
        /// What a perfect game on this board pays. The three boards pay 20 / 30 / 45: a harder
        /// board is more work, and if it paid the same there would be no reason to pick it.
        /// </summary>
        public static int BaseRewardFor(int pairs)
        {
            switch (pairs)
            {
                case 4: return 20;
                case 6: return 30;
                case 8: return 45;
                default: return Mathf.Max(10, Mathf.RoundToInt(pairs * 5.5f));
            }
        }

        public static int BaseRewardFor(MemoryDifficulty difficulty)
            => BaseRewardFor(PairsFor(difficulty));

        /// <summary>What finishing this board is worth right now.</summary>
        public static int Reward(MemoryDifficulty difficulty, int moves)
            => Reward(PairsFor(difficulty), moves);

        /// <summary>Coins for finishing: fewer moves pays more, but finishing always pays.</summary>
        public static int Reward(int pairs, int moves)
        {
            int baseReward = BaseRewardFor(pairs);
            int par = pairs * 2;                       // two flips per match is the perfect game
            int over = Mathf.Max(0, moves - par);

            // The floor is a third of the board's value: flailing at a hard board still beats a
            // perfect easy one only if the player earned it, but it never pays nothing — a board
            // completed with nothing to show for it is a board nobody finishes twice.
            int floor = Mathf.Max(6, Mathf.RoundToInt(baseReward * 0.34f));
            return Mathf.Clamp(baseReward - over, floor, baseReward);
        }

        /// <summary>The difficulty this board was dealt for, as a name.</summary>
        public string DifficultyName => NameOf(Difficulty);

        public int PendingReward => IsSolved ? Reward(Pairs, Moves) : 0;

        /// <summary>A word for the result line.</summary>
        public static string RankFor(int pairs, int moves)
        {
            int par = pairs * 2;
            if (moves <= par) return "记性好得吓人";
            if (moves <= par + 3) return "几乎没记错";
            if (moves <= par + 8) return "记得不错";
            return "记住了几张";
        }

        /// <summary>The face to print on a card, with the same fallback the UI uses.</summary>
        public static string Label(int face)
        {
            if (face < 0 || face >= SafeFaces.Length) return "?";
            return SafeFaces[face];
        }

        /// <summary>The animal drawn on a card, for the UI's texture lookup.</summary>
        public static PetFaceKind Kind(int face) => PetAvatarArt.KindAt(face);

        /// <summary>A compact snapshot, so a half-finished board can survive a scene reload.</summary>
        public string Encode()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _faces.Count; i++) sb.Append((char)('A' + _faces[i]));
            sb.Append(':').Append(Moves);
            return sb.ToString();
        }
    }
}
