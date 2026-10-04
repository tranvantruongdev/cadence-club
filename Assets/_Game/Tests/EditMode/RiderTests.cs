using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Template.Core.Random;

namespace CadenceClub.Core.Tests
{
    /// <summary>Riders in a level: charge from their colour, then a power that uses no move.</summary>
    public class RiderTests
    {
        private static RiderDef Rider(PowerKind power, int amount, int color = 0, int charge = 6) => new RiderDef
        {
            id = power.ToString(),
            name = power.ToString(),
            rarity = Rarity.SR,
            color = color,
            power = power,
            charge = charge,
            powerByLevel = Enumerable.Repeat(amount, 5).ToArray(),
        };

        private static LevelState Charged(LevelDef def, PowerKind power, int amount) =>
            new LevelState(def, 3, new[] { new RiderSlot(Rider(power, amount), 1, 6) });

        private static List<BoardEvent> FirstStep(List<BoardEvent> events) =>
            events.SkipWhile(e => e.type != BoardEventType.StepStarted).Skip(1).TakeWhile(e => e.type != BoardEventType.StepStarted).ToList();

        [Test]
        public void Clearing_the_riders_colour_fills_the_charge_up_to_full()
        {
            var def = LevelDef.Rectangle(7, 8, 5, 40, LevelDef.Collect(1, 999));
            var state = new LevelState(def, 5, new[] { new RiderSlot(Rider(PowerKind.ExtraMoves, 3, color: 0, charge: 10), 1) });
            Assert.IsFalse(state.TryUsePower(0, new List<BoardEvent>()), "not charged yet");

            var bot = new Bot(BotKind.Greedy, new SeededRandom(5));
            int cleared = 0;
            while (!state.Squad[0].Full && state.Outcome == LevelOutcome.Playing)
            {
                var events = new List<BoardEvent>();
                var move = bot.Choose(state).Value;
                state.TryMove(move.a, move.b, events);
                cleared += events.Count(e => e.type == BoardEventType.Cleared && e.piece.color == 0);
                Assert.AreEqual(System.Math.Min(10, cleared), state.Squad[0].Charge, "one charge per piece of the rider's colour, capped");
            }

            Assert.IsTrue(state.Squad[0].Full);
            int moves = state.MovesLeft;
            Assert.IsTrue(state.TryUsePower(0, new List<BoardEvent>()));
            Assert.AreEqual(moves + 3, state.MovesLeft, "+3 moves");
            Assert.AreEqual(0, state.Squad[0].Charge, "the charge empties");
        }

        [Test]
        public void Row_rockets_clear_whole_rows_without_using_a_move()
        {
            var state = Charged(LevelDef.Rectangle(7, 8, 5, 20, LevelDef.Collect(0, 999)), PowerKind.RowRockets, 2);
            var events = new List<BoardEvent>();
            Assert.IsTrue(state.TryUsePower(0, events));
            Assert.AreEqual(20, state.MovesLeft);
            var rows = FirstStep(events).Where(e => e.type == BoardEventType.Cleared).GroupBy(e => e.a.y).Count(g => g.Count() == 7);
            Assert.GreaterOrEqual(rows, 2, "two full rows cleared");
        }

        [Test]
        public void Column_rockets_clear_whole_columns()
        {
            var state = Charged(LevelDef.Rectangle(7, 8, 5, 20, LevelDef.Collect(0, 999)), PowerKind.ColumnRockets, 2);
            var events = new List<BoardEvent>();
            Assert.IsTrue(state.TryUsePower(0, events));
            var columns = FirstStep(events).Where(e => e.type == BoardEventType.Cleared).GroupBy(e => e.a.x).Count(g => g.Count() == 8);
            Assert.GreaterOrEqual(columns, 2, "two full columns cleared");
        }

        [Test]
        public void Break_obstacles_hits_crates_ice_and_chains_before_pieces()
        {
            var def = new LevelDef
            {
                shape = new[] { ".......", "1.....1", ".......", "i.....i", ".......", "l.....l", ".......", "......." },
                colors = 5,
                moves = 20,
                goals = new[] { LevelDef.Collect(0, 999) },
            };
            var state = Charged(def, PowerKind.BreakObstacles, 6);
            var events = new List<BoardEvent>();
            Assert.IsTrue(state.TryUsePower(0, events));
            var first = FirstStep(events);
            Assert.AreEqual(2, first.Count(e => e.type == BoardEventType.CrateHit && e.value == 0));
            Assert.AreEqual(2, first.Count(e => e.type == BoardEventType.IceBroken));
            Assert.AreEqual(2, first.Count(e => e.type == BoardEventType.ChainBroken));
        }

        [Test]
        public void Make_specials_turns_plain_pieces_into_rockets_and_bombs()
        {
            var state = Charged(LevelDef.Rectangle(7, 8, 5, 20, LevelDef.Collect(0, 999)), PowerKind.MakeSpecials, 5);
            int Specials() => Enumerable.Range(0, 8).Sum(y => Enumerable.Range(0, 7).Count(x => state.Board[x, y].IsSpecial));
            int before = Specials();
            var events = new List<BoardEvent>();
            Assert.IsTrue(state.TryUsePower(0, events));
            Assert.AreEqual(5, events.Count(e => e.type == BoardEventType.SpecialCreated));
            Assert.AreEqual(before + 5, Specials());
            Assert.IsFalse(events.Any(e => e.type == BoardEventType.Cleared), "nothing goes off until matched");
        }

        [Test]
        public void A_power_can_win_the_level()
        {
            var def = new LevelDef
            {
                shape = new[] { ".......", ".......", "...1...", ".......", ".......", "......." },
                colors = 5,
                moves = 20,
                goals = new[] { LevelDef.BreakCrates(1) },
            };
            var state = Charged(def, PowerKind.BreakObstacles, 1);
            Assert.IsTrue(state.TryUsePower(0, new List<BoardEvent>()));
            Assert.AreEqual(LevelOutcome.Won, state.Outcome);
            Assert.AreEqual(20, state.MovesLeft, "won without using a move");
        }
    }
}
