using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Template.Core.Random;

namespace CadenceClub.Core.Tests
{
    public class ObstacleTests
    {
        // Swapping (0,0) and (1,0) on this board turns the bottom row into BAAA: one match, (1,0)–(3,0).
        private static readonly string[] Rows = { "CDCD", "DCDC", "ABAA" };

        private static List<BoardEvent> Move(Board board, out List<BoardEvent> all)
        {
            all = new List<BoardEvent>();
            Assert.IsTrue(new MoveResolver(4, new SeededRandom(5)).TryMove(board, new Cell(0, 0), new Cell(1, 0), all));
            return all.SkipWhile(e => e.type != BoardEventType.StepStarted).Skip(1)
                .TakeWhile(e => e.type != BoardEventType.StepStarted).ToList();
        }

        [Test]
        public void Pieces_land_on_a_crate_and_slide_in_diagonally_below_it()
        {
            var board = Board.Parse("ABC", "D.E", "F.A");
            board.ApplyCovers(new[] { "...", ".2.", "..." });
            var events = new List<BoardEvent>();
            Gravity.Apply(board, events);

            // B stays on the crate; D slides down-right under it; A falls into D's old cell.
            CollectionAssert.AreEqual(new[] { ".BC", "A2E", "FDA" }, board.ToRows());
            var fell = events.Where(e => e.type == BoardEventType.Fell).ToList();
            Assert.AreEqual(2, fell.Count, "one event per piece that moved");
            Assert.AreEqual((new Cell(0, 1), new Cell(1, 0)), (fell[0].a, fell[0].b));
            Assert.AreEqual((new Cell(0, 2), new Cell(0, 1)), (fell[1].a, fell[1].b));
        }

        [Test]
        public void A_chained_piece_holds_its_place_and_cannot_be_swapped()
        {
            var column = Board.Parse("A", ".", "B", ".");
            column.ApplyCovers(new[] { ".", ".", "l", "." });
            Gravity.Apply(column, new List<BoardEvent>());
            CollectionAssert.AreEqual(new[] { ".", "A", "B", "." }, column.ToRows(), "A lands on the chained B; B stays put");

            var row = Board.Parse("AB");
            row.ApplyCovers(new[] { ".l" });
            Assert.IsFalse(MoveFinder.CanSwap(row, new Cell(0, 0), new Cell(1, 0)));
        }

        [Test]
        public void A_match_next_to_a_crate_hits_it_once()
        {
            var board = Board.Parse(Rows);
            board.ApplyCovers(new[] { "....", ".2..", "...." });
            var first = Move(board, out _);
            var hits = first.Where(e => e.type == BoardEventType.CrateHit).ToList();
            Assert.AreEqual(1, hits.Count, "one hit per crate per step, however many cleared pieces touch it");
            Assert.AreEqual(new Cell(1, 1), hits[0].a);
            Assert.AreEqual(1, hits[0].value, "a 2-hit crate has one hit left");
        }

        [Test]
        public void A_crate_breaks_on_its_last_hit_and_the_cell_refills()
        {
            var board = Board.Parse(Rows);
            board.ApplyCovers(new[] { "....", ".1..", "...." });
            var first = Move(board, out _);
            Assert.IsTrue(first.Any(e => e.type == BoardEventType.CrateHit && e.a.Equals(new Cell(1, 1)) && e.value == 0));
            Assert.IsFalse(board.HasCrate(new Cell(1, 1)));
            Assert.IsTrue(board.IsFull());
        }

        [Test]
        public void Ice_breaks_only_under_a_cleared_piece()
        {
            var board = Board.Parse(Rows);
            board.ApplyCovers(new[] { "....", "i...", "..i." });
            var first = Move(board, out _);
            var broken = first.Where(e => e.type == BoardEventType.IceBroken).Select(e => e.a).ToList();
            CollectionAssert.AreEqual(new[] { new Cell(2, 0) }, broken, "(2,0) was in the match; (0,1) wasn't");
            Assert.IsFalse(board.CoverAt(new Cell(2, 0)).ice);
        }

        [Test]
        public void A_match_through_a_chain_breaks_the_chain_and_keeps_the_piece()
        {
            var board = Board.Parse(Rows);
            board.ApplyCovers(new[] { "....", "....", "...l" });
            Assert.IsFalse(MoveFinder.CanSwap(board, new Cell(2, 0), new Cell(3, 0)), "chained pieces can't move");
            var first = Move(board, out _);
            Assert.IsTrue(first.Any(e => e.type == BoardEventType.ChainBroken && e.a.Equals(new Cell(3, 0))));
            Assert.IsFalse(first.Any(e => e.type == BoardEventType.Cleared && e.a.Equals(new Cell(3, 0))), "the chain took the clear");
            Assert.IsFalse(board.IsLocked(new Cell(3, 0)));
        }

        [Test]
        public void Crate_and_ice_goals_count_their_events_and_the_bot_clears_them()
        {
            var def = new LevelDef
            {
                shape = new[] { ".......", ".......", "..2.2..", ".......", "...l...", ".......", "iiiiiii", "iiiiiii" },
                colors = 5,
                moves = 40,
                goals = new[] { LevelDef.ClearIce(14), LevelDef.BreakCrates(2) },
            };

            int wins = 0;
            for (ulong seed = 1; seed <= 5; seed++)
            {
                var state = new LevelState(def, seed);
                var bot = new Bot(BotKind.Greedy, new SeededRandom(seed));
                var events = new List<BoardEvent>();
                int ice = 0;
                int crates = 0;
                while (state.Outcome == LevelOutcome.Playing)
                {
                    events.Clear();
                    var move = bot.Choose(state).Value;
                    Assert.IsTrue(state.TryMove(move.a, move.b, events));
                    ice += events.Count(e => e.type == BoardEventType.IceBroken);
                    crates += events.Count(e => e.type == BoardEventType.CrateHit && e.value == 0);
                    Assert.AreEqual(System.Math.Min(14, ice), state.Progress(0), $"seed {seed}: ice progress");
                    Assert.AreEqual(System.Math.Min(2, crates), state.Progress(1), $"seed {seed}: crate progress");
                    Assert.IsTrue(state.Board.IsFull(), $"seed {seed}: settled board is full");
                }

                if (state.Outcome == LevelOutcome.Won)
                {
                    wins++;
                    for (int y = 0; y < def.Height; y++)
                    {
                        for (int x = 0; x < def.Width; x++)
                        {
                            var cover = state.Board.CoverAt(x, y);
                            Assert.IsFalse(cover.ice || cover.crate > 0, $"seed {seed}: a won board has no ice or crates left");
                        }
                    }
                }
            }

            Assert.GreaterOrEqual(wins, 4, "the greedy bot clears 14 ice and 2 crates in 40 moves");
        }
    }
}
