using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The FOOTBALL event mode's numbers, kept in a file of its own so the mode's work touches only its own files. Not in
    /// the tuning asset: the values here are the ones played.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("FOOTBALL")]
        [Tooltip("The FOOTBALL event mode: whether it is on and its numbers")]
        [SerializeField]
        private FootballSettings football = new();

        public FootballSettings Football => football;

        /// <summary>
        /// FOOTBALL: blue and red, three cars each, push a big ball into the other team's goal on a pitch of the mode's
        /// own; the team with more goals wins when time runs out. Nobody falls: the pitch has a fence all round.
        /// </summary>
        [Serializable]
        public sealed class FootballSettings
        {
            /// <summary>The pitch's id in the arena catalog, the lobby and the analytics; never in the rotation.</summary>
            public const string PitchId = "pitch";

            /// <summary>The pitch's only look.</summary>
            public const string PitchVariant = "grass";

            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled;

            [Tooltip("Seconds a match of football lasts; the team with more goals wins, a level score is a draw")]
            [Min(10f)]
            [SerializeField]
            private float duration = 90f;

            [Tooltip("Bots on the pitch: with the player, two teams of three")]
            [Range(1, 6)]
            [SerializeField]
            private int bots = 5;

            [Tooltip("The most of the player's goals a match pays XP and coins for, each as a knock-out")]
            [Min(0)]
            [SerializeField]
            private int goalRewardCap = 5;

            [Header("Pitch")]
            [Tooltip("Half the pitch's width (X) and length (Z, away from the camera) in metres; the goals are at the " +
                "ends of the length. The floor texture (Tools/floors/designs_pitch.py) is painted for these numbers")]
            [SerializeField]
            private Vector2 pitchHalfSize = new(11f, 17f);

            [Tooltip("Radius of the pitch's rounded corners, in metres: round enough that the ball rolls out of them")]
            [Min(0f)]
            [SerializeField]
            private float cornerRadius = 4f;

            [Tooltip("Half the width of a goal's mouth, in metres")]
            [Min(1f)]
            [SerializeField]
            private float goalHalfWidth = 3.5f;

            [Tooltip("How deep a goal goes in from the end of the pitch, in metres: the goal line is this far in")]
            [Min(1f)]
            [SerializeField]
            private float goalDepth = 2.2f;

            [Tooltip("Height of a goal's crossbar, in metres")]
            [Min(1f)]
            [SerializeField]
            private float goalHeight = 2.4f;

            [Header("Ball")]
            [Tooltip("Radius of the ball, in metres; a car is about 1.6 wide")]
            [Min(0.3f)]
            [SerializeField]
            private float ballRadius = 1.05f;

            [Tooltip("Mass of the ball against a car's 1: light, so a car carries it")]
            [Min(0.05f)]
            [SerializeField]
            private float ballMass = 0.3f;

            [Tooltip("How much of its speed the ball keeps off a car, a wall or the floor: 0 dead, 1 a superball")]
            [Range(0f, 1f)]
            [SerializeField]
            private float ballBounce = 0.6f;

            [Tooltip("The ball's rolling drag: how fast a rolling ball slows down on its own")]
            [Min(0f)]
            [SerializeField]
            private float ballDrag = 0.35f;

            [Tooltip("The ball's top speed, in metres a second")]
            [Min(1f)]
            [SerializeField]
            private float ballMaxSpeed = 21f;

            [Tooltip("The ball's centre never goes higher than this, in metres: it stays under the fence's invisible walls")]
            [Min(1f)]
            [SerializeField]
            private float ballMaxHeight = 2f;

            [Tooltip("A car hitting the ball adds this times its speed towards the ball on top of the bounce, so a ram " +
                "shoots it")]
            [Min(0f)]
            [SerializeField]
            private float kick = 0.9f;

            [Tooltip("Share of a kick that lifts the ball off the floor, so a shot hops a little")]
            [Range(0f, 1f)]
            [SerializeField]
            private float kickLift = 0.12f;

            [Tooltip("A boosting car's kick is this many times stronger")]
            [Min(1f)]
            [SerializeField]
            private float boostKick = 1.5f;

            [Tooltip("A ball slower than this, in metres a second, near the fence for Stuck Time is nudged back into play")]
            [Min(0f)]
            [SerializeField]
            private float stuckSpeed = 0.6f;

            [Tooltip("Seconds a slow ball by the fence waits before it is nudged out")]
            [Min(0.5f)]
            [SerializeField]
            private float stuckTime = 2.5f;

            [Tooltip("Speed of the nudge that frees a stuck ball, in metres a second, towards the middle")]
            [Min(0f)]
            [SerializeField]
            private float stuckNudge = 5f;

            [Header("Goal and kick-off")]
            [Tooltip("Seconds of celebration after a goal before everyone goes back to kick-off: the ball in the net, " +
                "GOAL! on the HUD")]
            [Min(0f)]
            [SerializeField]
            private float celebrateTime = 1.6f;

            [Tooltip("Seconds the cars wait in their places after a goal before play goes on")]
            [Min(0f)]
            [SerializeField]
            private float kickoffWait = 1f;

            [Tooltip("Seconds of the near stop of time on a goal, at the time scale below")]
            [Min(0f)]
            [SerializeField]
            private float goalStop = 0.25f;

            [Tooltip("Time scale of the stop on a goal")]
            [Range(0.01f, 1f)]
            [SerializeField]
            private float goalStopScale = 0.1f;

            [Header("Bots")]
            [Tooltip("A bot drives at the ball from this far behind it, on the side of its own goal, in metres, so the " +
                "push sends the ball towards the other goal")]
            [Min(0f)]
            [SerializeField]
            private float approachBehind = 2.4f;

            [Tooltip("Within this angle of the line from the ball to the goal it attacks, in degrees, a bot behind the " +
                "ball drives straight through it")]
            [Range(0f, 90f)]
            [SerializeField]
            private float shootAngle = 35f;

            [Tooltip("The keeper of a team waits this far out in front of its goal line, in metres")]
            [Min(0f)]
            [SerializeField]
            private float keeperOut = 3.2f;

            [Tooltip("The keeper leaves its goal for a ball closer to it than this, in metres")]
            [Min(0f)]
            [SerializeField]
            private float keeperRush = 8f;

            [Tooltip("A bot that is not the nearest of its team to the ball keeps this far from it, in metres, so six " +
                "cars do not pile on the ball")]
            [Min(0f)]
            [SerializeField]
            private float supportDistance = 7f;

            [Tooltip("Share of the time a bot's aim at the ball is wrong, at worst (Easy) and at best (Normal)")]
            [SerializeField]
            private Vector2 aimError = new(0.35f, 0.12f);

            [Tooltip("Gas of the bots of the player's team, against the other team's: helpers that do not score " +
                "everything for the player")]
            [Range(0.3f, 1f)]
            [SerializeField]
            private float allyPace = 0.85f;

            public bool Enabled => enabled;
            public float Duration => duration;
            public int Bots => bots;
            public int GoalRewardCap => goalRewardCap;
            public Vector2 PitchHalfSize => pitchHalfSize;
            public float CornerRadius => cornerRadius;
            public float GoalHalfWidth => goalHalfWidth;
            public float GoalDepth => goalDepth;
            public float GoalHeight => goalHeight;
            public float BallRadius => ballRadius;
            public float BallMass => ballMass;
            public float BallBounce => ballBounce;
            public float BallDrag => ballDrag;
            public float BallMaxSpeed => ballMaxSpeed;
            public float BallMaxHeight => ballMaxHeight;
            public float Kick => kick;
            public float KickLift => kickLift;
            public float BoostKick => boostKick;
            public float StuckSpeed => stuckSpeed;
            public float StuckTime => stuckTime;
            public float StuckNudge => stuckNudge;
            public float CelebrateTime => celebrateTime;
            public float KickoffWait => kickoffWait;
            public float GoalStop => goalStop;
            public float GoalStopScale => goalStopScale;
            public float ApproachBehind => approachBehind;
            public float ShootAngle => shootAngle;
            public float KeeperOut => keeperOut;
            public float KeeperRush => keeperRush;
            public float SupportDistance => supportDistance;
            public Vector2 AimError => aimError;
            public float AllyPace => allyPace;

            /// <summary>The goal line's distance from the middle along Z, at each end.</summary>
            public float GoalLine => pitchHalfSize.y - goalDepth;

            private MapSettings pitch;

            /// <summary>
            /// The pitch: a rounded rectangle that never crumbles, with its one look. Made in code, so its numbers live
            /// here and not among the rotation's arenas.
            /// </summary>
            public MapSettings Pitch => pitch ??= new MapSettings(PitchId,
                ArenaSettings.StillSquare(pitchHalfSize, cornerRadius, 4f, 1.5f), new VariantSettings(PitchVariant));

            /// <summary>What the mode changes in the shared round (<see cref="ModeRules"/>).</summary>
            public ModeRules Rules => ModeRules.Plain.With(fall: FallRule.PutBack, fence: true,
                floor: FloorRule.Still, mechanics: false, pickups: PickupRule.None, duration: duration, teams: 2,
                bots: bots, edgeAssist: false, knockoutRewardCap: goalRewardCap, arena: Pitch,
                pointsPayAsKnockouts: true);
        }
    }
}
