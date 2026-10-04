using System;
using System.Collections.Generic;
using System.Linq;
using Template.Core.Random;

namespace CadenceClub.Core
{
    public enum GoalKind
    {
        /// <summary>Clear <c>count</c> pieces of <c>color</c>.</summary>
        Collect,

        /// <summary>Break <c>count</c> crates.</summary>
        Crates,

        /// <summary>Break <c>count</c> ice tiles.</summary>
        Ice,

        /// <summary>Clear <c>count</c> oil (it spreads after every move that clears none).</summary>
        Oil,

        /// <summary>Bring <c>count</c> trophies down to the bottom; one drops in at the top while more are needed.</summary>
        Trophies,
    }

    [Serializable]
    public struct Goal
    {
        public GoalKind kind;
        public int color;
        public int count;
    }

    /// <summary>What a level is: board shape, colours, move limit, goals, seed. Saved as one JSON file per level.</summary>
    [Serializable]
    public sealed class LevelDef
    {
        public int id;

        /// <summary>Rows top to bottom: '.' playable, '#' hole, '1'/'2' crate (hits), 'o' oil, 'i' ice, 'l' chained piece.</summary>
        public string[] shape;

        public int colors = 4;
        public int moves = 25;
        public Goal[] goals = Array.Empty<Goal>();
        public ulong seed = 1;

        public int Width => shape[0].Length;
        public int Height => shape.Length;

        public bool[] Playable()
        {
            var playable = new bool[Width * Height];
            for (int row = 0; row < Height; row++)
            {
                int y = Height - 1 - row;
                for (int x = 0; x < Width; x++)
                {
                    playable[y * Width + x] = shape[row][x] != '#';
                }
            }

            return playable;
        }

        /// <summary>A plain rectangle, for tests and quick prototypes.</summary>
        public static LevelDef Rectangle(int width, int height, int colors, int moves, params Goal[] goals)
        {
            var rows = new string[height];
            for (int i = 0; i < height; i++)
            {
                rows[i] = new string('.', width);
            }

            return new LevelDef { shape = rows, colors = colors, moves = moves, goals = goals };
        }

        public static Goal Collect(int color, int count) => new Goal { kind = GoalKind.Collect, color = color, count = count };

        public static Goal BreakCrates(int count) => new Goal { kind = GoalKind.Crates, color = Piece.NoColor, count = count };

        public static Goal ClearIce(int count) => new Goal { kind = GoalKind.Ice, color = Piece.NoColor, count = count };

        public static Goal ClearOil(int count) => new Goal { kind = GoalKind.Oil, color = Piece.NoColor, count = count };

        public static Goal BringTrophies(int count) => new Goal { kind = GoalKind.Trophies, color = Piece.NoColor, count = count };

        /// <summary>Shallow copy: the shape and goals arrays are shared (nothing mutates them).</summary>
        public LevelDef Clone() => (LevelDef)MemberwiseClone();

        /// <summary>What would make the level broken or unwinnable; empty when it's fine. The editor and tests use it.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (shape == null || shape.Length == 0 || string.IsNullOrEmpty(shape[0]))
            {
                problems.Add("the level has no shape");
                return problems;
            }

            int cells = 0;
            int ice = 0;
            int crates = 0;
            int oil = 0;
            for (int row = 0; row < shape.Length; row++)
            {
                if (shape[row].Length != Width)
                {
                    problems.Add($"row {row + 1} is {shape[row].Length} wide; row 1 is {Width}");
                }

                foreach (char c in shape[row])
                {
                    if ("#.12oil".IndexOf(c) < 0)
                    {
                        problems.Add($"row {row + 1} has '{c}' (use # . 1 2 o i l)");
                    }

                    cells += c == '.' || c == 'i' || c == 'l' ? 1 : 0;
                    ice += c == 'i' ? 1 : 0;
                    crates += c == '1' || c == '2' ? 1 : 0;
                    oil += c == 'o' ? 1 : 0;
                }
            }

            if (colors < 3 || colors > 6)
            {
                problems.Add($"colours must be 3–6, not {colors}");
            }

            if (moves < 1)
            {
                problems.Add("moves must be at least 1");
            }

            if (cells < 9)
            {
                problems.Add($"only {cells} cells hold pieces; a board needs at least 9");
            }

            if (goals == null || goals.Length == 0)
            {
                problems.Add("the level has no goals");
                return problems;
            }

