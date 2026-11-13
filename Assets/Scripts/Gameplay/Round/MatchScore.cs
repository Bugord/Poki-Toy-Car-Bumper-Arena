using System;
using System.Collections.Generic;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The score of a timed round, by car index, without keeping the cars: points, knock-outs, falls, how many of them
    /// were knock-outs by a car, and when each car reached its points. In the long match a knock-out is the point
    /// (<see cref="ModeRules.PointsPerKnockout"/> 1); an event mode adds its own (<see cref="AddPoints"/>: the crown's
    /// seconds, coins, goals). The ranking is the most points, then the fewest falls (a fall costs no points, only
    /// time), then whoever reached the points first; cars level on all three go by index, so the places are always
    /// 1..N, one each.
    /// <para>
    /// With teams (<see cref="ModeRules.Teams"/>) each car is on the team of its index (<see cref="TeamOf"/>: the
    /// player's is team 0, the bots alternate) and a team's points are its cars' sum; a mode on top of it only colours
    /// the cars and adds points.
    /// </para>
    /// </summary>
    public sealed class MatchScore
    {
        private readonly int[] points;
        private readonly int pointsPerKnockout;
        private readonly int teams;
        private readonly int[] knockouts;
        private readonly int[] falls;
        private readonly int[] knockedOff;
        private readonly float[] scoredAt;
        private readonly int[] teamPoints;

        // The indices best first, kept sorted as the score changes, so the HUD can read the leader every frame.
        private readonly int[] order;

        public MatchScore(int carCount, int pointsPerKnockout = 1, int teams = 0)
        {
            this.pointsPerKnockout = pointsPerKnockout;
            this.teams = teams;
            points = new int[carCount];
            knockouts = new int[carCount];
            falls = new int[carCount];
            knockedOff = new int[carCount];
            scoredAt = new float[carCount];
            teamPoints = new int[Math.Max(0, teams)];
            order = new int[carCount];
            for (var index = 0; index < carCount; index++) {
                order[index] = index;
            }
        }

        public int Count => order.Length;

        /// <summary>
        /// The car first in the ranking; the player's (0) while nobody has scored.
        /// </summary>
        public int Leader => order.Length > 0 ? order[0] : RoundResult.Nobody;

        /// <summary>
        /// The car at a place in the ranking, from 0 for the leader: the HUD's scoreboard reads the order without
        /// a snapshot, so a score change costs it no allocation.
        /// </summary>
        public int IndexAt(int place) => order[place];

        public int KnockoutsOf(int index) => knockouts[index];

        /// <summary>The points the ranking goes by: the knock-outs in the long match, the mode's own in an event mode.</summary>
        public int PointsOf(int index) => points[index];

        /// <summary>Raised after a car's points changed (<see cref="AddPoints"/> or a knock-out), with its index.</summary>
        public event Action<int> Changed;

        /// <summary>The number of teams, 0 for none.</summary>
        public int Teams => teams;

        /// <summary>The team of a car, from 0 (the player's); -1 without teams.</summary>
        public int TeamOf(int index) => RoundTeams.TeamOf(index, teams);

        /// <summary>
        /// Raised after a team's points changed, with the team and by how much: a point of one of its cars, or one the
        /// team gets as a whole (<see cref="AddTeamPoints"/>). The team HUD follows it.
        /// </summary>
        public event Action<int, int> TeamChanged;

        /// <summary>
        /// The points a team has as a whole, on top of its cars' own: a fall nobody on the team is credited with (a car of
        /// the other team that drove off alone), an own goal.
        /// </summary>
        public int TeamOwnPointsOf(int team) => team >= 0 && team < teamPoints.Length ? teamPoints[team] : 0;

        /// <summary>
        /// Adds <paramref name="amount"/> points to a team as a whole, credited to none of its cars (never below 0): its
        /// cars' own points stay their personal score.
        /// </summary>
        public void AddTeamPoints(int team, int amount)
        {
            if (amount == 0 || team < 0 || team >= teamPoints.Length) {
                return;
            }

            var before = TeamPointsOf(team);
            teamPoints[team] = Math.Max(0, teamPoints[team] + amount);
            var change = TeamPointsOf(team) - before;
            if (change != 0) {
                TeamChanged?.Invoke(team, change);
            }
        }

        /// <summary>Every team's points, by team: the result's score.</summary>
        public int[] TeamScores()
        {
            var scores = new int[teams];
            for (var team = 0; team < teams; team++) {
                scores[team] = TeamPointsOf(team);
            }

            return scores;
        }

        /// <summary>The sum of a team's points: its cars' and its own.</summary>
        public int TeamPointsOf(int team)
        {
            var sum = TeamOwnPointsOf(team);
            for (var index = 0; index < points.Length; index++) {
                if (TeamOf(index) == team) {
                    sum += points[index];
                }
            }

            return sum;
        }

        /// <summary>
        /// The team ahead on points, -1 for a draw or without teams.
        /// </summary>
        public int LeadingTeam
        {
            get
            {
                var best = -1;
                var bestPoints = int.MinValue;
                var isDraw = false;
                for (var team = 0; team < teams; team++) {
                    var sum = TeamPointsOf(team);
                    if (sum > bestPoints) {
                        best = team;
                        bestPoints = sum;
                        isDraw = false;
                    }
                    else if (sum == bestPoints) {
                        isDraw = true;
                    }
                }

                return isDraw ? -1 : best;
            }
        }

        /// <summary>
        /// Adds <paramref name="amount"/> points to a car (negative takes them away, never below 0), at
        /// <paramref name="time"/> into the round: an event mode's own scoring.
        /// </summary>
        public void AddPoints(int index, int amount, float time)
        {
            if (amount == 0 || index < 0 || index >= points.Length) {
                return;
            }

            var before = points[index];
            points[index] = Math.Max(0, points[index] + amount);
            if (amount > 0) {
                scoredAt[index] = time;
            }

            Sort();
            Changed?.Invoke(index);
            if (teams > 0 && points[index] != before) {
                TeamChanged?.Invoke(TeamOf(index), points[index] - before);
            }
        }

        public int FallsOf(int index) => falls[index];

        /// <summary>
        /// The falls of the car that another car gets the credit for.
        /// </summary>
        public int KnockedOffOf(int index) => knockedOff[index];

        /// <summary>
        /// Counts a fall of <paramref name="index"/>, and a point for <paramref name="by"/> when a car gets the credit
        /// (<see cref="RoundResult.Nobody"/> for a fall of its own), at <paramref name="time"/> into the match.
        /// </summary>
        public void RecordFall(int index, int by, float time)
        {
            falls[index]++;
            var isScored = by != RoundResult.Nobody && by != index;
            if (isScored) {
                knockouts[by]++;
                knockedOff[index]++;
                if (pointsPerKnockout != 0) {
                    points[by] += pointsPerKnockout;
                    scoredAt[by] = time;
                }
            }

            Sort();
            if (isScored && pointsPerKnockout != 0) {
                Changed?.Invoke(by);
                if (teams > 0) {
                    TeamChanged?.Invoke(TeamOf(by), pointsPerKnockout);
                }
            }
        }

        private void Sort()
        {
            // Insertion sort: a handful of cars, and no allocation.
            for (var i = 1; i < order.Length; i++) {
                var current = order[i];
                var slot = i;
                while (slot > 0 && Compare(current, order[slot - 1]) < 0) {
                    order[slot] = order[slot - 1];
                    slot--;
                }

                order[slot] = current;
            }
        }

        /// <summary>
        /// The car's place, 1 for the leader.
        /// </summary>
        public int PlaceOf(int index)
        {
            return Array.IndexOf(order, index) + 1;
        }

        /// <summary>
        /// The standings in place order; a match has no order of going out, so that is left at 0.
        /// </summary>
        public IReadOnlyList<RoundStanding> Snapshot()
        {
            var entries = new RoundStanding[order.Length];
            for (var place = 0; place < order.Length; place++) {
                var index = order[place];
                entries[place] = new RoundStanding(index, place + 1, knockouts[index], 0, points[index], TeamOf(index));
            }

            return Array.AsReadOnly(entries);
        }

        private int Compare(int a, int b)
        {
            var result = points[b].CompareTo(points[a]);
            if (result == 0) {
                result = falls[a].CompareTo(falls[b]);
            }

            if (result == 0) {
                result = scoredAt[a].CompareTo(scoredAt[b]);
            }

            return result != 0 ? result : a.CompareTo(b);
        }
    }
}
