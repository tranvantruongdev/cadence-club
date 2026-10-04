using System;
using System.Collections.Generic;
using Template.Core.Random;

namespace CadenceClub.Core
{
    public enum BoardEventType
    {
        /// <summary>a and b swapped places.</summary>
        Swapped,

        /// <summary>a and b swapped and swapped back: no match.</summary>
        SwapRejected,

        /// <summary>A new resolution step starts (0 = the player's move, then each cascade); value = step.</summary>
        StepStarted,

        /// <summary>The piece at a was cleared; piece = what it was (for goals).</summary>
        Cleared,

        /// <summary>The special at a went off.</summary>
        SpecialActivated,

        /// <summary>A new special appeared at a.</summary>
        SpecialCreated,

        /// <summary>piece fell from a to b.</summary>
        Fell,

        /// <summary>piece entered at a; b is where it starts above the board (for the drop animation).</summary>
        Spawned,

        /// <summary>No moves were left, so the board was shuffled; the board now holds the new layout.</summary>
        Shuffled,
    }

    public struct BoardEvent
    {
        public BoardEventType type;
        public Cell a;
        public Cell b;
        public Piece piece;
        public int value;

        public override string ToString() => $"{type} {a} {b} {piece} {value}";
    }

    /// <summary>Pieces fall straight down into empty cells, passing over holes.</summary>
    public static class Gravity
    {
        public static void Apply(Board board, List<BoardEvent> events)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int write = NextPlayable(board, x, 0);
                for (int y = 0; y < board.Height; y++)
                {
                    if (!board.IsPlayable(x, y) || board[x, y].IsEmpty)
                    {
                        continue;
                    }

                    if (y != write)
                    {
                        var piece = board[x, y];
                        board[x, write] = piece;
                        board[x, y] = Piece.Empty;
                        events?.Add(new BoardEvent { type = BoardEventType.Fell, a = new Cell(x, y), b = new Cell(x, write), piece = piece });
                    }

                    write = NextPlayable(board, x, write + 1);
                }
            }
        }

        private static int NextPlayable(Board board, int x, int y)
        {
            while (y < board.Height && !board.IsPlayable(x, y))
            {
                y++;
            }

            return y;
        }
    }

    /// <summary>Fills empty cells from the top with seeded random colours.</summary>
    public static class Refill
    {
        public static void Apply(Board board, SeededRandom rng, int colors, List<BoardEvent> events)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int dropped = 0;
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.IsPlayable(x, y) && board[x, y].IsEmpty)
                    {
                        var piece = Piece.Normal(rng.Range(0, colors));
                        board[x, y] = piece;
                        events?.Add(new BoardEvent
                        {
                            type = BoardEventType.Spawned,
                            a = new Cell(x, y),
                            b = new Cell(x, board.Height + dropped),
                            piece = piece,
                        });
                        dropped++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resolves a whole move in pure C#: swap, match, clear (setting off specials), fall, refill, repeat until
    /// the board is stable, then shuffle if no move is left. Returns the events in order; the view replays them.
    /// </summary>
    public sealed class MoveResolver
    {
        public const int MaxSteps = 64;
        private readonly SeededRandom _rng;
        private readonly int _colors;

        public MoveResolver(int colors, SeededRandom rng)
        {
            _colors = colors;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public int Colors => _colors;

        /// <summary>Returns false (board unchanged apart from a rejected swap) when the move makes no match.</summary>
        public bool TryMove(Board board, Cell a, Cell b, List<BoardEvent> events)
        {
            if (!MoveFinder.CanSwap(board, a, b))
            {
                return false;
            }

            var pa = board[a];
            var pb = board[b];
            Swap(board, a, b);
            events.Add(new BoardEvent { type = BoardEventType.Swapped, a = a, b = b });

            var clears = new List<Cell>();
            var creations = new List<MatchGroup>();
            var noBlast = new HashSet<Cell>(); // specials whose effect the swap already decided
            if (pa.special == Special.Disco || pb.special == Special.Disco)
            {
                // A disco swapped with anything clears every piece of the other piece's colour (two discos: everything).
                var discoCell = pa.special == Special.Disco ? b : a; // after the swap the disco sits where the other piece was
                var otherCell = pa.special == Special.Disco ? a : b;
                var other = pa.special == Special.Disco ? pb : pa;
                clears.Add(discoCell);
                noBlast.Add(discoCell);
                if (other.special == Special.Disco)
                {
                    clears.Add(otherCell);
                    noBlast.Add(otherCell);
                    AddAll(board, clears, _ => true);
                }
                else
                {
                    AddAll(board, clears, p => p.color == other.color);
                }
            }
            else if (pa.IsSpecial && pb.IsSpecial)
            {
                // Two specials swapped: both go off. (Combos come next.)
                clears.Add(a);
                clears.Add(b);
            }
            else
            {
                var groups = MatchFinder.Find(board, a, b);
                if (groups.Count == 0)
                {
                    Swap(board, a, b);
                    events.Add(new BoardEvent { type = BoardEventType.SwapRejected, a = a, b = b });
                    return false;
                }

                Collect(groups, clears, creations);
            }

            Resolve(board, clears, creations, noBlast, events);
            return true;
        }

        /// <summary>Clears the given cells and creations, then cascades until stable.</summary>
        private void Resolve(Board board, List<Cell> clears, List<MatchGroup> creations, HashSet<Cell> noBlast, List<BoardEvent> events)
        {
            for (int step = 0; step < MaxSteps; step++)
            {
                if (step > 0)
                {
                    clears.Clear();
                    creations.Clear();
                    noBlast.Clear();
                    Collect(MatchFinder.Find(board), clears, creations);
                }

                if (clears.Count == 0)
                {
                    break;
                }

                events.Add(new BoardEvent { type = BoardEventType.StepStarted, value = step });
                ClearAndActivate(board, clears, creations, noBlast, events);
                Gravity.Apply(board, events);
                Refill.Apply(board, _rng, _colors, events);
            }

            if (!MoveFinder.HasMove(board))
            {
                Shuffler.Shuffle(board, _rng, _colors);
                events.Add(new BoardEvent { type = BoardEventType.Shuffled });
            }
        }

        private static void Collect(List<MatchGroup> groups, List<Cell> clears, List<MatchGroup> creations)
        {
            foreach (var group in groups)
            {
                clears.AddRange(group.cells);
                if (group.creates != Special.None)
                {
                    creations.Add(group);
                }
            }
        }

        private void ClearAndActivate(Board board, List<Cell> initial, List<MatchGroup> creations, HashSet<Cell> noBlast,
            List<BoardEvent> events)
        {
            var queue = new Queue<Cell>(initial);
            var done = new HashSet<Cell>();
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (!board.IsPlayable(cell) || !done.Add(cell))
                {
                    continue;
                }

                var piece = board[cell];
                if (piece.IsEmpty)
                {
                    continue;
                }

                board[cell] = Piece.Empty;
                events.Add(new BoardEvent { type = BoardEventType.Cleared, a = cell, piece = piece });
                if (!piece.IsSpecial)
                {
                    continue;
                }

                events.Add(new BoardEvent { type = BoardEventType.SpecialActivated, a = cell, piece = piece });
                if (noBlast.Contains(cell))
                {
                    continue;
                }

                foreach (var hit in Blast(board, cell, piece, done))
                {
                    queue.Enqueue(hit);
                }
            }

            foreach (var group in creations)
            {
                var special = Piece.Make(group.color, group.creates);
                board[group.createAt] = special;
                events.Add(new BoardEvent { type = BoardEventType.SpecialCreated, a = group.createAt, piece = special });
            }
        }

        /// <summary>Cells a special clears when it goes off.</summary>
        private IEnumerable<Cell> Blast(Board board, Cell at, Piece piece, HashSet<Cell> done)
        {
            switch (piece.special)
            {
                case Special.RocketH:
                    for (int x = 0; x < board.Width; x++)
                    {
                        yield return new Cell(x, at.y);
                    }

                    break;
                case Special.RocketV:
                    for (int y = 0; y < board.Height; y++)
                    {
                        yield return new Cell(at.x, y);
                    }

                    break;
                case Special.Bomb:
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            yield return new Cell(at.x + dx, at.y + dy);
                        }
                    }

                    break;
                case Special.Glider:
                    // Hits its four neighbours, then flies to one more piece somewhere on the board.
                    yield return new Cell(at.x + 1, at.y);
                    yield return new Cell(at.x - 1, at.y);
                    yield return new Cell(at.x, at.y + 1);
                    yield return new Cell(at.x, at.y - 1);
                    var target = PickTarget(board, done, at);
                    if (target.HasValue)
                    {
                        yield return target.Value;
                    }

                    break;
                case Special.Disco:
                    // Set off by a blast: takes the most common colour on the board.
                    int color = MostCommonColor(board);
                    for (int y = 0; y < board.Height; y++)
                    {
                        for (int x = 0; x < board.Width; x++)
                        {
                            if (color >= 0 && board[x, y].color == color)
                            {
                                yield return new Cell(x, y);
                            }
                        }
                    }

                    break;
            }
        }

        private Cell? PickTarget(Board board, HashSet<Cell> done, Cell from)
        {
            var candidates = new List<Cell>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var c = new Cell(x, y);
                    if (board.IsPlayable(c) && !board[c].IsEmpty && !done.Contains(c) && !c.IsAdjacentTo(from))
                    {
                        candidates.Add(c);
                    }
                }
            }

            return candidates.Count > 0 ? candidates[_rng.Range(0, candidates.Count)] : (Cell?)null;
        }

        private static int MostCommonColor(Board board)
        {
            var counts = new Dictionary<int, int>();
            int best = -1;
            int bestCount = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    int c = board[x, y].color;
                    if (c < 0)
                    {
                        continue;
                    }

                    counts.TryGetValue(c, out int n);
                    counts[c] = ++n;
                    if (n > bestCount || (n == bestCount && c < best))
                    {
                        bestCount = n;
                        best = c;
                    }
                }
            }

            return best;
        }

        private static void AddAll(Board board, List<Cell> cells, Func<Piece, bool> match)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.IsPlayable(x, y) && !board[x, y].IsEmpty && match(board[x, y]))
                    {
                        cells.Add(new Cell(x, y));
                    }
                }
            }
        }

        /// <summary>Swaps two pieces with no rules applied (for move checks, tests and the view).</summary>
        public static void Swap(Board board, Cell a, Cell b)
        {
            var t = board[a];
            board[a] = board[b];
            board[b] = t;
        }
    }

    /// <summary>Which swaps are legal and useful: for hints, "no moves left" and the bot.</summary>
    public static class MoveFinder
    {
        public static bool CanSwap(Board board, Cell a, Cell b) =>
            a.IsAdjacentTo(b) && board.IsPlayable(a) && board.IsPlayable(b) && !board[a].IsEmpty && !board[b].IsEmpty;

        /// <summary>A swap counts if it makes a match, or involves a disco, or swaps two specials.</summary>
        public static bool IsValid(Board board, Cell a, Cell b)
        {
            if (!CanSwap(board, a, b))
            {
                return false;
            }

            var pa = board[a];
            var pb = board[b];
            if (pa.special == Special.Disco || pb.special == Special.Disco || (pa.IsSpecial && pb.IsSpecial))
            {
                return true;
            }

            MoveResolver.Swap(board, a, b);
            bool matches = MatchFinder.Find(board).Count > 0;
            MoveResolver.Swap(board, a, b);
            return matches;
        }

        public static List<(Cell a, Cell b)> FindAll(Board board)
        {
            var moves = new List<(Cell, Cell)>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var c = new Cell(x, y);
                    var right = new Cell(x + 1, y);
                    var up = new Cell(x, y + 1);
                    if (IsValid(board, c, right))
                    {
                        moves.Add((c, right));
                    }

                    if (IsValid(board, c, up))
                    {
                        moves.Add((c, up));
                    }
                }
            }

            return moves;
        }

        public static bool HasMove(Board board)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var c = new Cell(x, y);
                    if (IsValid(board, c, new Cell(x + 1, y)) || IsValid(board, c, new Cell(x, y + 1)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>Rearranges the pieces until there is no match on the board and at least one move.</summary>
    public static class Shuffler
    {
        public static void Shuffle(Board board, SeededRandom rng, int colors)
        {
            var cells = new List<Cell>();
            var pieces = new List<Piece>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.IsPlayable(x, y) && !board[x, y].IsEmpty)
                    {
                        cells.Add(new Cell(x, y));
                        pieces.Add(board[x, y]);
                    }
                }
            }

            for (int attempt = 0; attempt < 200; attempt++)
            {
                rng.Shuffle(pieces);
                for (int i = 0; i < cells.Count; i++)
                {
                    board[cells[i]] = pieces[i];
                }

                if (MatchFinder.Find(board).Count == 0 && MoveFinder.HasMove(board))
                {
                    return;
                }
            }

            // Too few pieces or colours to rearrange: deal fresh colours instead, keeping the specials.
            BoardGenerator.Fill(board, rng, colors, keepSpecials: true);
        }
    }

    /// <summary>Deals a starting board: no matches, at least one move, same seed → same board.</summary>
    public static class BoardGenerator
    {
        public static Board Generate(int width, int height, bool[] playable, int colors, SeededRandom rng)
        {
            var board = new Board(width, height, playable);
            Fill(board, rng, colors, keepSpecials: false);
            return board;
        }

        public static void Fill(Board board, SeededRandom rng, int colors, bool keepSpecials)
        {
            if (colors < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(colors), "need at least 3 colours to avoid starting matches");
            }

            var allowed = new List<int>(colors);
            for (int attempt = 0; attempt < 100; attempt++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    for (int x = 0; x < board.Width; x++)
                    {
                        if (!board.IsPlayable(x, y) || (keepSpecials && board[x, y].IsSpecial))
                        {
                            continue;
                        }

                        allowed.Clear();
                        for (int c = 0; c < colors; c++)
                        {
                            if (!WouldMatch(board, x, y, c))
                            {
                                allowed.Add(c);
                            }
                        }

                        board[x, y] = Piece.Normal(allowed.Count > 0 ? allowed[rng.Range(0, allowed.Count)] : rng.Range(0, colors));
                    }
                }

                if (MatchFinder.Find(board).Count == 0 && MoveFinder.HasMove(board))
                {
                    return;
                }
            }
        }

        /// <summary>Would colour c at (x, y) complete a line of 3 or a 2×2 with cells already dealt (left and below)?</summary>
        private static bool WouldMatch(Board board, int x, int y, int c)
        {
            bool Same(int px, int py) => board.IsPlayable(px, py) && board[px, py].CanMatch && board[px, py].color == c;
            return (Same(x - 1, y) && Same(x - 2, y))
                   || (Same(x, y - 1) && Same(x, y - 2))
                   || (Same(x - 1, y) && Same(x, y - 1) && Same(x - 1, y - 1));
        }
    }
}
