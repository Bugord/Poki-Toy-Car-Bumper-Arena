using System;
using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The FOOTBALL event mode: blue (the player's team) and red push a big ball into the other team's goal on a pitch
    /// of the mode's own (<see cref="GameTuning.FootballSettings.Pitch"/>); nobody falls, the team with more goals when
    /// time runs out wins, a level score is a draw. A goal is a point of the scoring team: the car of that team that
    /// touched the ball last scores it (<see cref="ArenaRound.AddPoints"/>, a point of its own that the player is paid
    /// for as a knock-out), an own goal or a ball nobody touched is the team's alone (<see cref="ArenaRound.AddTeamPoints"/>).
    /// After a goal the ball rests in the net a moment, then every car goes back to its place and the ball to the
    /// centre spot. The bots play roles (<see cref="SteerBot"/>): a keeper by its goal, the nearest of the rest goes
    /// for the ball from its own goal's side, the others keep their distance.
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.Football"/>), silent in every other: it builds nothing and draws no random number there.
    /// </para>
    /// </summary>
    public sealed class FootballMode : IRoundMode, IBotSteering, ITickable, IFixedTickable, IDisposable
    {
        /// <summary>Metres from the ball within which a car stops a slow ball from counting as stuck.</summary>
        private const float StuckClearance = 2.6f;

        /// <summary>Metres from the centre spot within which a slow ball is never stuck: it is just waiting.</summary>
        private const float MiddleRadius = 4f;

        /// <summary>Seconds between the bots' new mistakes of aim.</summary>
        private const float AimHold = 2f;

        /// <summary>Metres a bot keeps to the side of the ball when it goes round it.</summary>
        private const float RoundBall = 2.2f;

        public enum Phase
        {
            /// <summary>The round has not begun: the cars stand in their places, the ball on the spot.</summary>
            Waiting,

            /// <summary>Football.</summary>
            Play,

            /// <summary>A goal: the ball rests in the net, nobody scores.</summary>
            Celebrating,

            /// <summary>After a goal: every car in its place, waiting to go.</summary>
            Kickoff,
        }

        /// <summary>A goal: the scoring team, the car that scored it (null for an own goal or nobody's ball).</summary>
        public event Action<int, Car> Scored;

        private readonly GameTuning.FootballSettings settings;
        private readonly ArenaRound round;
        private readonly GameTuning.ArenaSettings arenaSettings;
        private readonly RoundDifficulty difficulty;
        private readonly MechanicArt art;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly Transform parent;
        private readonly HitStop hitStop = new();
        private readonly Dictionary<Bot, (float error, float until)> aims = new();
        private readonly Car[] chasers = new Car[2];
        private readonly Car[] keepers = new Car[2];
        private int rolesFrame = -1;
        private FootballPitch pitch;
        private EffectPlayer effects;
        private float phaseLeft;
        private float stuckTime;

        public FootballMode(GameTuning tuning, RoundRules rules, ArenaRound round, GameTuning.ArenaSettings arenaSettings,
            RoundDifficulty difficulty, MechanicArt art, ArenaCamera arenaCamera, IAudioService audioService,
            Transform parent)
        {
            this.settings = tuning.Football;
            this.round = round;
            this.arenaSettings = arenaSettings;
            this.difficulty = difficulty;
            this.art = art;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.parent = parent;
            this.IsActive = rules.Kind == SpecialRound.Football;
        }

        public SpecialRound Kind => SpecialRound.Football;

        /// <summary>A blue SUV with the ball on its roof.</summary>
        public ModeCard Card { get; } = new("suv", 2, FootballPitch.BallLookName, 1.05f);

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.FootballSettings Settings => settings;

        /// <summary>What the pitch has on it: the goals and the ball; null until the cars are placed.</summary>
        public FootballPitch Pitch => pitch;

        public FootballBall Ball => pitch?.Ball;

        /// <summary>Where the ball is now, the centre spot before it is built.</summary>
        public Vector3 BallPosition => pitch != null ? pitch.Ball.transform.position : Vector3.zero;

        public Phase State { get; private set; } = Phase.Waiting;

        /// <summary>Goals scored this round, by both teams.</summary>
        public int Goals { get; private set; }

        /// <summary>Goals the player scored this round.</summary>
        public int PlayerGoals { get; private set; }

        /// <summary>The middle of the goal line <paramref name="team"/> attacks.</summary>
        public Vector3 TargetOf(int team) => new(0f, 0f, -FootballPitch.EndOf(team) * settings.GoalLine);

        public void Tick()
        {
            if (!IsActive) {
                return;
            }

            hitStop.Tick();
            if (pitch == null) {
                if (!round.IsPlaced) {
                    return;
                }

                Build();
            }

            switch (State) {
                case Phase.Waiting:
                    if (round.HasBegun) {
                        pitch.Release();
                        State = Phase.Play;
                    }

                    break;
                case Phase.Celebrating:
                    phaseLeft -= Time.deltaTime;
                    if (phaseLeft <= 0f) {
                        Kickoff();
                    }

                    break;
                case Phase.Kickoff:
                    phaseLeft -= Time.deltaTime;
                    if (phaseLeft <= 0f) {
                        pitch.Release();
                        State = Phase.Play;
                    }

                    break;
            }

            KeepCarsInPlay();
        }

        public void FixedTick()
        {
            if (!IsActive || pitch == null || pitch.Ball.Body.isKinematic) {
                return;
            }

            KeepBallInPlay(Time.fixedDeltaTime);
            if (State == Phase.Play && round.IsRunning) {
                CheckGoal();
            }
        }

        public void Dispose()
        {
            hitStop.Release();
            pitch?.Dispose();
        }

        /// <summary>
        /// Every bot plays football: a keeper by its goal, the nearest of the others to the ball goes for it, the rest
        /// keep their distance. A bot drives at the ball only from its own goal's side, so the push sends the ball on.
        /// </summary>
        public bool SteerBot(Bot bot, IArenaEdge edge, float deltaTime)
        {
            bot.SetPickup(null);
            var car = bot.Car;
            if (pitch == null || State is Phase.Kickoff or Phase.Waiting) {
                car.Movement.Drive(Vector3.zero, 0f);
                return true;
            }

            UpdateRoles();
            var team = car.Team;
            var pace = bot.Pace * (team == round.Player?.Team ? settings.AllyPace : 1f);
            var ball = Flat(BallPosition);
            var position = Flat(car.Position);
            var own = Flat(pitch.GoalOf(team));
            if (State == Phase.Celebrating) {
                Arrive(car, SlotOf(car), pace * 0.5f);
                return true;
            }

            if (car == keepers[team]) {
                if (Vector3.Distance(ball, own) < settings.KeeperRush && car != chasers[team]
                    && IsCloserThanChaser(car, team, ball)) {
                    Attack(bot, team, ball, position, own, pace);
                    return true;
                }

                // In front of the goal, on the line from its middle to the ball, but never wider than the posts.
                var towards = ball - own;
                var spot = own + (towards.sqrMagnitude > 0.01f ? towards.normalized : Vector3.forward * -FootballPitch.EndOf(team))
                    * settings.KeeperOut;
                spot.x = Mathf.Clamp(spot.x, -settings.GoalHalfWidth, settings.GoalHalfWidth);
                Arrive(car, spot, pace);
                return true;
            }

            if (car == chasers[team]) {
                Attack(bot, team, ball, position, own, pace);
                return true;
            }

            // Support: some way back towards its own goal and off to the side of the ball, ready for a loose ball.
            var back = (own - ball).normalized;
            var side = Mathf.Sign(position.x - ball.x);
            if (Mathf.Approximately(side, 0f)) {
                side = 1f;
            }

            var supportSpot = ball + back * (settings.SupportDistance * 0.7f) + Vector3.right * (side * settings.SupportDistance * 0.6f);
            supportSpot = Clamp(supportSpot, 2f);
            Arrive(car, supportSpot, pace);
            return true;
        }

        private void Build()
        {
            pitch = new FootballPitch(settings, art);
            pitch.Build(parent);
            // The effects live in the scene, not the container.
            effects = Object.FindAnyObjectByType<EffectPlayer>();
            pitch.Hold();
            PlaceCars();
        }

        // A goal is the ball's middle past the line, between the posts and under the bar.
        private void CheckGoal()
        {
            var position = pitch.Ball.transform.position;
            if (Mathf.Abs(position.x) >= settings.GoalHalfWidth || position.y >= settings.GoalHeight
                || Mathf.Abs(position.z) <= settings.GoalLine + settings.BallRadius * 0.6f) {
                return;
            }

            // The far goal is red's: a ball in it is blue's goal.
            var scorer = position.z > 0f ? RoundTeams.Blue : RoundTeams.Red;
            var touch = pitch.Ball.LastTouch;
            var author = touch != null && touch.Team == scorer ? touch : null;
            var isScored = author != null ? round.AddPoints(author, 1) : round.AddTeamPoints(scorer, 1);
            if (!isScored) {
                return;
            }

            Goals++;
            if (author != null && author.IsPlayer) {
                PlayerGoals++;
            }

            State = Phase.Celebrating;
            phaseLeft = settings.CelebrateTime;
            Celebrate(scorer, position);
            Scored?.Invoke(scorer, author);
        }

        private void Celebrate(int scorer, Vector3 position)
        {
            var isOurs = round.Player != null && scorer == round.Player.Team;
            hitStop.Stop(settings.GoalStop, settings.GoalStopScale, 0f);
            if (effects != null) {
                effects.PlayShockwave(new Vector3(0f, 0.1f, position.z), settings.GoalHalfWidth * 1.4f);
                effects.PlayKnockoutCredit(position + Vector3.up * 0.6f);
            }

            if (audioService != null) {
                audioService.Play(audioService.Library.HitHeavy, 0.8f);
                audioService.Play(isOurs ? audioService.Library.KnockoutCredit : audioService.Library.Knockout, 1f);
            }

            if (arenaCamera != null) {
                arenaCamera.Shake(isOurs ? 0.6f : 0.35f);
            }
        }

        private void Kickoff()
        {
            pitch.Hold();
            PlaceCars();
            foreach (var car in round.Cars) {
                car.Movement.Stagger(settings.KickoffWait);
            }

            stuckTime = 0f;
            State = Phase.Kickoff;
            phaseLeft = settings.KickoffWait;
        }

        private void PlaceCars()
        {
            foreach (var car in round.Cars) {
                if (car.IsOut) {
                    continue;
                }

                var spot = SlotOf(car);
                spot.y = arenaSettings.SpawnHeight;
                car.Movement.Place(spot, Quaternion.LookRotation(Vector3.forward * -FootballPitch.EndOf(car.Team)));
            }
        }

        /// <summary>
        /// A car's place at kick-off, on its team's half: the team's first car on the line through the spot, the next
        /// ones to either side a little farther back, the last in goal. Red's places are blue's turned round.
        /// </summary>
        private Vector3 SlotOf(Car car)
        {
            var team = car.Team;
            var slot = 0;
            var count = 0;
            foreach (var other in round.Cars) {
                if (other.Team != team) {
                    continue;
                }

                if (other == car) {
                    slot = count;
                }

                count++;
            }

            var end = FootballPitch.EndOf(team);
            Vector2 place;
            if (count > 1 && slot == count - 1) {
                place = new Vector2(0f, settings.GoalLine - settings.KeeperOut);
            }
            else {
                place = slot switch {
                    0 => new Vector2(0f, 5.5f),
                    1 => new Vector2(5.5f, 7.5f),
                    2 => new Vector2(-5.5f, 7.5f),
                    _ => new Vector2(slot % 2 == 0 ? -3f : 3f, 10f),
                };
            }

            return new Vector3(-end * place.x, 0f, end * place.y);
        }

        // Each team's nearest car to the ball, of those that are not its keeper (the player counts: a bot next to it
        // keeps back), and its keeper, its last car.
        private void UpdateRoles()
        {
            if (rolesFrame == Time.frameCount) {
                return;
            }

            rolesFrame = Time.frameCount;
            var ball = Flat(BallPosition);
            for (var team = 0; team < 2; team++) {
                chasers[team] = null;
                keepers[team] = null;
                var best = float.PositiveInfinity;
                var count = 0;
                foreach (var car in round.Cars) {
                    if (car.Team == team && !car.IsOut) {
                        keepers[team] = car;
                        count++;
                    }
                }

                if (count < 2 || keepers[team] != null && keepers[team].IsPlayer) {
                    keepers[team] = null;
                }

                foreach (var car in round.Cars) {
                    if (car.Team != team || car.IsOut || car == keepers[team]) {
                        continue;
                    }

                    var distance = (Flat(car.Position) - ball).sqrMagnitude;
                    if (distance < best) {
                        best = distance;
                        chasers[team] = car;
                    }
                }
            }
        }

        private bool IsCloserThanChaser(Car car, int team, Vector3 ball)
        {
            var chaser = chasers[team];
            return chaser == null
                || (Flat(car.Position) - ball).sqrMagnitude < (Flat(chaser.Position) - ball).sqrMagnitude;
        }

        /// <summary>
        /// Goes for the ball: round it to the point behind it on the line from the goal it attacks, then through it.
        /// Near its own goal it clears the ball away from the goal rather than aiming.
        /// </summary>
        private void Attack(Bot bot, int team, Vector3 ball, Vector3 position, Vector3 own, float pace)
        {
            var target = Flat(pitch.TargetOf(team));
            target.x += AimErrorOf(bot) * settings.GoalHalfWidth * 2f;
            var aim = (target - ball).normalized;
            var fromOwn = ball - own;
            if (fromOwn.magnitude < settings.KeeperRush * 1.25f) {
                // Out of the box first: away from the goal, a little towards the other one.
                aim = (fromOwn.normalized + aim * 0.5f).normalized;
            }

            var toBall = ball - position;
            var distance = toBall.magnitude;
            var behind = ball - aim * settings.ApproachBehind;
            if (distance < 0.01f) {
                bot.Car.Movement.Drive(aim, pace);
                return;
            }

            if (Vector3.Angle(toBall, aim) < settings.ShootAngle) {
                // Lined up: through the ball, a little past it.
                bot.Car.Movement.Drive(ball + aim - position, pace);
                return;
            }

            // On the wrong side of the ball: round it on the side the car is on, so it never pushes the ball back.
            var side = Vector3.Cross(Vector3.up, aim);
            var sign = Vector3.Dot(position - ball, side) >= 0f ? 1f : -1f;
            var isAhead = Vector3.Dot(position - ball, aim) > -settings.ApproachBehind * 0.5f;
            var spot = isAhead ? ball + side * (sign * (settings.BallRadius + RoundBall)) - aim * settings.BallRadius : behind;
            bot.Car.Movement.Drive(Clamp(spot, 1.5f) - position, pace);
        }

        private float AimErrorOf(Bot bot)
        {
            if (!aims.TryGetValue(bot, out var aim) || Time.time >= aim.until) {
                var share = Mathf.Lerp(settings.AimError.x, settings.AimError.y, difficulty.Level);
                aim = (bot.Range(-share, share), Time.time + AimHold);
                aims[bot] = aim;
            }

            return aim.error;
        }

        private void Arrive(Car car, Vector3 spot, float pace)
        {
            var offset = spot - Flat(car.Position);
            var distance = offset.magnitude;
            car.Movement.Drive(offset, distance < 0.4f ? 0f : pace * Mathf.Clamp01(distance / 3f));
        }

        private Vector3 Clamp(Vector3 point, float margin)
        {
            var half = settings.PitchHalfSize;
            point.x = Mathf.Clamp(point.x, -half.x + margin, half.x - margin);
            point.z = Mathf.Clamp(point.z, -settings.GoalLine + margin, settings.GoalLine - margin);
            return point;
        }

        /// <summary>
        /// Keeps the ball in play: never faster than its top speed, never over the walls, back on the spot if it ever
        /// leaves the pitch, and a nudge to the middle when it lies still by a wall with nobody near.
        /// </summary>
        private void KeepBallInPlay(float deltaTime)
        {
            var body = pitch.Ball.Body;
            var position = body.position;
            var velocity = body.linearVelocity;
            var half = settings.PitchHalfSize;
            if (!IsFinite(position) || !IsFinite(velocity) || Mathf.Abs(position.x) > half.x + 1f
                || Mathf.Abs(position.z) > half.y + 1f || position.y < -2f) {
                body.position = Vector3.up * (settings.BallRadius + 0.5f);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                pitch.Ball.Forget();
                return;
            }

            var flat = Flat(velocity);
            if (flat.magnitude > settings.BallMaxSpeed) {
                flat = flat.normalized * settings.BallMaxSpeed;
            }

            // Never more rise than this step leaves room for under the top: the ball stays under the walls' tops.
            var vertical = Mathf.Min(velocity.y, Mathf.Max(0f, (settings.BallMaxHeight - position.y) / deltaTime));
            if (position.y > settings.BallMaxHeight) {
                body.position = new Vector3(position.x, settings.BallMaxHeight, position.z);
            }

            body.linearVelocity = new Vector3(flat.x, vertical, flat.z);

            if (flat.magnitude > settings.StuckSpeed || Flat(position).magnitude < MiddleRadius || IsCarNear(position)) {
                stuckTime = 0f;
                return;
            }

            stuckTime += deltaTime;
            if (stuckTime >= settings.StuckTime) {
                stuckTime = 0f;
                var inward = -Flat(position).normalized;
                body.AddForce(inward * settings.StuckNudge + Vector3.up * (settings.StuckNudge * 0.3f), ForceMode.VelocityChange);
            }
        }

        private bool IsCarNear(Vector3 position)
        {
            foreach (var car in round.Cars) {
                if (!car.IsOut && (Flat(car.Position) - Flat(position)).sqrMagnitude < StuckClearance * StuckClearance) {
                    return true;
                }
            }

            return false;
        }

        // A car can only leave the part of the pitch play happens on by a glitch of the physics, or by being put back on
        // the floor behind a bumper: it goes back to its place.
        private void KeepCarsInPlay()
        {
            if (State != Phase.Play || Time.frameCount % 15 != 0) {
                return;
            }

            foreach (var car in round.Cars) {
                if (!car.IsOut && car.Movement.IsGrounded && !pitch.IsInPlay(car.Position, -0.6f)) {
                    var spot = SlotOf(car);
                    spot.y = arenaSettings.SpawnHeight;
                    car.Movement.Place(spot, Quaternion.LookRotation(Vector3.forward * -FootballPitch.EndOf(car.Team)));
                }
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
