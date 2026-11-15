using System;
using System.Threading;
using Bugord.Core.Storage;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using UniState;

namespace ToyCarArena.App
{
    /// <summary>
    /// The root state: decides where a visit opens. A player who has never played (no XP and no round finished on this
    /// device) goes straight into the arena (<see cref="RoundState"/>), which waits there for the first input, on any
    /// visit; one who has played opens on the main menu (<see cref="MenuState"/>, <see cref="GameTuning.MenuSettings"/>). The kit runs the boot tasks, the platform's start among them, before the
    /// root state, so the visit is counted here (<see cref="Visits.Record"/> is idempotent) and the decision never waits
    /// on a frame.
    /// </summary>
    public sealed class EntryState : StateBase
    {
        private readonly Visits visits;
        private readonly GameTuning tuning;
        private readonly IKeyValueStorage storage;

        public EntryState(Visits visits, GameTuning tuning, IKeyValueStorage storage)
        {
            this.visits = visits;
            this.tuning = tuning;
            this.storage = storage;
        }

        /// <summary>Whether this device's player has played: some XP saved, or a round finished.</summary>
        public static bool HasPlayed(IKeyValueStorage storage) =>
            storage.GetInt(PlayerProgress.XpKey) > 0 || storage.GetInt(SessionProgress.RoundsKey) > 0;

        public override UniTask<StateTransitionInfo> Execute(CancellationToken token)
        {
            visits.Record(DateTime.Now);
            return UniTask.FromResult(tuning.Menu.OpensFor(HasPlayed(storage))
                ? Transition.GoTo<MenuState>()
                : Transition.GoTo<RoundState>());
        }
    }
}
