using System;
using System.Collections.Generic;

namespace Template.Core.Flow
{
    /// <summary>
    /// Small finite state machine with an explicit allow-list of transitions.
    /// Used for app flow (Boot → Title → Game) and in-game flow (Ready → Playing → Results).
    /// </summary>
    public sealed class StateMachine<TState> where TState : struct, Enum
    {
        private readonly Dictionary<TState, HashSet<TState>> _allowed = new Dictionary<TState, HashSet<TState>>();

        public StateMachine(TState initial)
        {
            Current = initial;
        }

        public TState Current { get; private set; }

        /// <summary>Raised after every transition with (previous, next).</summary>
        public event Action<TState, TState> Changed;

        public StateMachine<TState> Allow(TState from, params TState[] to)
        {
            if (!_allowed.TryGetValue(from, out var targets))
            {
                targets = new HashSet<TState>();
                _allowed[from] = targets;
            }

            foreach (var target in to)
            {
                targets.Add(target);
            }

            return this;
        }

        public bool CanGo(TState to) => _allowed.TryGetValue(Current, out var targets) && targets.Contains(to);

        public bool TryGo(TState to)
        {
            if (!CanGo(to))
            {
                return false;
            }

            var previous = Current;
            Current = to;
            Changed?.Invoke(previous, to);
            return true;
        }

        public void Go(TState to)
        {
            if (!TryGo(to))
            {
                throw new InvalidOperationException($"Transition {Current} -> {to} is not allowed.");
            }
        }
    }
}
