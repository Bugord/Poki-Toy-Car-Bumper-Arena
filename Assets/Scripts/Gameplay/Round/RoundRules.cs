namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The special rule of this scene's round (<see cref="GameTuning.SpecialRoundSettings"/>), read from the session's
    /// progress once, as the scene is built, like the difficulty: the lobby before it named the same one. Only a build
    /// of last-car-standing rounds has them. <see cref="SpecialRoundPlay"/> puts the rule on the arena; the round asks
    /// here which car is the boss.
    /// <para>
    /// The menu's event mode (<see cref="ISessionProgress.Mode"/>) wins over the rotation: an event mode makes every
    /// round its own kind (<see cref="EventModes.KindOf"/>), and the rotation plays one now and then. What the round's
    /// kind changes in the shared round it states in <see cref="Mode"/> (<see cref="GameTuning.RulesOf"/>): how cars
    /// fall, the fence, the floor, mechanics and boxes, how the round ends and is scored, teams, the number of cars and
    /// the edge assist. A mode's own logic checks <see cref="Kind"/> itself.
    /// </para>
    /// </summary>
    public sealed class RoundRules
    {
        public RoundRules(GameTuning tuning, ISessionProgress progress)
        {
            this.Kind = For(tuning, progress.Round, progress.Mode);
            this.Mode = tuning.RulesOf(Kind);
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

        /// <summary>
        /// The rules this round plays (<see cref="ModeRules"/>): the plain round's, the build's long match's or an event
        /// mode's.
        /// </summary>
        public ModeRules Mode { get; }

        /// <summary>Whether this round is an event mode's rather than a plain or special round.</summary>
        public bool IsEvent => EventModes.IsEvent(Kind);

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

            return mode != EventMode.None ? EventModes.KindOf(mode) : tuning.SpecialRounds.For(round);
        }

        /// <summary>The kind of the session's next round, as the scene built for it will read it.</summary>
        public static SpecialRound KindFor(GameTuning tuning, ISessionProgress progress) =>
            For(tuning, progress.Round, progress.Mode);

        /// <summary>
        /// The rules of the session's next round, before its scene is built: the lobby, the roster and the difficulty
        /// read the same ones as the round.
        /// </summary>
        public static ModeRules RulesFor(GameTuning tuning, ISessionProgress progress) =>
            tuning.RulesOf(KindFor(tuning, progress));

        /// <summary>The id of a special round for analytics and the string table: no numbers, never changed.</summary>
        public static string IdOf(SpecialRound kind) => kind switch {
            SpecialRound.Mega => "mega",
            SpecialRound.HotPotato => "hot-potato",
            SpecialRound.Boss => "boss",
            SpecialRound.Ice => "ice",
            SpecialRound.LowGravity => "low-gravity",
            SpecialRound.Crown => "crown",
            SpecialRound.GoldRush => "gold-rush",
            SpecialRound.ItemFrenzy => "item-frenzy",
            SpecialRound.Overheat => "overheat",
            SpecialRound.TeamPush => "team-push",
            SpecialRound.Football => "football",
            _ => "none",
        };
    }
}
