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

        /// <summary>The crate at a took a hit; value = hits left (0 = broken and gone).</summary>
        CrateHit,

        /// <summary>The ice under a broke (its piece was cleared).</summary>
        IceBroken,

        /// <summary>The chain at a broke; its piece stays, now free.</summary>
        ChainBroken,

        /// <summary>The oil at a was cleared (one hit).</summary>
        OilCleared,

        /// <summary>Oil spread from a onto b, covering the piece there (piece = what it covered).</summary>
        OilSpread,

        /// <summary>A trophy appeared at a, in place of the piece there (piece = the trophy).</summary>
        TrophyDropped,

        /// <summary>The trophy at a reached the bottom and left the board (piece = the trophy).</summary>
        TrophyCollected,
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

    /// <summary>
    /// Settles the board after clears. Pieces fall straight down, passing over holes, and land on crates, chained
    /// pieces or the bottom; empty cells with an open way to the top are refilled with seeded random colours; an
    /// empty cell under a crate or chain takes a piece sliding in diagonally from above-left or above-right.
    /// Repeats until nothing moves, then reports each piece ONCE: Fell from where it started to where it ended, or
    /// Spawned where it ended, so the view moves every piece with a single tween.
    /// </summary>
    public static class Gravity
    {
        /// <summary>Falls and slides only, no refill (for tests).</summary>
        public static void Apply(Board board, List<BoardEvent> events) => Settle(board, null, 0, events);

        public static void Settle(Board board, SeededRandom rng, int colors, List<BoardEvent> events)
        {
            int w = board.Width;
            int h = board.Height;
            var start = new Cell[w * h]; // where the piece now in each cell started (or entered, if spawned)
            var spawned = new bool[w * h];
            var dropped = new int[w]; // pieces that entered each column, to stack their entry points above the board
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    start[y * w + x] = new Cell(x, y);
                }
            }

            void Move(int fx, int fy, int tx, int ty)
            {
                board[tx, ty] = board[fx, fy];
                board[fx, fy] = Piece.Empty;
                start[ty * w + tx] = start[fy * w + fx];
                spawned[ty * w + tx] = spawned[fy * w + fx];
            }

            bool Free(int x, int y) => board.IsPlayable(x, y) && !board.IsFixed(x, y) && board[x, y].IsEmpty;
            bool Movable(int x, int y) => board.IsPlayable(x, y) && !board.IsFixed(x, y) && !board[x, y].IsEmpty;

            // Nothing fixed between (x, y) and the top of the board, so new pieces can drop in.
            bool OpenAbove(int x, int y)
            {
                for (int yy = y + 1; yy < h; yy++)
                {
                    if (board.IsPlayable(x, yy) && board.IsFixed(x, yy))
                    {
                        return false;
                    }
                }

                return true;
            }

            for (int guard = 0; guard < w * h * h; guard++)
            {
                // Straight down: each free cell takes the nearest piece above it, stopping at anything fixed.
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        if (!Free(x, y))
                        {
                            continue;
                        }

                        for (int yy = y + 1; yy < h; yy++)
                        {
                            if (!board.IsPlayable(x, yy))
                            {
                                continue;
                            }

                            if (board.IsFixed(x, yy))
                            {
                                break;
                            }

                            if (!board[x, yy].IsEmpty)
                            {
                                Move(x, yy, x, y);
                                break;
                            }
                        }
                    }
                }

                // New pieces into free cells open to the top.
                if (rng != null)
                {
                    for (int x = 0; x < w; x++)
                    {
                        for (int y = 0; y < h; y++)
                        {
                            if (Free(x, y) && OpenAbove(x, y))
                            {
                                board[x, y] = Piece.Normal(rng.Range(0, colors));
                                start[y * w + x] = new Cell(x, h + dropped[x]++);
                                spawned[y * w + x] = true;
                            }
                        }
                    }
                }

                // One diagonal slide into the lowest free cell that nothing can reach from straight above.
                bool slid = false;
                for (int y = 0; y < h && !slid; y++)
                {
                    for (int x = 0; x < w && !slid; x++)
                    {
                        if (!Free(x, y) || OpenAbove(x, y))
                        {
                            continue;
                        }

                        foreach (int sx in new[] { x - 1, x + 1 })
                        {
                            if (Movable(sx, y + 1))
                            {
                                Move(sx, y + 1, x, y);
                                slid = true;
                                break;
                            }
                        }
                    }
                }

                if (!slid)
                {
                    break;
                }
            }

            if (events == null)
            {
                return;
            }

            // Bottom rows first: every move goes down, so a cell's old piece has always left before a new one arrives.
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var here = new Cell(x, y);
                    if (!board.IsPlayable(here) || board[here].IsEmpty)
                    {
                        continue;
                    }

                    if (spawned[i])
                    {
                        events.Add(new BoardEvent { type = BoardEventType.Spawned, a = here, b = start[i], piece = board[here] });
                    }
                    else if (!start[i].Equals(here))
                    {
                        events.Add(new BoardEvent { type = BoardEventType.Fell, a = start[i], b = here, piece = board[here] });
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
            if (DiscoOnTrophy(pa, pb))
            {
                return false; // a trophy has no colour for the disco to take
            }

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
                    if (other.IsSpecial)
                    {
                        // Disco + special: every piece of that colour becomes the special, then they all go off.
                        ConvertColor(board, other.color, other.special);
                    }

                    AddAll(board, clears, p => p.color == other.color);
                }
            }
            else if (pa.IsSpecial && pb.IsSpecial)
            {
                ApplyCombo(board, a, b, pa, pb, clears, noBlast);
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

        /// <summary>
        /// Two coloured specials swapped together make one bigger effect, centred where the moved piece landed (b):
        /// rocket + rocket = cross · rocket + bomb = 3-wide cross · bomb + bomb = 5×5 ·
        /// glider + special = the glider carries the special to a target · glider + glider = three gliders.
        /// (Disco combos are handled with the other disco swaps.)
        /// </summary>
        private void ApplyCombo(Board board, Cell a, Cell b, Piece pa, Piece pb, List<Cell> clears, HashSet<Cell> noBlast)
        {
            clears.Add(a);
            clears.Add(b);
            noBlast.Add(a); // the combo replaces their separate effects
            noBlast.Add(b);
            var sa = pa.special;
            var sb = pb.special;
            if (IsRocket(sa) && IsRocket(sb))
            {
                AddCross(board, clears, b, 0);
            }
            else if ((IsRocket(sa) && sb == Special.Bomb) || (sa == Special.Bomb && IsRocket(sb)))
            {
                AddCross(board, clears, b, 1);
            }
            else if (sa == Special.Bomb && sb == Special.Bomb)
            {
                AddSquare(board, clears, b, 2);
            }
            else if (sa == Special.Glider && sb == Special.Glider)
            {
                for (int i = 0; i < 3; i++)
                {
                    var target = PickTarget(board, new HashSet<Cell>(clears), b);
                    if (target.HasValue)
                    {
                        clears.Add(target.Value);
                    }
                }
            }
            else
            {
                // Glider + rocket or bomb: the glider flies off and the other special goes off where it lands.
                var carried = sa == Special.Glider ? pb : pa;
                var target = PickTarget(board, new HashSet<Cell>(clears), b, crates: false); // needs a piece to land on
                if (target.HasValue)
                {
                    var landed = board[target.Value];
                    board[target.Value] = Piece.Make(landed.color >= 0 ? landed.color : carried.color, carried.special);
                    clears.Add(target.Value);
                }
            }
        }

        private static bool IsRocket(Special s) => s == Special.RocketH || s == Special.RocketV;

        internal static bool DiscoOnTrophy(Piece pa, Piece pb) => (pa.special == Special.Disco && pb.trophy) || (pb.special == Special.Disco && pa.trophy);

        /// <summary>Rows and columns through the centre, (2·half + 1) wide.</summary>
        private static void AddCross(Board board, List<Cell> clears, Cell at, int half)
        {
            for (int d = -half; d <= half; d++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    clears.Add(new Cell(x, at.y + d));
                }

                for (int y = 0; y < board.Height; y++)
                {
                    clears.Add(new Cell(at.x + d, y));
                }
            }
        }

        private static void AddSquare(Board board, List<Cell> clears, Cell at, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    clears.Add(new Cell(at.x + dx, at.y + dy));
                }
            }
        }

        /// <summary>Turns every plain piece of a colour into a special (rockets alternate direction).</summary>
        private static void ConvertColor(Board board, int color, Special special)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = board[x, y];
                    if (board.IsPlayable(x, y) && p.color == color && !p.IsSpecial)
                    {
                        var kind = IsRocket(special) ? ((x + y) % 2 == 0 ? Special.RocketH : Special.RocketV) : special;
                        board[x, y] = Piece.Make(color, kind);
                    }
                }
            }
        }

        /// <summary>Clears the given cells as one step (specials there go off), then cascades like a move. For rider powers.</summary>
        public void ResolveCells(Board board, IEnumerable<Cell> cells, List<BoardEvent> events) =>
            Resolve(board, new List<Cell>(cells), new List<MatchGroup>(), new HashSet<Cell>(), events);

        /// <summary>Turns the given plain pieces into random rockets and bombs of their colour, as one step. For rider powers.</summary>
        public void MakeSpecials(Board board, IEnumerable<Cell> cells, List<BoardEvent> events)
        {
            events.Add(new BoardEvent { type = BoardEventType.StepStarted, value = 0 });
            var kinds = new[] { Special.RocketH, Special.RocketV, Special.Bomb };
            foreach (var cell in cells)
            {
                var special = Piece.Make(board[cell].color, kinds[_rng.Range(0, kinds.Length)]);
                board[cell] = special;
                events.Add(new BoardEvent { type = BoardEventType.SpecialCreated, a = cell, piece = special });
            }
        }

        /// <summary>
        /// Clears the given cells and creations, then cascades until stable. A trophy that has reached the bottom of its
        /// column leaves at the start of the next step, with that step's matches, so each step settles once.
        /// </summary>
        private void Resolve(Board board, List<Cell> clears, List<MatchGroup> creations, HashSet<Cell> noBlast, List<BoardEvent> events)
        {
            for (int step = 0; step < MaxSteps; step++)
            {
                var trophies = BottomTrophies(board);
                if (step > 0)
                {
                    clears.Clear();
                    creations.Clear();
                    noBlast.Clear();
                    Collect(MatchFinder.Find(board), clears, creations);
                }

                if (clears.Count == 0 && trophies.Count == 0)
                {
                    break;
                }

                events.Add(new BoardEvent { type = BoardEventType.StepStarted, value = step });
                foreach (var cell in trophies)
                {
                    board[cell] = Piece.Empty;
                    events.Add(new BoardEvent { type = BoardEventType.TrophyCollected, a = cell, piece = Piece.Trophy });
                }

                ClearAndActivate(board, clears, creations, noBlast, events);
                Gravity.Settle(board, _rng, _colors, events);
            }

            if (!MoveFinder.HasMove(board))
            {
                Shuffler.Shuffle(board, _rng, _colors);
                events.Add(new BoardEvent { type = BoardEventType.Shuffled });
            }
        }

        /// <summary>Trophies with no playable cell below them in their column: they have arrived.</summary>
        public static List<Cell> BottomTrophies(Board board)
        {
            var cells = new List<Cell>();
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.IsPlayable(x, y))
                    {
                        if (board[x, y].trophy)
                        {
                            cells.Add(new Cell(x, y));
                        }

                        break; // only the lowest playable cell counts
                    }
                }
            }

            return cells;
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
            var crateHits = new HashSet<Cell>(); // a crate takes at most one hit per step
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (!board.IsPlayable(cell) || !done.Add(cell))
                {
                    continue;
                }

                if (board.HasCrate(cell))
                {
                    HitCrate(board, cell, crateHits, events); // a blast reached the crate itself
                    continue;
                }

                var piece = board[cell];
                if (piece.IsEmpty || piece.trophy)
                {
                    continue; // a trophy only leaves through the bottom
                }

                var cover = board.CoverAt(cell);
                if (cover.chain)
                {
                    // The chain takes the clear; the piece stays, free to move again.
                    cover.chain = false;
                    board.SetCover(cell, cover);
                    events.Add(new BoardEvent { type = BoardEventType.ChainBroken, a = cell, piece = piece });
                    continue;
                }

                board[cell] = Piece.Empty;
                events.Add(new BoardEvent { type = BoardEventType.Cleared, a = cell, piece = piece });
                if (cover.ice)
                {
                    cover.ice = false;
                    board.SetCover(cell, cover);
                    events.Add(new BoardEvent { type = BoardEventType.IceBroken, a = cell });
                }

                HitCrate(board, new Cell(cell.x + 1, cell.y), crateHits, events);
                HitCrate(board, new Cell(cell.x - 1, cell.y), crateHits, events);
                HitCrate(board, new Cell(cell.x, cell.y + 1), crateHits, events);
                HitCrate(board, new Cell(cell.x, cell.y - 1), crateHits, events);
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

        private static void HitCrate(Board board, Cell cell, HashSet<Cell> crateHits, List<BoardEvent> events)
        {
            if (!board.HasCrate(cell) || !crateHits.Add(cell))
            {
                return;
            }

            var cover = board.CoverAt(cell);
            bool oil = cover.oil;
            cover.crate--;
            cover.oil = oil && cover.crate > 0;
            board.SetCover(cell, cover);
            events.Add(new BoardEvent { type = oil ? BoardEventType.OilCleared : BoardEventType.CrateHit, a = cell, value = cover.crate });
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

        /// <summary>A random piece (or crate, when <paramref name="crates"/>) for a glider to fly to.</summary>
        private Cell? PickTarget(Board board, HashSet<Cell> done, Cell from, bool crates = true)
        {
            // ponytail: random target; aim at goal pieces and obstacles once the resolver knows the level's goals.
            var candidates = new List<Cell>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var c = new Cell(x, y);
                    bool target = !board[c].IsEmpty || (crates && board.HasCrate(c));
                    if (board.IsPlayable(c) && target && !done.Contains(c) && !c.IsAdjacentTo(from))
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
            a.IsAdjacentTo(b) && board.IsPlayable(a) && board.IsPlayable(b) && !board[a].IsEmpty && !board[b].IsEmpty
            && !board.IsLocked(a) && !board.IsLocked(b);

        /// <summary>A swap counts if it makes a match, or involves a disco, or swaps two specials.</summary>
        public static bool IsValid(Board board, Cell a, Cell b)
        {
            if (!CanSwap(board, a, b))
            {
                return false;
            }

            var pa = board[a];
            var pb = board[b];
            if (MoveResolver.DiscoOnTrophy(pa, pb))
            {
                return false;
            }

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

    /// <summary>Rearranges the free pieces (chained ones stay) until there is no match on the board and at least one move.</summary>
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
                    if (board.IsPlayable(x, y) && !board[x, y].IsEmpty && !board[x, y].trophy && !board.IsLocked(new Cell(x, y)))
                    {
                        cells.Add(new Cell(x, y)); // a trophy keeps its place (shuffled to the bottom, it would just leave)
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
        /// <param name="covers">Optional level shape rows with crates, ice and chains (see <see cref="Board.ApplyCovers"/>).</param>
        public static Board Generate(int width, int height, bool[] playable, int colors, SeededRandom rng, string[] covers = null)
        {
            var board = new Board(width, height, playable);
            if (covers != null)
            {
                board.ApplyCovers(covers);
            }

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
                        if (!board.IsPlayable(x, y) || board.HasCrate(x, y) || board[x, y].trophy || (keepSpecials && board[x, y].IsSpecial))
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