            for (int g = 0; g < goals.Length; g++)
            {
                var goal = goals[g];
                if (goal.count < 1)
                {
                    problems.Add($"goal {g + 1} asks for {goal.count}");
                }
                else if (goal.kind == GoalKind.Collect && (goal.color < 0 || goal.color >= colors))
                {
                    problems.Add($"goal {g + 1} collects colour {goal.color}, but the level has colours 0–{colors - 1}");
                }
                else if (goal.kind == GoalKind.Ice && goal.count > ice)
                {
                    problems.Add($"goal {g + 1} wants {goal.count} ice; the board has {ice}");
                }
                else if (goal.kind == GoalKind.Crates && goal.count > crates)
                {
                    problems.Add($"goal {g + 1} wants {goal.count} crates; the board has {crates}");
                }
                else if (goal.kind == GoalKind.Oil && goal.count > oil)
                {
                    // Oil can grow, but a player who clears it all must have met the goal.
                    problems.Add($"goal {g + 1} wants {goal.count} oil; the board starts with {oil}");
                }
            }

            return problems;
        }
    }

    public enum LevelOutcome
    {
        Playing,
        Won,
        Lost,
    }

    /// <summary>One play of a level: the board, moves left, goal progress and the outcome. Pure C#, cloneable for the bot.</summary>
    public sealed class LevelState
    {
        private readonly SeededRandom _rng;
        private readonly MoveResolver _resolver;
        private readonly int[] _progress;
        private readonly List<RiderSlot> _squad;

        /// <param name="squad">Riders brought into the level, charged as given (empty in the game; tests may pre-charge).</param>
        /// <param name="boosters">Bought before the level: their specials go onto random plain pieces at the start.</param>
        public LevelState(LevelDef def, ulong? seed = null, IEnumerable<RiderSlot> squad = null, IEnumerable<BoosterDef> boosters = null)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            _rng = new SeededRandom(seed ?? def.seed);
            _resolver = new MoveResolver(def.colors, _rng);
            Board = BoardGenerator.Generate(def.Width, def.Height, def.Playable(), def.colors, _rng, def.shape);
            MovesLeft = def.moves;
            _progress = new int[def.goals.Length];
            _squad = squad?.Select(s => new RiderSlot(s.Def, s.Level, s.Charge)).ToList() ?? new List<RiderSlot>();
            foreach (var booster in boosters ?? Enumerable.Empty<BoosterDef>())
            {
                var cells = PowerTargets.PlainPieces(this, _rng, booster.count);
                for (int i = 0; i < cells.Count; i++)
                {
                    bool rocket = booster.special == Special.RocketH || booster.special == Special.RocketV;
                    var kind = rocket ? (i % 2 == 0 ? Special.RocketH : Special.RocketV) : booster.special;
                    Board[cells[i]] = Piece.Make(Board[cells[i]].color, kind);
                }
            }

            DropTrophy(null);
            if (!MoveFinder.HasMove(Board))
            {
                Shuffler.Shuffle(Board, _rng, def.colors);
            }
        }

        /// <summary>
        /// After a win, the moves left go off as a victory lap: up to <paramref name="max"/> plain pieces become rockets,
        /// then all of them fire and the board cascades. Show only: goals, moves and charge stay as they are.
        /// </summary>
        public void VictoryLap(List<BoardEvent> events, int max)
        {
            if (Outcome != LevelOutcome.Won || MovesLeft <= 0)
            {
                return;
            }

            var cells = PowerTargets.PlainPieces(this, _rng, Math.Min(MovesLeft, max));
            events.Add(new BoardEvent { type = BoardEventType.StepStarted, value = 0 });
            for (int i = 0; i < cells.Count; i++)
            {
                Board[cells[i]] = Piece.Make(Board[cells[i]].color, i % 2 == 0 ? Special.RocketH : Special.RocketV);
                events.Add(new BoardEvent { type = BoardEventType.SpecialCreated, a = cells[i], piece = Board[cells[i]] });
            }

            _resolver.ResolveCells(Board, cells, events);
        }

        /// <summary>"+N moves" after running out: the lost level carries on. False unless it was lost.</summary>
        public bool Continue(int moves)
        {
            if (Outcome != LevelOutcome.Lost || moves <= 0)
            {
                return false;
            }

            MovesLeft += moves;
            Outcome = LevelOutcome.Playing;
            return true;
        }

        private LevelState(LevelState other)
        {
            Def = other.Def;
            _rng = new SeededRandom(other._rng.Save());
            _resolver = new MoveResolver(Def.colors, _rng);
            Board = other.Board.Clone();
            MovesLeft = other.MovesLeft;
            MovesUsed = other.MovesUsed;
            Outcome = other.Outcome;
            _progress = (int[])other._progress.Clone();
            _squad = other._squad.Select(s => new RiderSlot(s.Def, s.Level, s.Charge)).ToList();
        }

        public IReadOnlyList<RiderSlot> Squad => _squad;

        public LevelDef Def { get; }
        public Board Board { get; }
        public int MovesLeft { get; private set; }
        public int MovesUsed { get; private set; }
        public LevelOutcome Outcome { get; private set; }

        public int Progress(int goal) => _progress[goal];

        public int Remaining(int goal) => Math.Max(0, Def.goals[goal].count - _progress[goal]);

        /// <summary>Remaining pieces of a colour still needed by Collect goals (0 if that colour isn't a goal).</summary>
        public int NeededOf(int color)
        {
            int needed = 0;
            for (int i = 0; i < Def.goals.Length; i++)
            {
                if (Def.goals[i].kind == GoalKind.Collect && Def.goals[i].color == color)
                {
                    needed += Remaining(i);
                }
            }

            return needed;
        }

        /// <summary>Remaining crates or ice still needed by goals of that kind.</summary>
        public int Needed(GoalKind kind)
        {
            int needed = 0;
            for (int i = 0; i < Def.goals.Length; i++)
            {
                if (Def.goals[i].kind == kind)
                {
                    needed += Remaining(i);
                }
            }

            return needed;
        }

        public LevelState Clone() => new LevelState(this);

        /// <summary>Plays one move. Returns false if the swap is invalid (no move used) or the level is over.</summary>
        public bool TryMove(Cell a, Cell b, List<BoardEvent> events)
        {
            if (Outcome != LevelOutcome.Playing)
            {
                return false;
            }

            int start = events.Count;
            if (!_resolver.TryMove(Board, a, b, events))
            {
                return false;
            }

            MovesLeft--;
            MovesUsed++;
            Absorb(events, start);
            if (AllGoalsMet())
            {
                Outcome = LevelOutcome.Won;
            }
            else if (MovesLeft <= 0)
            {
                Outcome = LevelOutcome.Lost;
            }

            AfterTurn(events, start, spreadOil: true);
            return true;
        }

        /// <summary>
        /// The board's own turn, as one extra step: oil spreads onto a neighbouring piece unless the move cleared some
        /// (a power isn't a turn, so it never spreads), and a trophy drops in at the top while the goal needs one and none
        /// is on the board.
        /// </summary>
        private void AfterTurn(List<BoardEvent> events, int start, bool spreadOil)
        {
            if (Outcome != LevelOutcome.Playing)
            {
                return;
            }

            bool oilCleared = false;
            for (int i = start; i < events.Count; i++)
            {
                oilCleared |= events[i].type == BoardEventType.OilCleared;
            }

            var extra = new List<BoardEvent>();
            if (spreadOil && !oilCleared)
            {
                SpreadOil(extra);
            }

            DropTrophy(extra);
            if (extra.Count == 0)
            {
                return;
            }

            events.Add(new BoardEvent { type = BoardEventType.StepStarted, value = 0 });
            events.AddRange(extra);
            if (!MoveFinder.HasMove(Board))
            {
                Shuffler.Shuffle(Board, _rng, Def.colors);
                events.Add(new BoardEvent { type = BoardEventType.Shuffled });
            }
        }

        /// <summary>One oil cell, picked at random, covers a plain neighbouring piece.</summary>
        private void SpreadOil(List<BoardEvent> events)
        {
            var options = new List<(Cell from, Cell to)>();
            for (int y = 0; y < Board.Height; y++)
            {
                for (int x = 0; x < Board.Width; x++)
                {
                    var from = new Cell(x, y);
                    if (!Board.HasOil(from))
                    {
                        continue;
                    }

                    foreach (var to in new[] { new Cell(x + 1, y), new Cell(x - 1, y), new Cell(x, y + 1), new Cell(x, y - 1) })
                    {
                        if (Board.IsPlayable(to) && Board.CoverAt(to).IsEmpty && Board[to].CanMatch && !Board[to].IsSpecial)
                        {
                            options.Add((from, to));
                        }
                    }
                }
            }

            if (options.Count == 0)
            {
                return;
            }

            var (source, target) = options[_rng.Range(0, options.Count)];
            var covered = Board[target];
            Board[target] = Piece.Empty;
            Board.SetCover(target, new Cover { crate = 1, oil = true });
            events.Add(new BoardEvent { type = BoardEventType.OilSpread, a = source, b = target, piece = covered });
        }

        /// <summary>While the trophy goal needs more and none is on the board, one replaces a plain piece in a top cell.</summary>
        private void DropTrophy(List<BoardEvent> events)
        {
            if (Needed(GoalKind.Trophies) == 0)
            {
                return;
            }

            var tops = new List<Cell>();
            for (int x = 0; x < Board.Width; x++)
            {
                for (int y = Board.Height - 1; y >= 0; y--)
                {
                    var cell = new Cell(x, y);
                    if (Board[cell].trophy)
                    {
                        return; // one at a time
                    }

                    if (!Board.IsPlayable(cell))
                    {
                        continue;
                    }

                    if (Board.CoverAt(cell).IsEmpty && Board[cell].CanMatch && !Board[cell].IsSpecial && tops.Count(c => c.x == x) == 0)
                    {
                        tops.Add(cell);
                    }
                }
            }

            if (tops.Count == 0)
            {
                return;
            }

            var at = tops[_rng.Range(0, tops.Count)];
            Board[at] = Piece.Trophy;
            events?.Add(new BoardEvent { type = BoardEventType.TrophyDropped, a = at, piece = Piece.Trophy });
        }

        /// <summary>Fires a full rider's power (no move used) and empties their charge. False if not ready or the level is over.</summary>
        public bool TryUsePower(int slot, List<BoardEvent> events)
        {
            if (Outcome != LevelOutcome.Playing || slot < 0 || slot >= _squad.Count || !_squad[slot].Full)
            {
                return false;
            }

            var rider = _squad[slot];
            rider.Charge = 0;
            int start = events.Count;
            switch (rider.Def.power)
            {
                case PowerKind.RowRockets:
                case PowerKind.ColumnRockets:
                    _resolver.ResolveCells(Board, PowerTargets.Lines(this, rider.Amount, rider.Def.power == PowerKind.RowRockets), events);
                    break;
                case PowerKind.BreakObstacles:
                    _resolver.ResolveCells(Board, PowerTargets.Obstacles(this, _rng, rider.Amount), events);
                    break;
                case PowerKind.MakeSpecials:
                    _resolver.MakeSpecials(Board, PowerTargets.PlainPieces(this, _rng, rider.Amount), events);
                    break;
                case PowerKind.ExtraMoves:
                    MovesLeft += rider.Amount;
                    break;
            }

            Absorb(events, start);
            if (AllGoalsMet())
            {
                Outcome = LevelOutcome.Won;
            }

            AfterTurn(events, start, spreadOil: false);
            return true;
        }

        /// <summary>Counts goal progress and rider charge from the events of one move or power.</summary>
        private void Absorb(List<BoardEvent> events, int start)
        {
            for (int i = start; i < events.Count; i++)
            {
                var e = events[i];
                if (e.type == BoardEventType.Cleared)
                {
                    Count(GoalKind.Collect, e.piece.color);
                    foreach (var rider in _squad)
                    {
                        if (rider.Def.color == e.piece.color && !rider.Full)
                        {
                            rider.Charge++;
                        }
                    }
                }
                else if (e.type == BoardEventType.CrateHit && e.value == 0)
                {
                    Count(GoalKind.Crates, Piece.NoColor);
                }
                else if (e.type == BoardEventType.IceBroken)
                {
                    Count(GoalKind.Ice, Piece.NoColor);
                }
                else if (e.type == BoardEventType.OilCleared)
                {
                    Count(GoalKind.Oil, Piece.NoColor);
                }
                else if (e.type == BoardEventType.TrophyCollected)
                {
                    Count(GoalKind.Trophies, Piece.NoColor);
                }
            }
        }

        private void Count(GoalKind kind, int color)
        {
            for (int g = 0; g < Def.goals.Length; g++)
            {
                var goal = Def.goals[g];
                bool counts = goal.kind == kind && (kind != GoalKind.Collect || goal.color == color);
                if (counts && _progress[g] < goal.count)
                {
                    _progress[g]++;
                }
            }
        }

        private bool AllGoalsMet()
        {
            for (int g = 0; g < Def.goals.Length; g++)
            {
                if (_progress[g] < Def.goals[g].count)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
