using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using UniState;

namespace ToyCarArena.App
{
    /// <summary>
    /// The root state: decides where a visit opens. A first visit goes straight into the arena (<see cref="RoundState"/>),
    /// which waits there for the player's first input; a returning one opens on the main menu (<see cref="MenuState"/>,
    /// <see cref="GameTuning.MenuSettings"/>). The kit runs the boot tasks, the platform's start among them, before the
    /// root state, so the visit is counted here (<see cref="Visits.Record"/> is idempotent) and the decision never waits
    /// on a frame.
    /// </summary>
    public sealed class EntryState : StateBase
    {
        private readonly Visits visits;
        private readonly GameTuning tuning;

        public EntryState(Visits visits, GameTuning tuning)
        {
            this.visits = visits;
            this.tuning = tuning;
        }

        public override UniTask<StateTransitionInfo> Execute(CancellationToken token)
        {
            visits.Record(DateTime.Now);
            return UniTask.FromResult(tuning.Menu.OpensOn(visits.Count)
                ? Transition.GoTo<MenuState>()
                : Transition.GoTo<RoundState>());
        }
    }
}
