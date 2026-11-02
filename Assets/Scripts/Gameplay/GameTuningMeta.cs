using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The goals around the rounds, apart from the main file because they change together: coins and what they buy
    /// (<see cref="CoinSettings"/>, <see cref="TopperSettings"/>), round missions (<see cref="MissionSettings"/>) and
    /// win streaks inside a visit (<see cref="StreakSettings"/>). None of them touch the physics or the bots' driving.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Coins")]
        [Tooltip("Coins a round gives, saved between visits; they buy toppers in the garage")]
        [SerializeField]
        private CoinSettings coins = new();

        [Header("Streaks")]
        [Tooltip("Wins in a row inside a visit: when a streak starts and what it adds to the next round")]
        [SerializeField]
        private StreakSettings streaks = new();

        [Header("Missions")]
        [Tooltip("Three goals at a time, one per slot; a done mission gives XP and coins and is replaced at once")]
        [SerializeField]
        private MissionSettings missions = new();

        [Header("Toppers")]
        [Tooltip("The roof toppers the garage sells for coins")]
        [SerializeField]
        private TopperSettings toppers = new();

        public CoinSettings Coins => coins;
        public StreakSettings Streaks => streaks;
        public MissionSettings Missions => missions;
        public TopperSettings Toppers => toppers;

        /// <summary>
        /// A round's coins: about 30–100, plus the missions it completes. Generous on purpose: about three minutes of play
        /// (four or five rounds) buy most of the shop, the cheap toppers and the first paid cars, and only the crown
        /// and the last cars stay long goals.
        /// </summary>
        [Serializable]
        public sealed class CoinSettings
        {
            [Tooltip("Coins for every finished round, whatever its outcome")]
            [Min(0)]
            [SerializeField]
            private int roundCoins = 30;

            [Tooltip("Coins for each knock-out credited to the player")]
            [Min(0)]
            [SerializeField]
            private int knockoutCoins = 15;

            [Tooltip("Extra coins for being the last car standing")]
            [Min(0)]
            [SerializeField]
            private int winCoins = 30;

            [Tooltip("Cars bought for coins once their level opens them, every other one of the catalog: the first is " +
                "cheap enough for the coins a new player has after one round, with some left over; the last two are long goals")]
            [SerializeField]
            private CarPrice[] carPrices = {
                new("speedster", 30),
                new("suv", 80),
                new("truck", 150),
                new("monster-truck", 300),
            };

            public int RoundCoins => roundCoins;
            public int KnockoutCoins => knockoutCoins;
            public int WinCoins => winCoins;

            /// <summary>What the car costs in coins once open; 0 for one its level alone gives.</summary>
            public int PriceOf(string carId)
            {
                foreach (var car in carPrices) {
                    if (car.Id == carId) {
                        return car.Price;
                    }
                }

                return 0;
            }
        }

        /// <summary>
        /// A streak counts wins in a row; taking REVENGE counts as a win and starts one by itself. From
        /// <see cref="Start"/> the next round's XP and coins are multiplied, a crown sits on the player's car and the bots
        /// go a little more for its leader. A fall without REVENGE ends it; a round survived to the time keeps it.
        /// </summary>
        [Serializable]
        public sealed class StreakSettings
        {
            [Tooltip("Streak length from which the next round is a streak round")]
            [Min(1)]
            [SerializeField]
            private int start = 2;

            [Tooltip("What a streak round's own XP and coins are multiplied by; mission rewards are not")]
            [Min(1f)]
            [SerializeField]
            private float multiplier = 1.5f;

            public int Start => start;
            public float Multiplier => multiplier;

            /// <summary>The extra a streak round adds to <paramref name="amount"/>, rounded to the nearest whole.</summary>
            public int Bonus(int amount) => Mathf.RoundToInt(amount * (multiplier - 1f));
        }

        /// <summary>
        /// The mission pool and its rewards by tier. A slot draws only its own kind: the round slot asks for something in
        /// one round, the total slot adds up over rounds and visits, and the lesson slot teaches an item or a rule. The
        /// starter of each slot comes first, so a first round nearly always shows a done mission.
        /// </summary>
        [Serializable]
        public sealed class MissionSettings
        {
            [Tooltip("XP for a done mission of each tier: easy, medium, hard")]
            [SerializeField]
            private int[] tierXp = { 30, 45, 60 };

            [Tooltip("Coins for a done mission of each tier: easy, medium, hard")]
            [SerializeField]
            private int[] tierCoins = { 40, 60, 90 };

            [Tooltip("Every mission that can be drawn; ids are saved and measured, so they never change and carry no " +
                "digits")]
            [SerializeField]
            private MissionDefinition[] pool = DefaultPool();

            public IReadOnlyList<MissionDefinition> Pool => pool;

            public int XpOf(MissionTier tier) => tierXp[Mathf.Clamp((int)tier, 0, tierXp.Length - 1)];

            public int CoinsOf(MissionTier tier) => tierCoins[Mathf.Clamp((int)tier, 0, tierCoins.Length - 1)];

            private static MissionDefinition[] DefaultPool() => new[] {
                new MissionDefinition("final-two", MissionSlot.Round, MissionCounter.FinalTwo, 1, MissionTier.Easy,
                    starter: true),
                new MissionDefinition("round-boosts", MissionSlot.Round, MissionCounter.Boosts, 4, MissionTier.Easy),
                new MissionDefinition("round-knockouts", MissionSlot.Round, MissionCounter.Knockouts, 2,
                    MissionTier.Medium),
                new MissionDefinition("round-win", MissionSlot.Round, MissionCounter.Wins, 1, MissionTier.Medium),
                new MissionDefinition("round-hat-trick", MissionSlot.Round, MissionCounter.Knockouts, 3, MissionTier.Hard),
                new MissionDefinition("first-round", MissionSlot.Total, MissionCounter.Rounds, 1, MissionTier.Easy,
                    starter: true, starterOnly: true),
                new MissionDefinition("rounds", MissionSlot.Total, MissionCounter.Rounds, 3, MissionTier.Easy),
                new MissionDefinition("knockouts", MissionSlot.Total, MissionCounter.Knockouts, 6, MissionTier.Medium),
                new MissionDefinition("wins", MissionSlot.Total, MissionCounter.Wins, 3, MissionTier.Hard),
                new MissionDefinition("arena-tabletop", MissionSlot.Total, MissionCounter.ArenaRounds, 2,
                    MissionTier.Easy, map: "tabletop"),
                new MissionDefinition("arena-candy", MissionSlot.Total, MissionCounter.ArenaRounds, 2, MissionTier.Easy,
                    map: "candy"),
                new MissionDefinition("arena-playroom", MissionSlot.Total, MissionCounter.ArenaRounds, 2,
                    MissionTier.Easy, map: "playroom"),
                new MissionDefinition("revenge", MissionSlot.Lesson, MissionCounter.Revenge, 1, MissionTier.Medium,
                    starter: true),
                new MissionDefinition("items", MissionSlot.Lesson, MissionCounter.ItemsUsed, 3, MissionTier.Easy),
                new MissionDefinition("boxes", MissionSlot.Lesson, MissionCounter.Boxes, 4, MissionTier.Easy),
                new MissionDefinition("item-knockout", MissionSlot.Lesson, MissionCounter.ItemKnockouts, 1,
                    MissionTier.Hard),
            };
        }

        /// <summary>One mission of the pool: what it counts, how many, in which slot and how hard it is.</summary>
        [Serializable]
        public sealed class MissionDefinition
        {
            [Tooltip("Saved and measured as is: lowercase, words joined by '-', no digits")]
            [SerializeField]
            private string id;

            [SerializeField]
            private MissionSlot slot;

            [SerializeField]
            private MissionCounter counter;

            [Min(1)]
            [SerializeField]
            private int target = 1;

            [SerializeField]
            private MissionTier tier;

            [Tooltip("Drawn first into its slot when a player has no missions yet")]
            [SerializeField]
            private bool starter;

            [Tooltip("Only ever the first mission of its slot, never drawn again")]
            [SerializeField]
            private bool starterOnly;

            [Tooltip("For rounds on an arena: its id; the mission is drawn only once the arena is open")]
            [SerializeField]
            private string map = "";

            public MissionDefinition(string id, MissionSlot slot, MissionCounter counter, int target, MissionTier tier,
                bool starter = false, bool starterOnly = false, string map = "")
            {
                this.id = id;
                this.slot = slot;
                this.counter = counter;
                this.target = target;
                this.tier = tier;
                this.starter = starter;
                this.starterOnly = starterOnly;
                this.map = map;
            }

            public string Id => id;
            public MissionSlot Slot => slot;
            public MissionCounter Counter => counter;
            public int Target => Mathf.Max(1, target);
            public MissionTier Tier => tier;
            public bool IsStarter => starter;
            public bool IsStarterOnly => starterOnly;
            public string Map => map ?? "";

            /// <summary>Whether it counts items, which a round of a visit has only from <see cref="PickupSettings.FirstRound"/>.</summary>
            public bool NeedsItems => counter is MissionCounter.ItemsUsed or MissionCounter.Boxes or
                MissionCounter.ItemKnockouts;

            /// <summary>The round slot asks for it inside one round; the other slots add up across rounds.</summary>
            public bool IsInOneRound => slot == MissionSlot.Round;
        }

        /// <summary>The roof toppers on sale, in the garage's order. Their looks are built in code by id.</summary>
        [Serializable]
        public sealed class TopperSettings
        {
            [Tooltip("Each topper's id (saved and measured: no digits) and price in coins")]
            [SerializeField]
            private TopperDefinition[] toppers = {
                new("party-hat", 40),
                new("cone", 40),
                new("antenna", 60),
                new("flag", 60),
                new("horns", 90),
                new("propeller", 90),
                new("fin", 180),
                new("crown", 300),
            };

            public IReadOnlyList<TopperDefinition> All => toppers;

            public TopperDefinition Find(string id)
            {
                foreach (var topper in toppers) {
                    if (topper.Id == id) {
                        return topper;
                    }
                }

                return null;
            }
        }

        [Serializable]
        public sealed class CarPrice
        {
            [Tooltip("The car's id in the catalog")]
            [SerializeField]
            private string id;

            [Min(0)]
            [SerializeField]
            private int price;

            public CarPrice(string id, int price)
            {
                this.id = id;
                this.price = price;
            }

            public string Id => id;
            public int Price => Mathf.Max(0, price);
        }

        [Serializable]
        public sealed class TopperDefinition
        {
            [SerializeField]
            private string id;

            [Min(0)]
            [SerializeField]
            private int price;

            public TopperDefinition(string id, int price)
            {
                this.id = id;
                this.price = price;
            }

            public string Id => id;
            public int Price => price;
        }
    }

    /// <summary>The three mission slots. Stored in the asset by number, so new ones go at the end.</summary>
    public enum MissionSlot
    {
        Round,
        Total,
        Lesson,
    }

    /// <summary>What a mission counts. Stored in the asset by number, so new ones go at the end.</summary>
    public enum MissionCounter
    {
        /// <summary>Rounds played to the end.</summary>
        Rounds,

        /// <summary>Rounds won.</summary>
        Wins,

        /// <summary>Knock-outs credited to the player.</summary>
        Knockouts,

        /// <summary>Rounds the player ended among the last two cars.</summary>
        FinalTwo,

        /// <summary>The player's own boosts, the automatic ones aside.</summary>
        Boosts,

        /// <summary>REVENGE taken.</summary>
        Revenge,

        /// <summary>Items the player used.</summary>
        ItemsUsed,

        /// <summary>Boxes the player broke.</summary>
        Boxes,

        /// <summary>Knock-outs of cars an item of the player hit last.</summary>
        ItemKnockouts,

        /// <summary>Rounds played on the mission's arena.</summary>
        ArenaRounds,
    }

    /// <summary>How hard a mission is, which sets its reward. Stored in the asset by number.</summary>
    public enum MissionTier
    {
        Easy,
        Medium,
        Hard,
    }
}
