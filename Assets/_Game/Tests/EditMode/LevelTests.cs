using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Template.Core.Random;

namespace CadenceClub.Core.Tests
{
    public class LevelTests
    {
        private static void PlayToEnd(LevelState state, BotKind kind = BotKind.Greedy, ulong seed = 1)
        {
            var bot = new Bot(kind, new SeededRandom(seed));
            var events = new List<BoardEvent>();
            while (state.Outcome == LevelOutcome.Playing)
            {
                var move = bot.Choose(state);
                Assert.IsTrue(move.HasValue, "a move is always available");
                events.Clear();
                Assert.IsTrue(state.TryMove(move.Value.a, move.Value.b, events));
            }
        }

        [Test]
        public void Collect_goal_counts_only_its_colour_and_wins_when_met()
        {
            var def = LevelDef.Rectangle(7, 7, 4, 40, LevelDef.Collect(2, 12));
            var state = new LevelState(def, 3);
            var events = new List<BoardEvent>();
            var bot = new Bot(BotKind.Greedy, new SeededRandom(3));
            int counted = 0;
            while (state.Outcome == LevelOutcome.Playing)
            {
                var move = bot.Choose(state).Value;
                events.Clear();
                state.TryMove(move.a, move.b, events);
                counted += events.Count(e => e.type == BoardEventType.Cleared && e.piece.color == 2);
                Assert.AreEqual(System.Math.Min(12, counted), state.Progress(0), "progress = colour-2 pieces cleared, capped at the goal");
            }

            Assert.AreEqual(LevelOutcome.Won, state.Outcome, "12 pieces in 40 moves is easy");
            Assert.AreEqual(12, state.Progress(0));
            Assert.Greater(state.MovesLeft, 0);
        }

        [Test]
        public void Running_out_of_moves_loses_and_no_move_is_accepted_after()
        {
            var def = LevelDef.Rectangle(6, 6, 4, 3, LevelDef.Collect(0, 999));
            var state = new LevelState(def, 1);
            PlayToEnd(state);
            Assert.AreEqual(LevelOutcome.Lost, state.Outcome);
            Assert.AreEqual(0, state.MovesLeft);
            Assert.AreEqual(3, state.MovesUsed);
            var (a, b) = MoveFinder.FindAll(state.Board)[0];
            Assert.IsFalse(state.TryMove(a, b, new List<BoardEvent>()), "the level is over");
        }

        [Test]
        public void A_clone_plays_on_without_touching_the_original()
        {
            var state = new LevelState(LevelDef.Rectangle(7, 7, 5, 20, LevelDef.Collect(0, 30)), 5);
            var before = state.Board.ToRows();
            var clone = state.Clone();
            var (a, b) = MoveFinder.FindAll(clone.Board)[0];
            Assert.IsTrue(clone.TryMove(a, b, new List<BoardEvent>()));
            CollectionAssert.AreEqual(before, state.Board.ToRows());
            Assert.AreEqual(20, state.MovesLeft);
            Assert.AreEqual(19, clone.MovesLeft);
        }

        [Test]
        public void Simulator_gives_the_same_result_for_the_same_seeds()
        {
            var def = LevelDef.Rectangle(8, 8, 5, 20, LevelDef.Collect(0, 20), LevelDef.Collect(1, 20));
            var first = LevelSimulator.Run(def, 30, BotKind.Greedy, 100);
            var second = LevelSimulator.Run(def, 30, BotKind.Greedy, 100);
            Assert.AreEqual(first.wins, second.wins);
            Assert.AreEqual(first.averageGoalCompletion, second.averageGoalCompletion, 1e-12);
        }

        [Test]
        public void Greedy_bot_beats_the_random_bot()
        {
            var def = LevelDef.Rectangle(8, 8, 5, 18, LevelDef.Collect(0, 18), LevelDef.Collect(3, 18));
            var greedy = LevelSimulator.Run(def, 60, BotKind.Greedy, 1);
            var random = LevelSimulator.Run(def, 60, BotKind.Random, 1);
            TestContext.WriteLine($"greedy: {greedy}\nrandom: {random}");
            Assert.Greater(greedy.averageGoalCompletion, random.averageGoalCompletion, "focused play gets further");
            Assert.GreaterOrEqual(greedy.wins, random.wins);
        }

        [Test]
        public void Moves_to_win_gives_the_win_rate_of_every_move_limit_in_one_pass()
        {
            var def = LevelDef.Rectangle(7, 8, 5, 20, LevelDef.Collect(0, 25), LevelDef.Collect(2, 25));
            var needed = LevelSimulator.MovesToWin(def, 40, BotKind.Greedy, 1, 60);
            foreach (int moves in new[] { 8, 12, 16, 20 })
            {
                var limited = def.Clone();
                limited.moves = moves;
                var run = LevelSimulator.Run(limited, 40, BotKind.Greedy, 1);
                Assert.AreEqual(run.wins, needed.Count(n => n <= moves), $"{moves} moves: same wins as a separate simulation");
            }

            int fit = LevelSimulator.FitMoves(needed, (0.65, 0.80));
            double rate = LevelSimulator.WinRate(needed, fit);
            Assert.That(rate, Is.InRange(0.65, 0.80), "the fitted limit lands in the band");
            for (int moves = 1; moves <= 60; moves++)
            {
                double other = LevelSimulator.WinRate(needed, moves);
                if (other >= 0.65 && other <= 0.80)
                {
                    Assert.LessOrEqual(System.Math.Abs(rate - 0.725), System.Math.Abs(other - 0.725) + 1e-9, "and is the closest to its middle");
                }
            }
        }

        [Test]
        public void Validate_explains_what_is_wrong_with_a_level()
        {
            Assert.IsEmpty(LevelDef.Rectangle(7, 8, 5, 20, LevelDef.Collect(0, 10)).Validate());

            var broken = new LevelDef
            {
                shape = new[] { "..i..", "..x", "1...." },
                colors = 7,
                moves = 0,
                goals = new[] { LevelDef.Collect(5, 10), LevelDef.ClearIce(3), LevelDef.BreakCrates(1) },
            };
            var problems = broken.Validate();
            TestContext.WriteLine(string.Join("\n", problems));
            Assert.That(problems, Has.Some.Contains("row 2 is 3 wide"));
            Assert.That(problems, Has.Some.Contains("'x'"));
            Assert.That(problems, Has.Some.Contains("colours must be 3–6"));
            Assert.That(problems, Has.Some.Contains("moves must be at least 1"));
            Assert.That(problems, Has.Some.Contains("wants 3 ice; the board has 1"));
            Assert.IsFalse(problems.Any(p => p.Contains("crates")), "one crate on the board covers a 1-crate goal");
        }

        [Test]
        public void Target_bands_follow_the_sawtooth()
        {
            Assert.AreEqual((0.90, 1.0), LevelBands.For(3), "first levels are easy");
            Assert.AreEqual((0.65, 0.80), LevelBands.For(7), "normal");
            Assert.AreEqual((0.40, 0.55), LevelBands.For(10), "bump every 5th level");
            Assert.AreEqual((0.90, 1.0), LevelBands.For(11), "breather after a bump");
            Assert.AreEqual((0.35, 0.45), LevelBands.For(30), "finale");
        }
    }
}
