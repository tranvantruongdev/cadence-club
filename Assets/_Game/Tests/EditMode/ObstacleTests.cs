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

        private static int Count(Board board, System.Func<Cell, bool> what) =>
            Enumerable.Range(0, board.Height).Sum(y => Enumerable.Range(0, board.Width).Count(x => what(new Cell(x, y))));

        [Test]
        public void A_match_next_to_oil_clears_it_and_the_cell_refills()
        {
            var board = Board.Parse(Rows);
            board.ApplyCovers(new[] { "....", ".o..", "...." });
            Assert.IsTrue(board.HasOil(new Cell(1, 1)) && board[1, 1].IsEmpty, "oil fills its cell");
            var first = Move(board, out _);
            Assert.IsTrue(first.Any(e => e.type == BoardEventType.OilCleared && e.a.Equals(new Cell(1, 1))));
            Assert.IsFalse(board.HasCrate(new Cell(1, 1)) || board.HasOil(new Cell(1, 1)));
            Assert.IsTrue(board.IsFull());
        }

        [Test]
        public void Oil_spreads_one_cell_after_each_move_that_clears_none()
        {
            var def = new LevelDef
            {
                shape = new[] { "o.....", "......", "......", "......", "......", ".....o" },
                colors = 4,
                moves = 25,
                goals = new[] { LevelDef.ClearOil(2) },
            };
            CollectionAssert.IsEmpty(def.Validate());
            var state = new LevelState(def, 3);
            var bot = new Bot(BotKind.Random, new SeededRandom(4));
            int spreads = 0;
            while (state.Outcome == LevelOutcome.Playing)
            {
                int before = Count(state.Board, state.Board.HasOil);
                var events = new List<BoardEvent>();
                var move = bot.Choose(state).Value;
                Assert.IsTrue(state.TryMove(move.a, move.b, events));
                int cleared = events.Count(e => e.type == BoardEventType.OilCleared);
                int spread = events.Count(e => e.type == BoardEventType.OilSpread);
                Assert.AreEqual(before - cleared + spread, Count(state.Board, state.Board.HasOil), "oil = before − cleared + spread");
                Assert.LessOrEqual(spread, cleared > 0 || state.Outcome != LevelOutcome.Playing ? 0 : 1, "one cell, and only after a move that cleared none");
                Assert.IsTrue(MoveFinder.HasMove(state.Board), "a spread never leaves the board stuck");
                spreads += spread;
            }

            Assert.Greater(spreads, 0, "a random player leaves oil alone long enough for it to spread");
        }

        [Test]
        public void An_oil_goal_cannot_ask_for_more_oil_than_the_board_starts_with()
        {
            var def = new LevelDef { shape = new[] { "o...", "....", "....", "...." }, goals = new[] { LevelDef.ClearOil(2) } };
            CollectionAssert.Contains(def.Validate(), "goal 1 wants 2 oil; the board starts with 1");
        }

        [Test]
        public void A_trophy_shrugs_off_a_rocket_falls_and_leaves_at_the_bottom()
        {
            var board = Board.Parse("ATBC", "CDAB", "DBCA");
            board[0, 2] = Piece.Make(0, Special.RocketH);
            var resolver = new MoveResolver(4, new SeededRandom(2));
            var events = new List<BoardEvent>();
            resolver.ResolveCells(board, new[] { new Cell(0, 2) }, events);
            Assert.IsTrue(events.Any(e => e.type == BoardEventType.Cleared && e.a.Equals(new Cell(2, 2))), "the rocket cleared its row");
            Assert.IsFalse(events.Any(e => e.type == BoardEventType.Cleared && e.piece.trophy), "blasts pass over a trophy");

            var trophies = Enumerable.Range(0, 3).SelectMany(y => Enumerable.Range(0, 4).Select(x => new Cell(x, y))).Where(c => board[c].trophy).ToList();
            Assert.AreEqual(1, trophies.Count + events.Count(e => e.type == BoardEventType.TrophyCollected), "still there, or already gone through the bottom");
            if (trophies.Count == 1)
            {
                var at = trophies[0];
                events.Clear();
                resolver.ResolveCells(board, Enumerable.Range(0, at.y).Select(y => new Cell(at.x, y)), events);
                var collected = events.Where(e => e.type == BoardEventType.TrophyCollected).ToList();
                Assert.AreEqual(1, collected.Count, "with the cells below it cleared, it falls to the bottom and leaves");
                Assert.AreEqual(new Cell(at.x, 0), collected[0].a);
            }

            Assert.AreEqual(0, Count(board, c => board[c].trophy));
            Assert.IsTrue(board.IsFull(), "its column refilled");
        }

        [Test]
        public void A_disco_cannot_take_a_trophy()
        {
            var board = Board.Parse("ABC", "T@B");
            Assert.IsFalse(MoveFinder.IsValid(board, new Cell(0, 0), new Cell(1, 0)));
            Assert.IsFalse(new MoveResolver(3, new SeededRandom(1)).TryMove(board, new Cell(0, 0), new Cell(1, 0), new List<BoardEvent>()));
        }

        [Test]
        public void Trophies_drop_one_at_a_time_until_the_goal_is_met()
        {
            var state = new LevelState(LevelDef.Rectangle(6, 6, 4, 80, LevelDef.BringTrophies(2)), 2);
            Assert.AreEqual(1, Count(state.Board, c => state.Board[c].trophy), "the level starts with one");
            Assert.IsTrue(Enumerable.Range(0, 6).Any(x => state.Board[x, 5].trophy), "at the top");
            var bot = new Bot(BotKind.Greedy, new SeededRandom(1));
            int drops = 0;
            while (state.Outcome == LevelOutcome.Playing)
            {
                var events = new List<BoardEvent>();
                var move = bot.Choose(state).Value;
                state.TryMove(move.a, move.b, events);
                drops += events.Count(e => e.type == BoardEventType.TrophyDropped);
                int onBoard = Count(state.Board, c => state.Board[c].trophy);
                Assert.AreEqual(state.Outcome == LevelOutcome.Playing && state.Remaining(0) > 0 ? 1 : 0, onBoard, "one trophy while more are needed");
            }

            Assert.AreEqual(LevelOutcome.Won, state.Outcome, "the greedy bot brings two trophies down in 80 moves");
            Assert.AreEqual(1, drops, "the second one dropped in after the first left");
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
