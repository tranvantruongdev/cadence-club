using System;
using System.Collections.Generic;
using Template.Core.Random;

namespace CadenceClub.Core
{
    public enum BotKind
    {
        /// <summary>Takes the move whose match helps the goals most, like a focused player.</summary>
        Greedy,

        /// <summary>Takes any valid move, like a casual player.</summary>
        Random,
    }

    /// <summary>
    /// Picks moves for simulation. The greedy bot scores each valid move by its first match only (one MatchFinder
    /// pass per candidate, no full resolve), so thousands of games run in seconds.
    /// </summary>
    public sealed class Bot
    {
        private readonly BotKind _kind;
        private readonly SeededRandom _rng;
        private readonly List<(Cell a, Cell b)> _best = new List<(Cell a, Cell b)>();

        public Bot(BotKind kind, SeededRandom rng)
        {
            _kind = kind;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public (Cell a, Cell b)? Choose(LevelState state)
        {
            var moves = MoveFinder.FindAll(state.Board);
            if (moves.Count == 0)
            {
                return null;
            }

            if (_kind == BotKind.Random)
            {
                return moves[_rng.Range(0, moves.Count)];
            }

            int bestScore = int.MinValue;
            _best.Clear();
            foreach (var move in moves)
            {
                int score = Score(state, move.a, move.b);
                if (score > bestScore)
                {
                    bestScore = score;
                    _best.Clear();
                }

                if (score == bestScore)
                {
                    _best.Add(move);
                }
            }

            return _best[_rng.Range(0, _best.Count)];
        }

        private static int Score(LevelState state, Cell a, Cell b)
        {
            var board = state.Board;
            var pa = board[a];
            var pb = board[b];
            if (pa.special == Special.Disco || pb.special == Special.Disco)
            {
                var other = pa.special == Special.Disco ? pb : pa;
                return 60 + 4 * state.NeededOf(other.color);
            }

            if (pa.IsSpecial && pb.IsSpecial)
            {
                return 50;
            }

            MoveResolver.Swap(board, a, b);
            var groups = MatchFinder.Find(board, a, b);
            int score = 0;
            bool wantIce = state.Needed(GoalKind.Ice) > 0;
            bool wantCrates = state.Needed(GoalKind.Crates) > 0;
            bool wantOil = state.Needed(GoalKind.Oil) > 0;
            bool wantTrophy = state.Needed(GoalKind.Trophies) > 0;
            var crates = new HashSet<Cell>();
            foreach (var g in groups)
            {
                foreach (var c in g.cells)
                {
                    var cover = board.CoverAt(c);
                    score += wantIce && cover.ice ? 5 : 0;
                    score += cover.chain ? 2 : 0; // frees a locked piece
                    if (wantCrates || wantOil)
                    {
                        foreach (var n in new[] { new Cell(c.x + 1, c.y), new Cell(c.x - 1, c.y), new Cell(c.x, c.y + 1), new Cell(c.x, c.y - 1) })
                        {
                            bool wanted = board.HasOil(n) ? wantOil : wantCrates;
                            score += wanted && board.HasCrate(n) && crates.Add(n) ? 5 : 0;
                        }
                    }

                    score += wantTrophy && TrophyAbove(board, c) ? 6 : 0; // the trophy falls a row
                }

                score += g.cells.Count + 3 * Math.Min(g.cells.Count, state.NeededOf(g.color));
                score += g.creates == Special.Disco ? 20
                    : g.creates == Special.Bomb ? 12
                    : g.creates == Special.RocketH || g.creates == Special.RocketV ? 10
                    : g.creates == Special.Glider ? 8
                    : 0;
                foreach (var c in g.cells)
                {
                    if (board[c].IsSpecial)
                    {
                        score += 8; // sets off a special already on the board
                    }
                }
            }

            MoveResolver.Swap(board, a, b);

            // Matches low on the board shake more pieces loose, so they cascade more.
            score += (board.Height - Math.Min(a.y, b.y)) / 3;
            return score;
        }

        private static bool TrophyAbove(Board board, Cell c)
        {
            for (int y = c.y + 1; y < board.Height; y++)
            {
                if (board[c.x, y].trophy)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public struct SimResult
    {
        public int runs;
        public int wins;
        public double averageMovesLeftOnWin;

        /// <summary>Average share of the goals completed, 0..1 (how close the losses came).</summary>
        public double averageGoalCompletion;

        public double WinRate => runs == 0 ? 0 : (double)wins / runs;

        public override string ToString() =>
            $"{wins}/{runs} won ({WinRate:P0}), {averageMovesLeftOnWin:0.0} moves left on wins, {averageGoalCompletion:P0} of goals on average";
    }

    /// <summary>
    /// Target greedy-bot win rates from the design plan (§4): a sawtooth. Easy first levels and an easy breather
    /// after each bump (1–6, 11, 16, 21, 26), a hard bump every 5th level from 10, a finale at 30, normal otherwise.
    /// </summary>
    public static class LevelBands
    {
        public static (double min, double max) For(int level) =>
            level >= 30 ? (0.35, 0.45)
            : level >= 10 && level % 5 == 0 ? (0.40, 0.55)
            : level <= 6 || level % 5 == 1 ? (0.90, 1.0)
            : (0.65, 0.80);
    }

    /// <summary>Monte Carlo difficulty check: plays a level many times with different seeds and reports the win rate.</summary>
    public static class LevelSimulator
    {
        /// <summary>
        /// How many moves each run took to win (int.MaxValue if it didn't within <paramref name="cap"/>). The bot
        /// never looks at moves left, so a run plays the same moves under any limit: one pass with a high cap gives
        /// the win rate for every move limit (<see cref="WinRate"/>, <see cref="MovesFor"/>).
        /// </summary>
        public static int[] MovesToWin(LevelDef def, int runs, BotKind kind, ulong firstSeed = 1, int cap = 100)
        {
            var open = def.Clone();
            open.moves = cap;
            var needed = new int[runs];
            for (int i = 0; i < runs; i++)
            {
                var state = Play(open, firstSeed + (ulong)i, kind);
                needed[i] = state.Outcome == LevelOutcome.Won ? state.MovesUsed : int.MaxValue;
            }

            return needed;
        }

        public static double WinRate(int[] needed, int moves)
        {
            int wins = 0;
            foreach (int n in needed)
            {
                wins += n <= moves ? 1 : 0;
            }

            return needed.Length == 0 ? 0 : (double)wins / needed.Length;
        }

        /// <summary>
        /// The move limit whose win rate lies in the band and closest to its target: the middle, or 99% for an easy
        /// band (the greedy bot plays better than a beginner, so easy levels get slack). -1 if no limit fits.
        /// </summary>
        public static int FitMoves(int[] needed, (double min, double max) band)
        {
            double target = band.max >= 1.0 ? 0.99 : (band.min + band.max) / 2;
            int longest = 0;
            foreach (int n in needed)
            {
                longest = n == int.MaxValue ? longest : Math.Max(longest, n);
            }

            int best = -1;
            double bestGap = double.MaxValue;
            for (int moves = 1; moves <= longest; moves++)
            {
                double rate = WinRate(needed, moves);
                double gap = Math.Abs(rate - target);
                if (rate >= band.min && rate <= band.max && gap < bestGap)
                {
                    best = moves;
                    bestGap = gap;
                }
            }

            return best;
        }

        public static SimResult Run(LevelDef def, int runs, BotKind kind, ulong firstSeed = 1)
        {
            var result = new SimResult { runs = runs };
            long movesLeft = 0;
            double completion = 0;
            for (int i = 0; i < runs; i++)
            {
                var state = Play(def, firstSeed + (ulong)i, kind);
                if (state.Outcome == LevelOutcome.Won)
                {
                    result.wins++;
                    movesLeft += state.MovesLeft;
                }

                completion += Completion(state);
            }

            result.averageMovesLeftOnWin = result.wins > 0 ? (double)movesLeft / result.wins : 0;
            result.averageGoalCompletion = runs > 0 ? completion / runs : 0;
            return result;
        }

        private static LevelState Play(LevelDef def, ulong seed, BotKind kind)
        {
            var state = new LevelState(def, seed);
            var bot = new Bot(kind, new SeededRandom(seed, 7UL));
            var events = new List<BoardEvent>();
            while (state.Outcome == LevelOutcome.Playing)
            {
                var move = bot.Choose(state);
                events.Clear();
                if (!move.HasValue || !state.TryMove(move.Value.a, move.Value.b, events))
                {
                    break; // can't happen: the resolver always leaves a valid move
                }
            }

            return state;
        }

        private static double Completion(LevelState state)
        {
            int needed = 0;
            int done = 0;
            for (int g = 0; g < state.Def.goals.Length; g++)
            {
                needed += state.Def.goals[g].count;
                done += state.Progress(g);
            }

            return needed == 0 ? 1 : (double)done / needed;
        }
    }
}
