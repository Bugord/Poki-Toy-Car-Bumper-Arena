using System;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Round;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The TEAM PUSH event mode: blue (the player's team) against red, three against three, for a minute
    /// (<see cref="GameTuning.TeamPushSettings.Rules"/>). Every car that falls is a point for the other team and comes
    /// back a moment later, safe for a moment, as in the long match. A fall a car of the other team is credited with is
    /// that car's knock-out, scored by the shared round (its points are the car's personal score); a fall nobody is
    /// credited with (off on its own, or after a teammate's bump, which is no hit) goes to the other team as a whole
    /// (<see cref="ArenaRound.AddTeamPoints"/>). The team with more points when time runs out wins, level is a draw.
    /// <para>
    /// Everything else is the shared team layer (<see cref="RoundTeams"/>, <c>Car.IsTeammateOf</c>,
    /// <see cref="GameTuning.Teams"/>): team colours, the teams starting face to face, no friendly fire, the bots that
    /// leave their teammates alone, help a pushed one and spread over the other team. The bots play no way of their own
    /// (<see cref="SteerBot"/> is false), the usual target picking already plays as a team.
    /// </para>
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.TeamPush"/>), silent in every other: nothing subscribed, nothing drawn.
    /// </para>
    /// </summary>
    public sealed class TeamPushMode : IRoundMode, IBotSteering, IInitializable, IDisposable
    {
        private readonly GameTuning.TeamPushSettings settings;
        private readonly ArenaRound round;

        public TeamPushMode(GameTuning tuning, RoundRules rules, ArenaRound round)
        {
            this.settings = tuning.TeamPush;
            this.round = round;
            this.IsActive = rules.Kind == SpecialRound.TeamPush;
        }

        public SpecialRound Kind => SpecialRound.TeamPush;

        /// <summary>The card's picture: a blue SUV head-on against a red one.</summary>
        public ModeCard Card { get; } = new("suv", RoundTeams.PaintOf(RoundTeams.Blue), rivalCarId: "suv",
            rivalColor: RoundTeams.PaintOf(RoundTeams.Red));

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.TeamPushSettings Settings => settings;

        /// <summary>The points given for falls no car of the other team was credited with, in all: for the tests.</summary>
        public int TeamFallPoints { get; private set; }

        public void Initialize()
        {
            if (IsActive) {
                round.Eliminated += OnEliminated;
            }
        }

        public void Dispose()
        {
            round.Eliminated -= OnEliminated;
        }

        /// <summary>The bots play the shared team play: no way of their own.</summary>
        public bool SteerBot(Bot bot, IArenaEdge edge, float deltaTime) => false;

        private void OnEliminated(int index, int by)
        {
            // A knock-out by a car of the other team is already its point; any other fall is the other team's as a whole.
            if (by != RoundResult.Nobody && round.TeamOf(by) != round.TeamOf(index)) {
                return;
            }

            var other = round.TeamOf(index) == RoundTeams.Blue ? RoundTeams.Red : RoundTeams.Blue;
            if (round.AddTeamPoints(other, settings.PointsPerFall)) {
                TeamFallPoints += settings.PointsPerFall;
            }
        }
    }
}
