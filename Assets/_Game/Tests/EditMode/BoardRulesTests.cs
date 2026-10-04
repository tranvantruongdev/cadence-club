using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Template.Core.Random;

namespace CadenceClub.Core.Tests
{
    public class BoardRulesTests
    {
        private static MatchGroup Single(params string[] rows)
        {
            var groups = MatchFinder.Find(Board.Parse(rows));
            Assert.AreEqual(1, groups.Count, "expected exactly one match group");
            return groups[0];
        }

        [Test]
        public void Parse_and_ToRows_round_trip()
        {
            string[] rows = { "AB#", ".@C" };
            CollectionAssert.AreEqual(rows, Board.Parse(rows).ToRows());
        }

        [Test]
        public void Three_in_a_row_clears_without_a_special()
        {
            var group = Single("AAAB");
            Assert.AreEqual(3, group.cells.Count);
            Assert.AreEqual(Special.None, group.creates);
        }

        [Test]
        public void Four_in_a_line_makes_a_rocket_five_makes_a_disco()
        {
            Assert.AreEqual(Special.RocketH, Single("AAAA").creates);
            Assert.AreEqual(Special.RocketV, Single("A", "A", "A", "A").creates);
            Assert.AreEqual(Special.Disco, Single("AAAAA").creates);
        }

        [Test]
        public void L_and_T_shapes_make_a_bomb_at_the_corner()
        {
            var l = Single("A..", "A..", "AAA");
            Assert.AreEqual(Special.Bomb, l.creates);
            Assert.AreEqual(5, l.cells.Count);
            Assert.AreEqual(new Cell(0, 0), l.createAt, "the bomb appears where the row and column meet");

            var t = Single("AAA", ".A.", ".A.");
            Assert.AreEqual(Special.Bomb, t.creates);
            Assert.AreEqual(new Cell(1, 2), t.createAt);
        }

        [Test]
        public void Two_by_two_makes_a_glider()
        {
            var group = Single("AA", "AA");
            Assert.AreEqual(Special.Glider, group.creates);
            Assert.AreEqual(4, group.cells.Count);
        }

        [Test]
        public void Rocket_from_a_swap_points_along_the_swipe_and_appears_where_the_piece_landed()
        {
            // Swapping (3,0) and (3,1) completes AAAA on the bottom row with a vertical swipe.
            var board = Board.Parse("...A", "AAAB");
            MoveResolver.Swap(board, new Cell(3, 0), new Cell(3, 1));
            var groups = MatchFinder.Find(board, new Cell(3, 1), new Cell(3, 0));
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(Special.RocketV, groups[0].creates);
            Assert.AreEqual(new Cell(3, 0), groups[0].createAt);
        }

        [Test]
        public void Gravity_drops_pieces_past_holes()
        {
            var board = Board.Parse("A", ".", "#", ".");
            var events = new List<BoardEvent>();
            Gravity.Apply(board, events);
            CollectionAssert.AreEqual(new[] { ".", ".", "#", "A" }, board.ToRows());
            Assert.AreEqual(1, events.Count(e => e.type == BoardEventType.Fell));
        }

