using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The sliding picture puzzle, as arithmetic.
    ///
    /// Pure on purpose — no scene, no UI, no Texture2D — because the interesting part of a puzzle
    /// is the state machine, and "is it solved", "which moves are legal" and "what does it pay
    /// out" are exactly the things that are painful to check by hand and trivial to check in a
    /// test. The picture is drawn somewhere else; this only knows that a grid of tiles has one
    /// empty slot in it and that sliding a neighbour into that slot is the only move there is.
    ///
    /// A sliding puzzle rather than a jigsaw-with-drag: dragging pieces is a mouse gesture that
    /// has to be re-invented for a thumb, while "tap the tile next to the hole" is the same
    /// gesture on both, needs no grab radius, and cannot be played into an unsolvable state.
    /// </summary>
    public class PetPuzzle
    {
        /// <summary>Side length. 3x3 is solvable in a minute and readable on a phone.</summary>
        public const int Size = 3;

        public const int TileCount = Size * Size;

        /// <summary>Value of the empty slot.</summary>
        public const int Empty = 0;

        private readonly int[] _tiles = new int[TileCount];

        /// <summary>The board, row-major from the top-left. 0 is the empty slot.</summary>
        public int this[int index] => _tiles[Mathf.Clamp(index, 0, TileCount - 1)];

        public int Moves { get; private set; }

        /// <summary>Moves the player has made since the shuffle, for the score.</summary>
        public int EmptyIndex
        {
            get
            {
                for (int i = 0; i < TileCount; i++)
                {
                    if (_tiles[i] == Empty) return i;
                }
                return TileCount - 1;
            }
        }

        /// <summary>True when every tile is back where the picture wants it.</summary>
        public bool IsSolved
        {
            get
            {
                for (int i = 0; i < TileCount - 1; i++)
                {
                    if (_tiles[i] != i + 1) return false;
                }
                return _tiles[TileCount - 1] == Empty;
            }
        }

        private PetPuzzle() { }

        /// <summary>A shuffled board. Never starts solved.</summary>
        public static PetPuzzle Start(int seed, int shuffleSteps = 120)
        {
            var puzzle = new PetPuzzle();
            puzzle.Reset();

            // Shuffled by legal moves rather than by permuting: half of all permutations of a
            // 3x3 board cannot be solved at all, and a puzzle that is impossible to finish is
            // the kind of bug a player reads as "this game is broken".
            var rng = new System.Random(seed);
            int last = -1;
            for (int i = 0; i < Mathf.Max(20, shuffleSteps); i++)
            {
                int slot = puzzle.EmptyIndex;
                int[] neighbours = Neighbours(slot);

                int pick;
                int guard = 0;
                do
                {
                    pick = neighbours[rng.Next(neighbours.Length)];
                    guard++;
                } while (pick == last && guard < 8);

                puzzle.Slide(pick);
                last = slot;
            }

            puzzle.Moves = 0;
            if (puzzle.IsSolved) puzzle.Slide(Neighbours(puzzle.EmptyIndex)[0]);
            puzzle.Moves = 0;
            return puzzle;
        }

        /// <summary>The solved board, for tests and for the "show me the picture" preview.</summary>
        public static PetPuzzle Solved()
        {
            var puzzle = new PetPuzzle();
            puzzle.Reset();
            return puzzle;
        }

        private void Reset()
        {
            for (int i = 0; i < TileCount; i++) _tiles[i] = i == TileCount - 1 ? Empty : i + 1;
            Moves = 0;
        }

        /// <summary>Indexes that can slide into the empty slot right now.</summary>
        public int[] MovableTiles()
        {
            int[] neighbours = Neighbours(EmptyIndex);
            var movable = new int[neighbours.Length];
            for (int i = 0; i < neighbours.Length; i++) movable[i] = _tiles[neighbours[i]];
            return movable;
        }

        /// <summary>True when the tile at <paramref name="index"/> is next to the hole.</summary>
        public bool CanSlide(int index)
        {
            if (index < 0 || index >= TileCount) return false;
            if (_tiles[index] == Empty) return false;

            int row = index / Size, column = index % Size;
            int emptyRow = EmptyIndex / Size, emptyColumn = EmptyIndex % Size;
            return Mathf.Abs(row - emptyRow) + Mathf.Abs(column - emptyColumn) == 1;
        }

        /// <summary>Slides the tile at <paramref name="index"/> into the hole. Counts as a move.</summary>
        public bool TrySlide(int index)
        {
            if (!CanSlide(index)) return false;
            Slide(index);
            Moves++;
            return true;
        }

        private void Slide(int index)
        {
            int empty = EmptyIndex;
            _tiles[empty] = _tiles[index];
            _tiles[index] = Empty;
        }

        private static int[] Neighbours(int index)
        {
            int row = index / Size, column = index % Size;
            var list = new System.Collections.Generic.List<int>(4);

            if (row > 0) list.Add(index - Size);
            if (row < Size - 1) list.Add(index + Size);
            if (column > 0) list.Add(index - 1);
            if (column < Size - 1) list.Add(index + 1);

            return list.ToArray();
        }

        /// <summary>
        /// The fewest moves a 3x3 board needs from a fresh shuffle, as the par the payout is
        /// measured against.
        ///
        /// Not the true optimal solution — that is a breadth-first search over 181440 states and
        /// belongs nowhere near a phone — but a fair par that a competent player beats and a
        /// flailing one does not. It is deliberately generous: the point of the game is the coins
        /// and the pet, not a leaderboard.
        /// </summary>
        public const int Par = 22;

        /// <summary>Coins for finishing in this many moves. Never zero: finishing is the point.</summary>
        public static int Reward(int moves)
        {
            int over = Mathf.Max(0, moves - Par);
            return Mathf.Clamp(40 - over * 2, 8, 40);
        }

        /// <summary>Coins for the board as it stands, or zero while it is unsolved.</summary>
        public int PendingReward => IsSolved ? Reward(Moves) : 0;

        /// <summary>A compact snapshot, for the saved-game-free "resume where I was" case.</summary>
        public string Encode()
        {
            var sb = new System.Text.StringBuilder(TileCount + 4);
            for (int i = 0; i < TileCount; i++) sb.Append(_tiles[i].ToString("X1"));
            sb.Append(':').Append(Moves);
            return sb.ToString();
        }

        /// <summary>Reads back <see cref="Encode"/>. Returns null for anything malformed.</summary>
        public static PetPuzzle Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return null;

            int colon = encoded.IndexOf(':');
            if (colon != TileCount) return null;

            var puzzle = new PetPuzzle();
            var seen = new bool[TileCount + 1];

            for (int i = 0; i < TileCount; i++)
            {
                int value;
                if (!int.TryParse(encoded[i].ToString(), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out value)) return null;
                if (value < 0 || value > TileCount) return null;
                if (seen[value]) return null;      // duplicates would make a tile vanish
                seen[value] = true;
                puzzle._tiles[i] = value;
            }

            if (!seen[Empty]) return null;

            int moves;
            if (!int.TryParse(encoded.Substring(colon + 1), out moves)) return null;
            puzzle.Moves = Mathf.Max(0, moves);
            return puzzle;
        }
    }
}
