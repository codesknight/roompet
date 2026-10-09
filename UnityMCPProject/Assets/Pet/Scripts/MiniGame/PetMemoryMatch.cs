using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// 记忆配对: turn over two cards, keep the ones that match.
    ///
    /// A pure state machine, like the puzzle: a deck of pairs, two face-up slots, and a rule for
    /// what happens on the third flip. The interesting part is that the *cards* are this pet's own
    /// vocabulary — the symbols are drawn from a small table of pet-things, so the game reads as
    /// part of the room rather than as a minigame bolted on.
    ///
    /// No scene, no textures: the faces are text, which also means there is nothing to get wrong
    /// on a phone with a different font.
    /// </summary>
    public class PetMemoryMatch
    {
        /// <summary>What is printed on a card face. Pairs of these make the deck.</summary>
        public static readonly string[] Faces = { "🍎", "🐟", "🦴", "🎾", "🧶", "🥕", "🍖", "🪀" };

        /// <summary>
        /// Fallback faces, because Unity's built-in font has no emoji.
        ///
        /// The first version of this used emoji and every card came out blank — the same lesson the
        /// pet chips and the round buttons taught. These are the ones actually used, and the emoji
        /// list above is kept only as the "nicer if the font ever has them" note.
        /// </summary>
        public static readonly string[] SafeFaces = { "果", "鱼", "骨", "球", "毛", "萝", "肉", "铃" };

        public const int Columns = 4;

        public int Pairs { get; private set; }
        public int Moves { get; private set; }
        public int Matched { get; private set; }

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

        /// <summary>Coins for finishing: fewer moves pays more, but finishing always pays.</summary>
        public static int Reward(int pairs, int moves)
        {
            int par = pairs * 2;                       // two flips per match is the perfect game
            int over = Mathf.Max(0, moves - par);
            return Mathf.Clamp(30 - over, 10, 30);
        }

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