        [Test]
        public void Generated_boards_have_no_match_and_a_move_and_repeat_by_seed()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var board = BoardGenerator.Generate(8, 8, null, 5, new SeededRandom(seed));
                Assert.IsTrue(board.IsFull(), $"seed {seed}: full");
                Assert.AreEqual(0, MatchFinder.Find(board).Count, $"seed {seed}: no starting match");
                Assert.IsTrue(MoveFinder.HasMove(board), $"seed {seed}: at least one move");
                CollectionAssert.AreEqual(board.ToRows(), BoardGenerator.Generate(8, 8, null, 5, new SeededRandom(seed)).ToRows());
            }
        }

        [Test]
        public void A_swap_without_a_match_is_rejected_and_undone()
        {
            var board = Board.Parse("ABC", "BCA", "CAB");
            var before = board.ToRows();
            var events = new List<BoardEvent>();
            bool moved = new MoveResolver(3, new SeededRandom(1)).TryMove(board, new Cell(0, 0), new Cell(1, 0), events);
            Assert.IsFalse(moved);
            CollectionAssert.AreEqual(before, board.ToRows());
            CollectionAssert.AreEqual(new[] { BoardEventType.Swapped, BoardEventType.SwapRejected }, events.Select(e => e.type));
        }

        [Test]
        public void A_valid_move_clears_cascades_and_leaves_a_stable_full_board()
        {
            var rng = new SeededRandom(7);
            var board = BoardGenerator.Generate(8, 8, null, 5, rng);
            var resolver = new MoveResolver(5, rng);
            var (a, b) = MoveFinder.FindAll(board)[0];
            var events = new List<BoardEvent>();
            Assert.IsTrue(resolver.TryMove(board, a, b, events));
            Assert.GreaterOrEqual(events.Count(e => e.type == BoardEventType.Cleared), 3);
            Assert.IsTrue(board.IsFull());
            Assert.AreEqual(0, MatchFinder.Find(board).Count, "no match is left on a settled board");
            Assert.IsTrue(MoveFinder.HasMove(board), "a move is always available after resolving");
        }

        [Test]
        public void A_bomb_set_off_by_a_match_clears_the_three_by_three_around_it()
        {
            // Swapping (4,3) down into (4,2) makes AAA on row 2, through the bomb at (2,2).
            var board = Board.Parse("BCBCB", "CBCBA", "BCAAB", "CBCBC", "BCBCB");
            board[2, 2] = Piece.Make(0, Special.Bomb);
            var events = new List<BoardEvent>();
            Assert.IsTrue(new MoveResolver(3, new SeededRandom(5)).TryMove(board, new Cell(4, 3), new Cell(4, 2), events));

            var firstStep = FirstStepClears(events);
            AssertSquareCleared(firstStep, new Cell(2, 2), 1);
            Assert.IsTrue(events.Any(e => e.type == BoardEventType.SpecialActivated && e.piece.special == Special.Bomb));
        }

        [Test]
        public void Rocket_plus_rocket_clears_a_cross_where_the_piece_landed()
        {
            var board = Board.Parse("ABCA", "BCAB", "CABC", "ABCA");
            board[0, 0] = Piece.Make(0, Special.RocketH);
            board[1, 0] = Piece.Make(1, Special.RocketV);
            var events = new List<BoardEvent>();
            Assert.IsTrue(new MoveResolver(3, new SeededRandom(3)).TryMove(board, new Cell(0, 0), new Cell(1, 0), events));

            var firstStep = FirstStepClears(events);
            for (int i = 0; i < 4; i++)
            {
                Assert.Contains(new Cell(i, 0), firstStep, "row of the landing cell");
                Assert.Contains(new Cell(1, i), firstStep, "column of the landing cell");
            }
        }

        [Test]
        public void Rocket_plus_bomb_clears_a_three_wide_cross()
        {
            var board = Board.Parse("ABCABC", "BCABCA", "CABCAB", "ABCABC", "BCABCA", "CABCAB");
            board[2, 2] = Piece.Make(0, Special.RocketH);
            board[3, 2] = Piece.Make(1, Special.Bomb);
            var events = new List<BoardEvent>();
            Assert.IsTrue(new MoveResolver(3, new SeededRandom(4)).TryMove(board, new Cell(2, 2), new Cell(3, 2), events));

            var firstStep = FirstStepClears(events);
            for (int i = 0; i < 6; i++)
            {
                for (int d = -1; d <= 1; d++)
                {
                    Assert.Contains(new Cell(i, 2 + d), firstStep, "three rows through (3,2)");
                    Assert.Contains(new Cell(3 + d, i), firstStep, "three columns through (3,2)");
                }
            }
        }

        [Test]
        public void Bomb_plus_bomb_clears_five_by_five()
        {
            var board = Board.Parse("ABCABCA", "BCABCAB", "CABCABC", "ABCABCA", "BCABCAB", "CABCABC", "ABCABCA");
            board[3, 3] = Piece.Make(0, Special.Bomb);
            board[4, 3] = Piece.Make(1, Special.Bomb);
            var events = new List<BoardEvent>();
            Assert.IsTrue(new MoveResolver(3, new SeededRandom(6)).TryMove(board, new Cell(3, 3), new Cell(4, 3), events));
            AssertSquareCleared(FirstStepClears(events), new Cell(4, 3), 2);
        }

        [Test]
        public void Disco_plus_rocket_turns_that_colour_into_rockets_that_all_go_off()
        {
            var board = Board.Parse("ABCA", "BCAB", "CABC", "@BCA");
            board[1, 0] = Piece.Make(1, Special.RocketH);
            var events = new List<BoardEvent>();
            Assert.IsTrue(new MoveResolver(3, new SeededRandom(8)).TryMove(board, new Cell(0, 0), new Cell(1, 0), events));

            var firstStep = FirstStepEvents(events);
            int rocketsFired = firstStep.Count(e => e.type == BoardEventType.SpecialActivated &&
                                                    (e.piece.special == Special.RocketH || e.piece.special == Special.RocketV));
            Assert.AreEqual(5, rocketsFired, "every B (4 plain + the rocket) became a rocket and fired");
        }

        [Test]
        public void Disco_swapped_with_a_colour_clears_exactly_that_colour()
        {
            var board = Board.Parse("ABCA", "BCAB", "CABC", "@BCA");
            var events = new List<BoardEvent>();
            Assert.IsTrue(new MoveResolver(3, new SeededRandom(9)).TryMove(board, new Cell(0, 0), new Cell(1, 0), events)); // with a B

            var cleared = FirstStepEvents(events).Where(e => e.type == BoardEventType.Cleared).ToList();
            Assert.AreEqual(1, cleared.Count(e => e.piece.special == Special.Disco));
            Assert.IsTrue(cleared.Where(e => e.piece.special != Special.Disco).All(e => e.piece.color == 1), "only colour B is cleared");
            Assert.AreEqual(5, cleared.Count(e => e.piece.color == 1), "every B on the board");
        }

        [Test]
        public void Same_seed_and_moves_give_the_same_events()
        {
            string Play()
            {
                var rng = new SeededRandom(42);
                var board = BoardGenerator.Generate(7, 9, null, 5, rng);
                var resolver = new MoveResolver(5, rng);
                var log = new List<string>();
                for (int i = 0; i < 20; i++)
                {
                    var events = new List<BoardEvent>();
                    var (a, b) = MoveFinder.FindAll(board)[0];
                    resolver.TryMove(board, a, b, events);
                    log.AddRange(events.Select(e => e.ToString()));
                }

                return string.Join("|", log) + "#" + board;
            }

            Assert.AreEqual(Play(), Play());
        }

        [Test]
        public void Many_moves_always_settle_with_a_move_left()
        {
            var rng = new SeededRandom(2026);
            var board = BoardGenerator.Generate(9, 9, Diamond(9), 5, rng);
            var resolver = new MoveResolver(5, rng);
            for (int i = 0; i < 500; i++)
            {
                var moves = MoveFinder.FindAll(board);
                Assert.IsNotEmpty(moves, $"move {i}: no move left");
                var (a, b) = moves[rng.Range(0, moves.Count)];
                var events = new List<BoardEvent>();
                Assert.IsTrue(resolver.TryMove(board, a, b, events), $"move {i}: valid move rejected");
                int steps = events.Where(e => e.type == BoardEventType.StepStarted).Select(e => e.value).DefaultIfEmpty(0).Max();
                Assert.Less(steps, MoveResolver.MaxSteps - 1, "cascades end well before the safety cap");
                Assert.IsTrue(board.IsFull());
                Assert.AreEqual(0, MatchFinder.Find(board).Count);
            }
        }

        [Test]
        public void Shuffle_always_leaves_a_move_and_no_match()
        {
            var board = Board.Parse("ABAB", "CDCD", "ABAB", "CDCD");
            Assert.IsFalse(MoveFinder.HasMove(board), "precondition: this board has no move");
            Shuffler.Shuffle(board, new SeededRandom(11), 4);
            Assert.IsTrue(MoveFinder.HasMove(board));
            Assert.AreEqual(0, MatchFinder.Find(board).Count);
        }

        private static List<BoardEvent> FirstStepEvents(List<BoardEvent> events) =>
            events.SkipWhile(e => e.type != BoardEventType.StepStarted).Skip(1)
                .TakeWhile(e => e.type != BoardEventType.StepStarted).ToList();

        private static List<Cell> FirstStepClears(List<BoardEvent> events) =>
            FirstStepEvents(events).Where(e => e.type == BoardEventType.Cleared).Select(e => e.a).ToList();

        private static void AssertSquareCleared(List<Cell> cleared, Cell centre, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    Assert.Contains(new Cell(centre.x + dx, centre.y + dy), cleared, $"cell around {centre} cleared");
                }
            }
        }

        /// <summary>A diamond-shaped board: holes in the corners.</summary>
        private static bool[] Diamond(int size)
        {
            var playable = new bool[size * size];
            int mid = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    playable[y * size + x] = System.Math.Abs(x - mid) + System.Math.Abs(y - mid) <= mid + 1;
                }
            }

            return playable;
        }
    }
}
