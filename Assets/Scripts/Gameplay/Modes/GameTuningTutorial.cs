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
        /// visit's first round of QUICK MATCH, not in a match) teaches the game step by step on a still floor, with three
        /// bots that never attack. Drive to a point; push one bot off the edge; take a box, which always holds a
        /// Mega, which runs with no timer here; grown, push the last two bots off. The round ends when the last bot is off, as a win, and pays a
        /// hat (<see cref="TopperSettings.GiftTopper"/>). The player cannot fall: the safe first rounds catch them
        /// (<see cref="SafetySettings"/>).
        /// <para>
        /// Every step is a gate on what has happened, not on the order: a bot pushed off early, or both shoved off by
        /// ramming, moves the lesson on all the same. A step the player does not get on with is helped along after a
        /// while: the point to drive to is skipped, the bot to push comes closer, and a Mega lost to a fall
        /// brings another box.
        /// </para>
        /// </summary>
        [Serializable]
        public sealed class TutorialSettings
        {
            [Tooltip("Whether a new player's first round is the tutorial")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("Metres from the player's start towards the middle where the point to drive to lies")]
            [Min(1f)]
            [SerializeField]
            private float driveDistance = 5.5f;

            [Tooltip("How close the player's car must come to the point, in metres")]
            [Min(0.5f)]
            [SerializeField]
            private float driveReach = 1.8f;

            [Tooltip("Seconds of the drive step after which it is skipped")]
            [Min(1f)]
            [SerializeField]
            private float driveSkip = 12f;

            [Tooltip("Seconds of the push step after which the bot to push comes towards the player")]
            [Min(1f)]
            [SerializeField]
            private float pushHelp = 12f;

            [Tooltip("How far from the player the helping bot stops, in metres")]
            [Min(1f)]
            [SerializeField]
            private float helpStop = 3.5f;

            [Tooltip("How far inside the safe edge the last two bots wait for the grown player, in metres")]
            [Min(1f)]
            [SerializeField]
            private float pairInset = 3.5f;

            [Tooltip("Metres between the last two bots, side by side")]
            [Min(1f)]
            [SerializeField]
            private float pairGap = 3f;

            [Tooltip("Metres from the middle towards the pair where the box lies")]
            [Min(0f)]
            [SerializeField]
            private float boxDistance = 1f;

            [Tooltip("Seconds after the Mega was lost with bots left standing until the next box")]
            [Min(0f)]
            [SerializeField]
            private float boxAgain = 1.5f;

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
            public float DriveDistance => driveDistance;
            public float DriveReach => driveReach;
            public float DriveSkip => driveSkip;
            public float PushHelp => pushHelp;
            public float HelpStop => helpStop;
            public float PairInset => pairInset;
            public float PairGap => pairGap;
            public float BoxDistance => boxDistance;
            public float BoxAgain => boxAgain;
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
