namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The special rule of this scene's round (<see cref="GameTuning.SpecialRoundSettings"/>), read from the session's
    /// progress once, as the scene is built, like the difficulty: the lobby before it named the same one. Only a build
    /// of last-car-standing rounds has them. <see cref="SpecialRoundPlay"/> puts the rule on the arena; the round asks
    /// here which car is the boss.
    /// <para>
    /// The menu's event mode (<see cref="ISessionProgress.Mode"/>) wins over the rotation: HOT POTATO makes every round
    /// <see cref="SpecialRound.HotPotato"/>, which the rotation also plays now and then. Everything the mode
    /// changes reads <see cref="IsHotPotato"/>: the fence, no crumbling, no mechanics and no boxes, the bomb, the bots.
    /// </para>
    /// </summary>
    public sealed class RoundRules
    {
        public RoundRules(GameTuning tuning, ISessionProgress progress)
        {
            this.Kind = For(tuning, progress.Round, progress.Mode);
            // The boss starts across the floor from the player, who is first, nearest the camera.
            this.BossIndex = Kind == SpecialRound.Boss ? 1 + RoundDifficulty.BotCountFor(tuning, progress) / 2 : -1;
            // A fall takes 1 / sqrt(g) as long, so a push off the rim under low gravity still counts as a knock-out.
            this.FallTimeScale = Kind == SpecialRound.LowGravity
                ? 1f / UnityEngine.Mathf.Sqrt(tuning.SpecialRounds.GravityScale)
                : 1f;
        }

        /// <summary>How many times longer a fall takes this round: the knock-out credit windows stretch with it.</summary>
        public float FallTimeScale { get; }

        public SpecialRound Kind { get; }

        public bool IsSpecial => Kind != SpecialRound.None;

        /// <summary>Whether this round is the HOT POTATO mode's: a fenced floor, the bomb, and nothing else.</summary>
        public bool IsHotPotato => Kind == SpecialRound.HotPotato;

        /// <summary>The boss's car index in a boss round (1..N), -1 in any other.</summary>
        public int BossIndex { get; }

        /// <summary>
        /// The special round of the visit's round <paramref name="round"/> in this build, in the menu's
        /// <paramref name="mode"/>: none in a match, the mode's own for an event mode, else the rotation's.
        /// </summary>
        public static SpecialRound For(GameTuning tuning, int round, EventMode mode)
        {
            if (tuning.IsMatch) {
                return SpecialRound.None;
            }

            return mode == EventMode.HotPotato ? SpecialRound.HotPotato : tuning.SpecialRounds.For(round);
        }

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
