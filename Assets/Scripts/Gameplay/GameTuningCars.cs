using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// What a car of the catalog does for the player beside its looks: its speed, power and boost, each from 1 to 5,
    /// shown in the garage and on the popup that opens it. 3 is the tuned car of <see cref="CarSettings"/> and
    /// <see cref="BoostSettings"/>; each point off it moves the player's car a small step, so a later car feels better
    /// without leaving the bots behind. Only the player's car reads them: a bot's car is its look.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Car stats")]
        [Tooltip("Each catalog car's speed, power and boost for the player, and what one point does")]
        [SerializeField]
        private CarStatSettings carStats = new();

        public CarStatSettings CarStats => carStats;

        [Serializable]
        public sealed class CarStatSettings
        {
            /// <summary>The stat every car has where its row is missing, and the middle of the scale.</summary>
            public const int Middle = 3;

            /// <summary>The most points a stat has.</summary>
            public const int Max = 5;

            [Tooltip("Share of the top speed and acceleration one point of speed adds above 3, or takes below")]
            [Range(0f, 0.2f)]
            [SerializeField]
            private float speedStep = 0.03f;

            [Tooltip("Share of the push of the player's hits one point of power adds above 3, or takes below")]
            [Range(0f, 0.3f)]
            [SerializeField]
            private float powerStep = 0.08f;

            [Tooltip("Share of the boost's cooldown one point of boost takes off above 3, or adds below")]
            [Range(0f, 0.2f)]
            [SerializeField]
            private float boostStep = 0.08f;

            [Tooltip("Each car's stats by its catalog id, 1 to 5")]
            [SerializeField]
            private CarStatLine[] cars = {
                new("racer", 3, 2, 3),
                new("speedster", 5, 2, 2),
                new("vintage", 3, 3, 3),
                new("suv", 2, 4, 3),
                new("drag-racer", 5, 2, 4),
                new("truck", 2, 5, 3),
                new("monster-truck", 3, 5, 4),
            };

            /// <summary>The car's stats; the middle of the scale for a car with no row.</summary>
            public CarStatLine Of(string id)
            {
                foreach (var car in cars) {
                    if (car.Id == id) {
                        return car;
                    }
                }

                return new CarStatLine(id, Middle, Middle, Middle);
            }

            /// <summary>Times the tuned top speed and acceleration the player's car drives at.</summary>
            public float SpeedScale(CarStatLine stats) => 1f + speedStep * (stats.Speed - Middle);

            /// <summary>Times the push of the player's hits.</summary>
            public float PowerScale(CarStatLine stats) => 1f + powerStep * (stats.Power - Middle);

            /// <summary>Times the tuned boost cooldown.</summary>
            public float CooldownScale(CarStatLine stats) => Mathf.Max(0.1f, 1f - boostStep * (stats.Boost - Middle));
        }

        [Serializable]
        public sealed class CarStatLine
        {
            [Tooltip("The car's id in the catalog")]
            [SerializeField]
            private string id;

            [Range(1, CarStatSettings.Max)]
            [SerializeField]
            private int speed = CarStatSettings.Middle;

            [Range(1, CarStatSettings.Max)]
            [SerializeField]
            private int power = CarStatSettings.Middle;

            [Range(1, CarStatSettings.Max)]
            [SerializeField]
            private int boost = CarStatSettings.Middle;

            public CarStatLine(string id, int speed, int power, int boost)
            {
                this.id = id;
                this.speed = speed;
                this.power = power;
                this.boost = boost;
            }

            public string Id => id;
            public int Speed => speed;
            public int Power => power;
            public int Boost => boost;
        }
    }
}
