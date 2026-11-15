using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The first round's tutorial, in a file of its own. Not in the tuning asset: the values here are the ones played.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Tutorial")]
        [Tooltip("The new player's first round as a tutorial: whether it is on and its numbers")]
        [SerializeField]
        private TutorialSettings tutorial = new();

        public TutorialSettings Tutorial => tutorial;

        /// <summary>
        /// The tutorial: a new player's very first round (no experience and no round finished on this device, the
        /// visit's first round of QUICK MATCH, not in a match) teaches the game step by step on a still floor, with bots
        /// that never attack. One bot stands across the floor: push it off the edge (the sticker says how to drive).
        /// Praise; a box in the middle, which always holds a Mega, running with no timer here; use it; praise; two more
        /// bots drop in side by side, and grown, the player sends them flying off. The round ends when the last bot is
        /// off, as a win, and pays a hat (<see cref="TopperSettings.GiftTopper"/>). The player cannot fall: the safe
        /// first rounds catch them (<see cref="SafetySettings"/>).
        /// <para>
        /// A step the player does not get on with is helped along after a while: the bot to push comes closer, so do the
        /// last two, and a Mega lost to a fall brings another box.
        /// </para>
        /// </summary>
        [Serializable]
        public sealed class TutorialSettings
        {
            [Tooltip("Whether a new player's first round is the tutorial")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("Seconds the praise after a step stays before the next step comes")]
            [Min(0.5f)]
            [SerializeField]
            private float praiseTime = 1.6f;

            [Tooltip("Seconds of the push step after which the bot to push comes towards the player")]
            [Min(1f)]
            [SerializeField]
            private float pushHelp = 12f;

            [Tooltip("How far from the player the helping bot stops, in metres")]
            [Min(1f)]
            [SerializeField]
            private float helpStop = 3.5f;

            [Tooltip("How far inside the safe edge the last two bots drop in, in metres")]
            [Min(1f)]
            [SerializeField]
            private float pairInset = 3.5f;

            [Tooltip("Metres between the last two bots, side by side")]
            [Min(1f)]
            [SerializeField]
            private float pairGap = 3f;

            [Tooltip("Seconds after the Mega was lost with bots left standing until the next box")]
            [Min(0f)]
            [SerializeField]
            private float boxAgain = 1.5f;

            [Tooltip("Metres a second the grown player's hit throws one of the last two bots away, along the floor")]
            [Min(0f)]
            [SerializeField]
            private float smashPush = 17f;

            [Tooltip("Metres a second the grown player's hit throws one of the last two bots up")]
            [Min(0f)]
            [SerializeField]
            private float smashLift = 7.5f;

            [Tooltip("Degrees a second the grown player's hit spins a bot it throws")]
            [Min(0f)]
            [SerializeField]
            private float smashSpin = 600f;

            [Tooltip("How fast, in metres a second, the grown player must close in for a hit to throw a bot")]
            [Min(0f)]
            [SerializeField]
            private float smashFrom = 1.5f;

            [Tooltip("Throttle of a bot walking to its place, 0 to 1")]
            [Range(0.1f, 1f)]
            [SerializeField]
            private float botPace = 0.55f;

            [Tooltip("The longest the tutorial may last, in seconds: it ends when its lesson does, this only stops a stuck "
                + "round")]
            [Min(30f)]
            [SerializeField]
            private float length = 300f;

            public bool IsEnabled => enabled;
            public float PraiseTime => praiseTime;
            public float PushHelp => pushHelp;
            public float HelpStop => helpStop;
            public float PairInset => pairInset;
            public float PairGap => pairGap;
            public float BoxAgain => boxAgain;
            public float SmashPush => smashPush;
            public float SmashLift => smashLift;
            public float SmashSpin => smashSpin;
            public float SmashFrom => smashFrom;
            public float BotPace => botPace;

            /// <summary>
            /// Seconds a bot the player rammed stands still instead of walking back to its place: it would push back.
            /// </summary>
            public float RammedRest => 1.5f;

            /// <summary>The bots of the tutorial: one to push, two to smash off grown.</summary>
            public int Bots => 3;

            /// <summary>
            /// A plain round's falls and edge assist, on a still floor without mechanics, with only the boxes the
            /// tutorial lays, three bots and a clock long enough never to end it.
            /// </summary>
            public ModeRules Rules => ModeRules.Plain.With(floor: FloorRule.Still, mechanics: false,
                pickups: PickupRule.Laid, bots: Bots, length: length);
        }
    }
}
