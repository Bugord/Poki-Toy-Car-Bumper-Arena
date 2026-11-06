using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The ads, apart from the main file: the commercial break between rounds (<see cref="AdSettings.FreePlaySeconds"/>)
    /// the rewarded ad that doubles a result's reward (CLAIM ×2) and the one that doubles the XP of the next matches (XP
    /// BOOST in the main menu). None touches the physics or the bots.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Ads")]
        [Tooltip("The commercial break between rounds and CLAIM ×2 on the result")]
        [SerializeField]
        private AdSettings ads = new();

        public AdSettings Ads => ads;

        [Serializable]
        public sealed class AdSettings
        {
            [Tooltip("Seconds of rounds a visit plays before NEXT MATCH shows a commercial break under the curtain: the "
                + "first rounds, which decide whether a new player stays, go straight in, as in SMUP")]
            [Min(0f)]
            [SerializeField]
            private float freePlaySeconds = 180f;

            [Tooltip("The result offers CLAIM ×2: a rewarded ad that pays the round's own XP and coins once more")]
            [SerializeField]
            private bool doubleReward = true;

            [Tooltip("The main menu offers XP BOOST: a rewarded ad that multiplies the XP of the next matches")]
            [SerializeField]
            private bool xpBoost = true;

            [Tooltip("What XP BOOST multiplies a match's own XP by (its missions' XP stays as it is)")]
            [Min(1)]
            [SerializeField]
            private int boostMultiplier = 2;

            [Tooltip("How many matches (rounds) one XP BOOST lasts. A boosted result is not held to one level")]
            [Min(1)]
            [SerializeField]
            private int boostMatches = 5;

            public float FreePlaySeconds => freePlaySeconds;
            public bool DoubleReward => doubleReward;
            public bool XpBoost => xpBoost;
            public int BoostMultiplier => boostMultiplier;
            public int BoostMatches => boostMatches;
        }
    }
}
