using System;
using NUnit.Framework;
using Template.Core.Flow;

namespace Template.Core.Tests
{
    public class StateMachineTests
    {
        private enum S { Boot, Title, Game, Results }

        private static StateMachine<S> Create() => new StateMachine<S>(S.Boot)
            .Allow(S.Boot, S.Title)
            .Allow(S.Title, S.Game)
            .Allow(S.Game, S.Results, S.Title)
            .Allow(S.Results, S.Game, S.Title);

        [Test]
        public void Starts_in_initial_state()
        {
            Assert.AreEqual(S.Boot, Create().Current);
        }

        [Test]
        public void Allowed_transition_changes_state_and_raises_event()
        {
            var fsm = Create();
            S from = S.Results, to = S.Results;
            fsm.Changed += (a, b) => { from = a; to = b; };

            Assert.IsTrue(fsm.TryGo(S.Title));
            Assert.AreEqual(S.Title, fsm.Current);
            Assert.AreEqual(S.Boot, from);
            Assert.AreEqual(S.Title, to);
        }

        [Test]
        public void Disallowed_transition_is_rejected_without_side_effects()
        {
            var fsm = Create();
            bool raised = false;
            fsm.Changed += (a, b) => raised = true;

            Assert.IsFalse(fsm.TryGo(S.Game));
            Assert.AreEqual(S.Boot, fsm.Current);
            Assert.IsFalse(raised);
            Assert.Throws<InvalidOperationException>(() => fsm.Go(S.Results));
        }

        [Test]
        public void Allow_accumulates_targets()
        {
            var fsm = new StateMachine<S>(S.Game).Allow(S.Game, S.Results).Allow(S.Game, S.Title);
            Assert.IsTrue(fsm.CanGo(S.Results));
            Assert.IsTrue(fsm.CanGo(S.Title));
            Assert.IsFalse(fsm.CanGo(S.Boot));
        }
    }
}
