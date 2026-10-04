using System;
using System.Collections.Generic;
using System.Linq;
using Template.Core.Random;

namespace CadenceClub.Core
{
    /// <summary>A squad rider in a level: clearing pieces of their colour fills the charge; full, the power is ready.</summary>
    public sealed class RiderSlot
    {
        public RiderSlot(RiderDef def, int level, int charge = 0)
        {
            Def = def;
            Level = level;
            Charge = charge;
        }

        public RiderDef Def { get; }
        public int Level { get; }
        public int Charge { get; internal set; }
        public bool Full => Charge >= Def.charge;
        public float Fill => Math.Min(1f, Charge / (float)Def.charge);
        public int Amount => Def.Power(Level);
    }

    /// <summary>Where rider powers land. Goal pieces and obstacles the goals need count extra, so powers help the level.</summary>
    public static class PowerTargets
    {
        /// <summary>Every cell of the <paramref name="count"/> rows (or columns) with the most to gain.</summary>
        public static List<Cell> Lines(LevelState state, int count, bool rows)
        {
            var board = state.Board;
            int lines = rows ? board.Height : board.Width;
            int length = rows ? board.Width : board.Height;
            var best = Enumerable.Range(0, lines)
                .OrderByDescending(i => Enumerable.Range(0, length).Sum(j => Value(state, rows ? new Cell(j, i) : new Cell(i, j))))
                .ThenBy(i => i)
                .Take(count);
            return best.SelectMany(i => Enumerable.Range(0, length).Select(j => rows ? new Cell(j, i) : new Cell(i, j)))
                .Where(board.IsPlayable).ToList();
        }

        /// <summary>Up to <paramref name="count"/> random crates, ice and chains; random pieces fill in when there are fewer.</summary>
        public static List<Cell> Obstacles(LevelState state, SeededRandom rng, int count)
        {
            var board = state.Board;
            var cells = All(board).ToList();
            var obstacles = cells.Where(c => !board.CoverAt(c).IsEmpty).ToList();
            var pieces = cells.Where(c => board.CoverAt(c).IsEmpty && !board[c].IsEmpty && !board[c].trophy).ToList();
            rng.Shuffle(obstacles);
            rng.Shuffle(pieces);
            return obstacles.Concat(pieces).Take(count).ToList();
        }

        /// <summary>Up to <paramref name="count"/> random plain, free pieces (no special, no chain).</summary>
        public static List<Cell> PlainPieces(LevelState state, SeededRandom rng, int count)
        {
            var board = state.Board;
            var cells = All(board).Where(c => board[c].CanMatch && !board[c].IsSpecial && !board.IsLocked(c)).ToList();
            rng.Shuffle(cells);
            return cells.Take(count).ToList();
        }

        private static int Value(LevelState state, Cell c)
        {
            var board = state.Board;
            if (!board.IsPlayable(c))
            {
                return 0;
            }

            var cover = board.CoverAt(c);
            if (cover.oil)
            {
                return state.Needed(GoalKind.Oil) > 0 ? 3 : 2; // oil left alone spreads
            }

            if (cover.crate > 0)
            {
                return state.Needed(GoalKind.Crates) > 0 ? 3 : 1;
            }

            int value = board[c].IsEmpty ? 0 : state.NeededOf(board[c].color) > 0 ? 2 : 1;
            return value + (cover.ice && state.Needed(GoalKind.Ice) > 0 ? 2 : 0);
        }

        private static IEnumerable<Cell> All(Board board)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.IsPlayable(x, y))
                    {
                        yield return new Cell(x, y);
                    }
                }
            }
        }
    }
}
