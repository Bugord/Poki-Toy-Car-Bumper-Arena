using System;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Input;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Drives the player's car from the input while the round runs, the same way whatever the player holds:
    /// <list type="bullet">
    /// <item>the move keys or a gamepad stick point where the car should go, up being away from the camera;</item>
    /// <item>the mouse, held down, drives the car towards the cursor and stops it there;</item>
    /// <item>a finger is a joystick that appears where it touched, or drives the car towards itself like the mouse
    /// (<see cref="GameTuning.ControlSettings.TouchSteering"/>). The joystick is drawn under the finger while it
    /// steers (<see cref="IOnScreenControls.ShowJoystick"/>), so the player sees where its middle is and how hard they
    /// push.</item>
    /// </list>
    /// A drag wins over the keys while it lasts. Boost (Space, a short click of the left mouse button, a gamepad's south
    /// button or the boost button, a second finger or a short tap on a touch screen) fires the car's boost towards
    /// where it is steered. A released tap or click remembers nonzero steering briefly, so letting go to tap keeps the
    /// intended boost line. In a long match the item key (E, Left Shift, the right mouse button, a gamepad's west button
    /// or the item button) uses the item the car holds (<see cref="ArenaPickups.Use"/>).
    /// Once the round is over the
    /// car rolls to a stop, so a click on the result screen does not steer it, and boost does nothing.
    /// Before the round begins it only watches for the player's first steer or boost (<see cref="HasActed"/>), which the
    /// first round of a visit waits for; through the kick-off's 3, 2, 1 the car stands, a boost or an item pressed then
    /// is dropped, and a finger already down shows its joystick, so whatever the player holds drives the car from GO.
    /// Near the edge it takes away the part of the steer that points off the arena, as much as the round's
    /// <see cref="RoundDifficulty.EdgeAssist"/> says, so a new player who steers too far stops at the edge instead of
    /// driving off it, and a boost there does not throw it off either; a hit still pushes the car off.
    /// Each physics step also limits outward speed after movement, including a boost, while grounded and free of
    /// stagger or recent pressure. It removes no tangential speed and never pulls a car inward after a ring warning.
    /// At a ram or a rescue the game may fire the boost for the player, as often as the round's
    /// <see cref="RoundDifficulty.AutoBoost"/> says (<see cref="Cars.AutoBoost"/>); such boosts are counted apart
    /// (<see cref="AutoBoosts"/>), so the analytics still tell whether the player found the boost.
    /// It also notes how the player plays the round (<see cref="FirstInput"/>, <see cref="FirstMoveTime"/>,
    /// <see cref="Boosts"/>), so the analytics can tell a player who never found the controls from one who was bored.
    /// </summary>
    public sealed class PlayerDriver : ITickable, IFixedTickable, IInitializable, IDisposable
    {
        private readonly IInputService input;
        private readonly GameTuning tuning;
        private readonly ArenaCamera arenaCamera;
        private readonly ArenaRound round;
        private readonly IOnScreenControls onScreenControls;
        private readonly RoundDifficulty difficulty;
        private readonly ArenaPickups pickups;
        private readonly AutoBoost autoBoost;

        // The joystick's centre: where the finger touched, pulled along when the finger goes past the radius.
        private int joystickDragId;
        private Vector2 joystickCenter;

        // Where the player last steered, before the edge assist; zero when they did not.
        private Vector3 steer;
        private float steerThrottle;
        private Vector3 lastSteer;
        private float lastSteerTime = float.NegativeInfinity;

        private bool isDriving;
        private bool isJoystickShown;

        // Seconds the cars have stood waiting for the player before the round began.
        private float waitTime;

        /// <summary>
        /// Whether the player has steered or pressed boost since the cars were placed: the round can begin, the player
        /// is there.
        /// </summary>
        public bool HasActed { get; private set; }

        /// <summary>
        /// What the player first steered with this round, null until they steer.
        /// </summary>
        public InputMethod? FirstInput { get; private set; }

        /// <summary>
        /// What fired the first successful boost this round; starting the round or pressing during cooldown does not count.
        /// </summary>
        public BoostInputMethod? FirstBoostInput { get; private set; }

        /// <summary>
        /// Seconds from the cars standing on the arena to the player's first steer, or a negative number until they
        /// steer: into the round, plus the wait for the player before a round that waits for one.
        /// </summary>
        public float FirstMoveTime { get; private set; } = -1f;

        /// <summary>
        /// Boosts the player's car fired this round.
        /// </summary>
        public int Boosts { get; private set; }

        /// <summary>
        /// Boosts the game fired for the player this round (<see cref="Cars.AutoBoost"/>), not in <see cref="Boosts"/>.
        /// </summary>
        public int AutoBoosts { get; private set; }

        /// <summary>
        /// The game fired the player's boost for them, so the HUD can show it was a boost.
        /// </summary>
        public event Action<AutoBoostKind> AutoBoosted;

        /// <summary>
        /// Whether this round has boxes and items, so the HUD has an item button.
        /// </summary>
        public bool HasItems => pickups.IsEnabled;

        /// <summary>
        /// Items the player used this round.
        /// </summary>
        public int ItemsUsed { get; private set; }

        /// <summary>
        /// Unscaled time of the last nonzero steer, before edge assist, so the HUD never mistakes braking at the edge
        /// for a player who has not found the joystick. Negative infinity until the first steer.
        /// </summary>
        public float LastSteerTime => lastSteerTime;

        /// <summary>
        /// Unscaled time of the last touch input, including a boost pressed during cooldown. Events keep even touches
        /// shorter than a HUD refresh, so a demonstration yields to the player's finger immediately.
        /// </summary>
        public float LastTouchTime { get; private set; } = float.NegativeInfinity;

        public PlayerDriver(IInputService input, GameTuning tuning, ArenaCamera arenaCamera, ArenaRound round,
            IOnScreenControls onScreenControls, RoundDifficulty difficulty, ArenaPickups pickups)
        {
            this.input = input;
            this.tuning = tuning;
            this.arenaCamera = arenaCamera;
            this.round = round;
            this.onScreenControls = onScreenControls;
            this.difficulty = difficulty;
            this.pickups = pickups;
            this.autoBoost = new AutoBoost(tuning, difficulty, round);
        }

        public void Initialize()
        {
            input.BoostPressed += OnBoostPressed;
            input.ItemPressed += OnItemPressed;
            input.MoveUsed += OnMoveUsed;
        }

        public void Dispose()
        {
            input.MoveUsed -= OnMoveUsed;
            input.BoostPressed -= OnBoostPressed;
            input.ItemPressed -= OnItemPressed;
            // The HUD outlives the arena: a joystick left shown would stay on the next round's screen.
            SetJoystickShown(false);
        }

        public void Tick()
        {
            SetJoystickShown(Steer());
            if (isDriving) {
                var kind = autoBoost.Tick(round.Player, steer, steerThrottle, Time.deltaTime);
                if (kind != AutoBoostKind.None) {
                    AutoBoosts++;
                    AutoBoosted?.Invoke(kind);
                }
            }
        }

        public void FixedTick()
        {
            var car = round.Player;
            if (car == null) {
                return;
            }

            // VContainer's fixed tick precedes MonoBehaviour.FixedUpdate. Supply the current shrinking edge and
            // contact history each step; CarMovement limits the final driven velocity after its own ground probe.
            var share = round.IsRunning && !car.IsOut ? difficulty.EdgeAssist : 0f;
            car.Movement.SetEdgeHold(round.Edge, share, car.LastHitTime);
        }

        /// <returns>True while a finger steers by the joystick, which is drawn under it then.</returns>
        private bool Steer()
        {
            if (IsWaiting) {
                WatchForFirstInput();
                return input.TryGetDrag(out var held) && !held.IsMouse &&
                    tuning.Controls.TouchSteering == GameTuning.TouchSteering.Joystick && TryShowJoystick(held);
            }

            var car = round.Player;
            if (!round.IsRunning || car.IsOut) {
                if (isDriving) {
                    isDriving = false;
                    car.Movement.Drive(Vector3.zero, 0f);
                }

                return false;
            }

            isDriving = true;
            if (input.TryGetDrag(out var drag)) {
                if (drag.IsMouse || tuning.Controls.TouchSteering == GameTuning.TouchSteering.FollowFinger) {
                    FollowPointer(car, drag.Position);
                    return false;
                }

                SteerByJoystick(car, drag);
                return true;
            }

            var move = Vector2.ClampMagnitude(input.CurrentMoveInput, 1f);
            Drive(car, arenaCamera.ScreenToGroundDirection(move), move.magnitude);
            return false;
        }

        // A press while the boost cools down is dropped, not kept for later: a boost that fires on its own a moment
        // after the press would throw the car somewhere the player no longer means.
        // The press that starts the round only starts it: a boost before the round would throw the car off its place.
        private void OnBoostPressed(BoostInputMethod method)
        {
            if (method == BoostInputMethod.Button || method == BoostInputMethod.Finger || method == BoostInputMethod.Tap) {
                LastTouchTime = Time.unscaledTime;
            }

            if (IsWaiting) {
                HasActed = true;
                return;
            }

            var car = round.Player;
            if (round.IsRunning && !car.IsOut && TryBoost(car, method)) {
                Boosts++;
                FirstBoostInput ??= method;
            }
        }

        // The press that starts the round only starts it, as with boost; an empty or spinning slot does nothing.
        private void OnItemPressed()
        {
            // A round without boxes has no items: the key does nothing there, not even start the round.
            if (!pickups.IsEnabled) {
                return;
            }

            if (IsWaiting) {
                HasActed = true;
                return;
            }

            if (pickups.Use(round.Player)) {
                ItemsUsed++;
            }
        }

        /// <summary>
        /// Fires the car's boost, but near the edge without the part of it that points off the arena, by the round's
        /// edge assist: a boost carries the car far past where the steer could stop it, so one fired off the edge
        /// would undo the assist. One left pointing mostly off is not fired, and its cooldown is kept.
        /// </summary>
        private bool TryBoost(Car car, BoostInputMethod method)
        {
            // Only a released tap or click uses memory: other controls keep their usual steering/nose boost.
            var isTap = method == BoostInputMethod.Tap || method == BoostInputMethod.Mouse;
            var heading = steer;
            if (isTap && heading == Vector3.zero && Time.unscaledTime - lastSteerTime <= tuning.Controls.TapBoostMemory) {
                heading = lastSteer;
            }

            var way = heading != Vector3.zero ? heading : car.transform.forward;
            way.y = 0f;
            way.Normalize();
            var outward = Outward(car, out var share);
            var isAdjusted = false;
            if (difficulty.EdgeAssist > 0f && share > 0f) {
                var off = Vector3.Dot(way, outward);
                if (off > 0f) {
                    way -= outward * (off * difficulty.EdgeAssist);
                    if (way.magnitude < 0.3f) {
                        return false;
                    }

                    isAdjusted = true;
                }
            }

            if (isTap || isAdjusted) {
                car.Movement.Drive(way, 1f);
            }

            return car.Movement.TryBoost();
        }

        // A steer that starts the round counts as the round's first, at its very start.
        private void OnMoveUsed(InputMethod method)
        {
            if (method == InputMethod.Touch) {
                LastTouchTime = Time.unscaledTime;
            }

            if (!round.IsPlaced || round.IsFinished || round.Player.IsOut) {
                return;
            }

            HasActed = true;
            // The press that starts the round (or comes during the kick-off) only starts it, finger or mouse.
            if (IsWaiting) {
                input.CancelTapBoost();
            }

            if (FirstInput == null) {
                FirstInput = method;
                FirstMoveTime = waitTime + round.Elapsed;
            }
        }

        private bool IsWaiting => round.IsPlaced && !round.HasBegun;

        /// <summary>
        /// Notices the player's first steer while the cars wait: a key or stick held, or a finger or the mouse that
        /// comes down (which also reports it as <see cref="IInputService.MoveUsed"/>). The car is not driven yet.
        /// </summary>
        private void WatchForFirstInput()
        {
            waitTime += Time.deltaTime;
            if (input.TryGetDrag(out var drag)) {
                // Neither a tap nor a click held across the start boosts as it lifts.
                input.CancelTapBoost();
                HasActed = true;
            }
            else if (input.CurrentMoveInput != Vector2.zero) {
                HasActed = true;
            }
        }

        private void FollowPointer(Car car, Vector2 screenPosition)
        {
            if (!arenaCamera.TryScreenToGround(screenPosition, out var target)) {
                Drive(car, Vector3.zero, 0f);
                return;
            }

            var controls = tuning.Controls;
            var offset = target - car.Position;
            offset.y = 0f;
            var throttle = Mathf.InverseLerp(controls.PointerStopRadius, controls.PointerSlowRadius, offset.magnitude);
            Drive(car, offset, throttle);
        }

        private void SteerByJoystick(Car car, PointerDrag drag)
        {
            var stick = PlaceJoystick(drag);
            var throttle = Mathf.InverseLerp(tuning.Controls.JoystickDeadZone, 1f, stick.magnitude);
            Drive(car, arenaCamera.ScreenToGroundDirection(stick), throttle);
        }

        // Before GO the joystick only shows, so the thumb finds it during the kick-off and keeps its centre at GO.
        private bool TryShowJoystick(PointerDrag drag)
        {
            PlaceJoystick(drag);
            return true;
        }

        /// <returns>The stick: where the finger is from the joystick's middle, 1 at its radius.</returns>
        private Vector2 PlaceJoystick(PointerDrag drag)
        {
            if (drag.Id != joystickDragId) {
                joystickDragId = drag.Id;
                joystickCenter = drag.Start;
            }

            var radius = tuning.Controls.JoystickRadius * Mathf.Min(Screen.width, Screen.height);
            var offset = drag.Position - joystickCenter;
            if (offset.magnitude > radius) {
                joystickCenter = drag.Position - offset.normalized * radius;
                offset = drag.Position - joystickCenter;
            }

            onScreenControls.ShowJoystick(joystickCenter, joystickCenter + offset, radius);
            return offset / radius;
        }

        /// <summary>
        /// Drives the car, less off the arena the closer it is to the edge, by the round's edge assist: what is left is
        /// the steer along the edge, at as much throttle as it keeps of the steer, so steering straight off stops the
        /// car and steering past the edge slides it along.
        /// </summary>
        private void Drive(Car car, Vector3 heading, float throttle)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            car.Movement.FallTelemetry?.Assist(0f);
#endif
            heading.y = 0f;
            steer = throttle > 0f ? heading : Vector3.zero;
            steerThrottle = throttle;
            if (steer != Vector3.zero) {
                lastSteer = steer;
                lastSteerTime = Time.unscaledTime;
            }

            if (difficulty.EdgeAssist > 0f && heading.sqrMagnitude > 0.0001f) {
                var outward = Outward(car, out var share);
                share *= difficulty.EdgeAssist;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                car.Movement.FallTelemetry?.Assist(share);
#endif
                heading.Normalize();
                var off = Vector3.Dot(heading, outward);
                if (off > 0f && share > 0f) {
                    heading -= outward * (off * share);
                    throttle *= heading.magnitude;
                }
            }

            car.Movement.Drive(heading, throttle);
        }

        /// <returns>The way off the arena from the car, flat, or zero at the very middle.</returns>
        /// <param name="share">How near the edge the car is for the edge assist: 0 farther in than
        /// <see cref="GameTuning.ControlSettings.EdgeAssistMargin"/>, 1 at
        /// <see cref="GameTuning.ControlSettings.EdgeAssistStop"/> and beyond.</param>
        private Vector3 Outward(Car car, out float share)
        {
            var position = car.Position;
            position.y = 0f;
            var edge = round.Edge;
            var inward = edge.Inward(position);
            if (inward == Vector3.zero) {
                share = 0f;
                return Vector3.zero;
            }

            var controls = tuning.Controls;
            share = edge.Proximity(position, controls.EdgeAssistMargin, controls.EdgeAssistStop);
            return (-inward).normalized;
        }

        private void SetJoystickShown(bool isShown)
        {
            if (!isShown && isJoystickShown) {
                onScreenControls.HideJoystick();
            }

            isJoystickShown = isShown;
        }
    }
}
