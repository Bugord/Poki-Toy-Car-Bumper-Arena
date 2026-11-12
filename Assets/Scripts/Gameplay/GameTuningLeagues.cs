using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The league rewards (<see cref="LeagueRewardSettings"/>): each league past the first gives a car and a hat sold
    /// nowhere, so reaching it is something to show off. Apart from the main file, as the cars' and hats' own sections
    /// only list them.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("League rewards")]
        [Tooltip("The car and the hat each league gives, by the level that starts the league")]
        [SerializeField]
        private LeagueRewardSettings leagueRewards = new();

        public LeagueRewardSettings LeagueRewards => leagueRewards;

        /// <summary>
        /// What reaching each league gives: its car (in the catalog with the league's level as its unlock level, free,
        /// so the level opens it as any free car) and its hat (one of <see cref="TopperSettings.League"/>, owned from the
        /// level on). Neither is sold, and a visit's bot drives or wears one only when its own level has reached the
        /// league, so the rewards read as the league's.
        /// </summary>
        [Serializable]
        public sealed class LeagueRewardSettings
        {
            [Tooltip("One row a league past the first: the level that starts it (the leagues' levels in the UI), its car " +
                "and its hat; ids have no digits")]
            [SerializeField]
            private LeagueReward[] leagues = {
                new(10, "hot-rod", "ember"),
                new(25, "police", "silver-torch"),
                new(50, "dozer", "fire-crown"),
                new(100, "comet", "phoenix"),
            };

            public IReadOnlyList<LeagueReward> All => leagues;

            /// <summary>The league that starts at <paramref name="level"/>, or null when none does.</summary>
            public LeagueReward AtLevel(int level)
            {
                foreach (var league in leagues) {
                    if (league.Level == level) {
                        return league;
                    }
                }

                return null;
            }

            /// <summary>The level of the league whose car this is, or 0 for a car no league gives.</summary>
            public int LevelOfCar(string id)
            {
                foreach (var league in leagues) {
                    if (league.Car == id) {
                        return league.Level;
                    }
                }

                return 0;
            }

            /// <summary>The level of the league whose hat this is, or 0 for a hat no league gives.</summary>
            public int LevelOfTopper(string id)
            {
                foreach (var league in leagues) {
                    if (league.Topper == id) {
                        return league.Level;
                    }
                }

                return 0;
            }

            /// <summary>
            /// Whether someone at <paramref name="level"/> may drive the car: any car no league gives, or one whose
            /// league the level has reached.
            /// </summary>
            public bool AllowsCar(string id, int level) => LevelOfCar(id) <= level;

            /// <summary>The same for a hat.</summary>
            public bool AllowsTopper(string id, int level) => LevelOfTopper(id) <= level;
        }

        [Serializable]
        public sealed class LeagueReward
        {
            [Tooltip("The level that starts the league")]
            [Min(2)]
            [SerializeField]
            private int level;

            [Tooltip("The car's id in the catalog")]
            [SerializeField]
            private string car;

            [Tooltip("The hat's id in TopperSettings' league list")]
            [SerializeField]
            private string topper;

            public LeagueReward(int level, string car, string topper)
            {
                this.level = level;
                this.car = car;
                this.topper = topper;
            }

            public int Level => level;
            public string Car => car;
            public string Topper => topper;
        }
    }
}
