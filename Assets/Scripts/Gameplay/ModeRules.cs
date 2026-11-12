namespace ToyCarArena.Gameplay
{
    /// <summary>What happens to a car that drops below the round's fall height.</summary>
    public enum FallRule
    {
        /// <summary>It is out of the round: the last car standing.</summary>
        Out,

        /// <summary>
        /// Nobody falls: a car that got past the fence after all is put straight back on the floor (the hot potato).
        /// </summary>
        PutBack,

        /// <summary>
        /// It comes back a moment later, away from the others, safe for a moment (the long match): a fall only costs
        /// time and score.
        /// </summary>
        Respawn,
    }

    /// <summary>What the floor's edge does over the round.</summary>
    public enum FloorRule
    {
        /// <summary>
        /// It crumbles away once, wave by wave, down to the core (in cycles only when
        /// <see cref="GameTuning.FloorCycleSettings.Enabled"/> asks, for the tests).
        /// </summary>
        Crumble,

        /// <summary>
        /// It goes in cycles, the outer waves falling and coming back (<see cref="GameTuning.FloorCycles"/>), and the
        /// arena's variants turn with them: the long match.
        /// </summary>
        Cycles,

        /// <summary>It stays whole the whole round.</summary>
        Still,
    }

    /// <summary>The boxes with items on the floor (<see cref="GameTuning.Pickups"/>).</summary>
    public enum PickupRule
    {
        /// <summary>No boxes.</summary>
        None,

        /// <summary>From the visit's <see cref="GameTuning.PickupSettings.FirstRound"/> on, as a plain round has them.</summary>
        Usual,

        /// <summary>From the first round of the visit on, as a match has them.</summary>
        Always,
    }

    /// <summary>
    /// The rules of one kind of round, the plain round, the build's long match and every event mode alike: how cars
    /// fall, whether a fence stands, what the floor and its mechanics do, the boxes, how the round ends and is scored,
    /// teams, how many cars and whether the player's edge assist helps. Everything shared that a mode changes reads it
    /// here (<see cref="Round.RoundRules.Mode"/>), so a new mode changes the shared code by its numbers, not by an
    /// <c>if</c> of its own. A mode's own settings build its rules (<see cref="GameTuning.RulesOf"/>).
    /// <para>
    /// Immutable: <see cref="With"/> copies it with some rules changed, the way a mode states how it differs from the
    /// plain round.
    /// </para>
    /// </summary>
    public sealed class ModeRules
    {
        /// <summary>The plain round of last car standing: the rotation's special rounds keep it too.</summary>
        public static readonly ModeRules Plain = new();

        private ModeRules()
        {
        }

        public FallRule Fall { get; private set; } = FallRule.Out;

        /// <summary>A low fence with unseen walls stands round the rim and every hole (<see cref="Arena.ArenaFence"/>).</summary>
        public bool HasFence { get; private set; }

        public FloorRule Floor { get; private set; } = FloorRule.Crumble;

        /// <summary>The arena variant's mechanic (the ball, the turntable, the crab...) plays; else none does.</summary>
        public bool HasMechanics { get; private set; } = true;

        public PickupRule Pickups { get; private set; } = PickupRule.Usual;

        /// <summary>How many times as often boxes come as the usual pace: more than 1 for a frenzy of them.</summary>
        public float PickupPace { get; private set; } = 1f;

        /// <summary>How many boxes may lie on the floor at once; 0 for the usual number.</summary>
        public int PickupMax { get; private set; }

        /// <summary>
        /// Seconds the round lasts when it is timed (<see cref="IsTimed"/>): it ends only when time runs out and is
        /// decided by the score. 0 for a round of last car standing, which ends when the player is out, is the last car
        /// left or the round's time runs out.
        /// </summary>
        public float Duration { get; private set; }

        public bool IsTimed => Duration > 0f;

        /// <summary>
        /// The points a knock-out scores in a timed round: 1 in the match, where knock-outs are the score, 0 in a mode
        /// that scores something else (<see cref="Round.ArenaRound.AddPoints"/>).
        /// </summary>
        public int PointsPerKnockout { get; private set; }

        /// <summary>The number of teams, 0 for none or 2: then the round has an even number of cars.</summary>
        public int Teams { get; private set; }

        /// <summary>
        /// How many bots the round brings, whatever the difficulty would; 0 for the difficulty's own number. With teams
        /// the count is made even with the player's car either way.
        /// </summary>
        public int Bots { get; private set; }

        /// <summary>Whether the player's edge assist and edge hold help (the difficulty's share), or never.</summary>
        public bool HasEdgeAssist { get; private set; } = true;

        /// <summary>
        /// The most knock-outs a timed round pays XP and coins for: a mode where cars come back is full of knock-outs.
        /// 0 for no cap.
        /// </summary>
        public int KnockoutRewardCap { get; private set; }

        /// <summary>
        /// The worst place of a timed round that is not a loss (the losing streak, the jingle); 0 for half the cars.
        /// </summary>
        public int TopPlaces { get; private set; }

        /// <summary>The car falls and stays out; a car that is put back or comes back is never out for good.</summary>
        public bool IsKnockOutFinal => Fall == FallRule.Out;

        /// <summary>
        /// The rival lives within the round: none at the start, the car that knocks the player off becomes it. With
        /// respawns, where the player is knocked off many times; else the visit's rival from the round before.
        /// </summary>
        public bool HasRoundRival => Fall == FallRule.Respawn;

        /// <summary>The worst place of a timed round of <paramref name="cars"/> that is not a loss.</summary>
        public int TopPlacesOf(int cars) => TopPlaces > 0 ? TopPlaces : UnityEngine.Mathf.Max(1, cars / 2);

        /// <summary>
        /// The bots of a round whose difficulty brings <paramref name="difficultyBots"/>, at most
        /// <paramref name="maxBots"/>: the mode's own number, made even with the player's car when there are teams.
        /// </summary>
        public int BotCountFor(int difficultyBots, int maxBots)
        {
            var bots = Bots > 0 ? Bots : difficultyBots;
            if (Teams > 0 && (bots + 1) % Teams != 0) {
                bots = bots + 1 <= maxBots ? bots + 1 : bots - 1;
            }

            return UnityEngine.Mathf.Clamp(bots, 1, maxBots);
        }

        /// <summary>The build's long match (<see cref="GameTuning.IsMatch"/>), by the same rules as a mode.</summary>
        public static ModeRules Match(GameTuning tuning) => Plain.With(fall: FallRule.Respawn, floor: FloorRule.Cycles,
            pickups: PickupRule.Always, duration: tuning.Match.Duration, pointsPerKnockout: 1,
            topPlaces: tuning.Match.TopPlaces);

        /// <summary>A copy with the rules given changed, the others as they are here.</summary>
        public ModeRules With(FallRule? fall = null, bool? fence = null, FloorRule? floor = null, bool? mechanics = null,
            PickupRule? pickups = null, float? pickupPace = null, int? pickupMax = null, float? duration = null,
            int? pointsPerKnockout = null, int? teams = null, int? bots = null, bool? edgeAssist = null,
            int? knockoutRewardCap = null, int? topPlaces = null)
        {
            return new ModeRules {
                Fall = fall ?? Fall,
                HasFence = fence ?? HasFence,
                Floor = floor ?? Floor,
                HasMechanics = mechanics ?? HasMechanics,
                Pickups = pickups ?? Pickups,
                PickupPace = pickupPace ?? PickupPace,
                PickupMax = pickupMax ?? PickupMax,
                Duration = duration ?? Duration,
                PointsPerKnockout = pointsPerKnockout ?? PointsPerKnockout,
                Teams = teams ?? Teams,
                Bots = bots ?? Bots,
                HasEdgeAssist = edgeAssist ?? HasEdgeAssist,
                KnockoutRewardCap = knockoutRewardCap ?? KnockoutRewardCap,
                TopPlaces = topPlaces ?? TopPlaces,
            };
        }
    }
}
