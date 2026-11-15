namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The visit's rotation with the arenas of modes on top: when the session's next round is a mode with an arena of its
    /// own (<see cref="ModeRules.Arena"/>, FOOTBALL's pitch), that arena is <see cref="Current"/>, so the lobby names it,
    /// its floor is loaded first and the analytics carry its id. The rotation underneath does not move for such a round:
    /// the round after it is on the arena the rotation had ready.
    /// </summary>
    public sealed class ModeArenaRotation : IArenaRotation
    {
        private readonly ArenaRotation rotation;
        private readonly GameTuning tuning;
        private readonly ISessionProgress progress;

        // Whether the round that began last was on a mode's own arena: the next Next leaves the rotation where it is.
        private bool playedOwn;

        public ModeArenaRotation(ArenaRotation rotation, GameTuning tuning, ISessionProgress progress)
        {
            this.rotation = rotation;
            this.tuning = tuning;
            this.progress = progress;
        }

        private GameTuning.MapSettings Own => RoundRules.RulesFor(tuning, progress).Arena;

        public string Current => Own?.Id ?? rotation.Current;

        public string CurrentVariant
        {
            get
            {
                var own = Own;
                if (own == null) {
                    return rotation.CurrentVariant;
                }

                return own.Variants.Count > 0 ? own.Variants[0].Id : null;
            }
        }

        public void Next(int level)
        {
            if (playedOwn) {
                playedOwn = false;
                return;
            }

            rotation.Next(level);
        }

        public bool IsNew(string id) => rotation.IsNew(id);

        public void MarkSeen(string id) => rotation.MarkSeen(id);

        public void MarkPlayed(string map, string variant)
        {
            playedOwn = map != rotation.Current;
            if (!playedOwn) {
                rotation.MarkPlayed(map, variant);
            }
        }
    }
}
