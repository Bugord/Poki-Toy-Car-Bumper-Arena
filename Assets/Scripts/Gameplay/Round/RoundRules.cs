namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The special rule of this scene's round (<see cref="GameTuning.SpecialRoundSettings"/>), read from the session's
    /// progress once, as the scene is built, like the difficulty: the lobby before it named the same one. Only a build
    /// of last-car-standing rounds has them. <see cref="SpecialRoundPlay"/> puts the rule on the arena; the round asks
    /// here which car is the boss.
    /// </summary>
    public sealed class RoundRules
    {
        public RoundRules(GameTuning tuning, ISessionProgress progress)
        {
            this.Kind = For(tuning, progress.Round);
            // The boss starts across the floor from the player, who is first, nearest the camera.
            this.BossIndex = Kind == SpecialRound.Boss ? 1 + RoundDifficulty.BotCountFor(tuning, progress) / 2 : -1;
        }

        public SpecialRound Kind { get; }

        public bool IsSpecial => Kind != SpecialRound.None;

        /// <summary>The boss's car index in a boss round (1..N), -1 in any other.</summary>
        public int BossIndex { get; }

        /// <summary>The special round of the visit's round <paramref name="round"/> in this build; none in a match.</summary>
        public static SpecialRound For(GameTuning tuning, int round) =>
            tuning.IsMatch ? SpecialRound.None : tuning.SpecialRounds.For(round);

        /// <summary>The id of a special round for analytics and the string table: no numbers, never changed.</summary>
        public static string IdOf(SpecialRound kind) => kind switch {
            SpecialRound.Mega => "mega",
            SpecialRound.HotPotato => "hot-potato",
            SpecialRound.Boss => "boss",
            SpecialRound.Ice => "ice",
            SpecialRound.LowGravity => "low-gravity",
            _ => "none",
        };
    }
}
