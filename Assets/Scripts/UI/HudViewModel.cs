using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The in-round overlay: the time left, which cars are still on the arena, the player's knock-outs and, on a touch
    /// screen, the boost button with the boost's cooldown. The round state passes the round in; the view model reads it
    /// on demand, so the view polls instead of subscribing to every change. A car going out and a knock-out by the
    /// player are passed on as they happen, since the car's chip leaving the HUD and the KNOCKOUT! sticker belong to the
    /// moment the car falls.
    /// <para>
    /// The boost button is there from the visit's first round on: the boost is the one way to dodge a hit and to save
    /// the car at the edge, and the first test on Poki showed touch players losing the first round without it.
    /// </para>
    /// <para>
    /// The settings open over the running round, which goes on under them: the game has no pause, and Poki players
    /// reach for the sound in the middle of play, not only on the result.
    /// </para>
    /// </summary>
    public sealed class HudViewModel : ViewModel, IPayloadReceiver<HudRound>
    {
        /// <summary>
        /// Raised when the player knocks a car out, with the point on the arena's edge where it went over: where the
        /// KNOCKOUT! sticker goes; true for REVENGE! on the marked rival instead.
        /// </summary>
        public event Action<Vector3, bool> PlayerKnockout;

        /// <summary>
        /// Raised when a car falls off, the player's included, with its index in the round (0 the player, 1..N the
        /// bots, independently of their colours).
        /// </summary>
        public event Action<int> CarOut;
        public event Action<int, int> Eliminated;

        /// <summary>
        /// Raised in a match when a car that fell comes back onto the floor, with its index.
        /// </summary>
        public event Action<int> CarBack;

        /// <summary>
        /// Raised in a match when the player's marked rival changes (<see cref="IsNemesis"/>).
        /// </summary>
        public event Action NemesisChanged;

        /// <summary>
        /// Raised in a match when the player's car breaks a box and its slot starts to spin, with the item it will land
        /// on (the HUD spins anyway and lands on it).
        /// </summary>
        public event Action<ItemKind> ItemRolled;

        /// <summary>
        /// Raised in a match when the player uses the item in the slot, with its kind.
        /// </summary>
        public event Action<ItemKind> ItemUsed;

        /// <summary>
        /// Raised in a match when an item of the player's hits a car, with its kind and where the car was.
        /// </summary>
        public event Action<ItemKind, Vector3> ItemHit;

        /// <summary>
        /// The game fired the player's boost for them (a ram or a rescue).
        /// </summary>
        public event Action AutoBoosted;

        /// <summary>A mission was done during the round: the state counts them, the view pops MISSION!.</summary>
        public event Action MissionDone;

        private readonly IScreenNavigator navigator;
        private Car player;
        private readonly HudControls controls;
        private ArenaRound round;
        private PlayerDriver driver;

        public HudViewModel(IScreenNavigator navigator, HudControls controls)
        {
            this.navigator = navigator;
            this.controls = controls;
        }

        public GameTuning.ControlSettings Controls { get; private set; }

        public bool IsFirstRound { get; private set; }

        /// <summary>The round of the visit this HUD shows, 1 for the first, captured before progress moves on.</summary>
        public int RoundNumber { get; private set; }

        /// <summary>The player has never finished a round, so the start screen also says what the game is about.</summary>
        public bool IsNewPlayer { get; private set; }

        /// <summary>The win streak this round is played on (it multiplies the reward), or 0 when it is no streak.</summary>
        public int Streak { get; private set; }

        public bool IsRunning => round != null && round.IsRunning && !round.Player.IsOut;

        public float LastSteerTime => driver?.LastSteerTime ?? float.NegativeInfinity;

        public float LastTouchTime => driver?.LastTouchTime ?? float.NegativeInfinity;

        /// <summary>
        /// Suggest a boost only before the first successful one, when it can fire and another living car is nearby.
        /// </summary>
        public bool CanHintBoost
        {
            get
            {
                if (!IsFirstRound || !IsRunning || driver.Boosts > 0 || !IsBoostReady) {
                    return false;
                }

                var distanceSquared = Controls.BoostHintDistance * Controls.BoostHintDistance;
                for (var index = 0; index < round.Cars.Count; index++) {
                    var car = round.Cars[index];
                    if (car != round.Player && !car.IsOut &&
                        (car.Position - round.Player.Position).sqrMagnitude < distanceSquared) {
                        return true;
                    }
                }

                return false;
            }
        }

        // The round keeps the player as YOU; only the screen shows it in the player's language.
        public string NameOf(int index) => index == 0 ? GameText.You : round.NameOf(index);
        public int LevelOf(int index) => round.LevelOf(index);
        public int ColorOf(int index) => round.ColorOf(index);

        public Vector3 PositionOf(int index) => round.Cars[index].Position;

        public float LastBoostOf(int index) => round.Cars[index].Movement.LastBoostTime;
        public float LastHitOf(int index) => round.Cars[index].LastHitTime;

        // Observation only: these sources never steer a car or consume the bot driver's random sequence.
        public bool IsNearEdge(int index)
        {
            var position = round.Cars[index].Position;
            position.y = 0f;
            return round.Edge.Nearness(position) >= 0.85f;
        }

        public bool HasRecovered(int index)
        {
            var car = round.Cars[index];
            var position = car.Position;
            position.y = 0f;
            return !car.IsOut && car.Movement.IsGrounded && !car.Movement.IsStaggered &&
                (round.Edge.Nearness(position) < 0.85f || Vector3.Dot(round.Edge.Inward(position), car.Movement.Velocity) >= 0f);
        }

        public bool IsNemesis(int index) => round != null && round.Nemesis != null &&
            round.IndexOf(round.Nemesis) == index;

        public bool HasBegun => round != null && round.HasBegun;

        /// <summary>
        /// Seconds left in the round.
        /// </summary>
        public float TimeLeft => round?.TimeLeft ?? 0f;

        public int CarsLeft => round?.CarsLeft ?? 0;

        /// <summary>
        /// Whether the round has ended: in a match, the clock ran out.
        /// </summary>
        public bool IsFinished => round != null && round.IsFinished;

        /// <summary>
        /// Whether the round is a long match: the HUD shows the score instead of the cars still in.
        /// </summary>
        public bool IsMatch => round != null && round.IsMatch;

        /// <summary>
        /// The knock-outs a car has scored in the match.
        /// </summary>
        public int ScoreOf(int index) => round?.Score?.KnockoutsOf(index) ?? 0;

        /// <summary>
        /// The car at a place of the match's ranking, from 0 for the leader (knock-outs, then fewer falls, then who
        /// scored first).
        /// </summary>
        public int IndexAtPlace(int place) => round?.Score?.IndexAt(place) ?? place;

        /// <summary>
        /// Seconds until a car fallen in a match comes back; 0 while it is in play.
        /// </summary>
        public float RespawnTimeLeftOf(int index) =>
            round != null && index >= 0 && index < round.Cars.Count ? round.RespawnTimeLeft(round.Cars[index]) : 0f;

        /// <summary>
        /// Seconds until the player's car comes back after a fall in a match; 0 while it is in play.
        /// </summary>
        public float RespawnTimeLeft => round?.Player != null ? round.RespawnTimeLeft(round.Player) : 0f;

        /// <summary>
        /// How much of the player's safe time after coming back is left, 1 to 0; 0 when not safe.
        /// </summary>
        public float SafeShareLeft => round?.Player != null && round.IsRunning ? round.SafeShareLeft(round.Player) : 0f;

        /// <summary>
        /// The ability the player's car has running (<see cref="ItemKind.None"/> for none) and how much of it is left,
        /// from 1 to 0, and in seconds. Several can run at once; the badge has room for one, the one used last.
        /// </summary>
        public ItemKind Ability => round?.Player != null ? round.Player.Ability : ItemKind.None;
        public float AbilityShare => round?.Player != null ? round.Player.AbilityShare : 0f;
        public float AbilityTimeLeft => round?.Player != null ? round.Player.AbilityTimeLeft : 0f;

        /// <summary>
        /// The item in the player's slot (<see cref="ItemKind.None"/> for an empty one), whether it still spins, and
        /// whether it can be used now.
        /// </summary>
        /// <summary>Whether the round has boxes and items: a match, or a round from the visit's second.</summary>
        public bool HasItems => driver != null && driver.HasItems;

        public ItemKind HeldItem => round?.Player != null ? round.Player.HeldItem : ItemKind.None;
        public bool IsItemRolling => round?.Player != null && round.Player.IsItemRolling;
        public bool IsItemReady => IsRunning && round.Player.IsItemReady;

        /// <summary>
        /// The item button was clicked with a mouse: the input service hears it as the item key. A touch on it is the
        /// input service's own.
        /// </summary>
        public void PressItem() => controls.PressItem();

        public Vector3 PlayerPosition => round?.Player != null ? round.Player.Position : Vector3.zero;

        /// <summary>
        /// How many cars started the round, so their indices run from 0 to one less.
        /// </summary>
        public int CarCount => round?.Cars.Count ?? 0;

        /// <summary>
        /// The index of the player's car; its white ring and place on the HUD set it apart.
        /// </summary>
        public int PlayerIndex => round?.Player != null ? round.IndexOf(round.Player) : 0;

        /// <summary>
        /// Cars the player has knocked out this round.
        /// </summary>
        public int Knockouts => round?.PlayerKnockouts ?? 0;

        /// <summary>
        /// How far the player's boost has cooled down, from 0 just after a boost to 1 once it is ready again.
        /// </summary>
        public float BoostReadiness => round != null && round.IsRunning ? round.Player.Movement.BoostReadiness : 0f;

        /// <summary>
        /// Whether the player's boost can fire now: cooled down and the round on.
        /// </summary>
        public bool IsBoostReady => round != null && round.IsRunning && round.Player.Movement.IsBoostReady;

        /// <summary>
        /// Whether the cars stand on the arena and the round has not begun yet: the wait for the player's first input
        /// in a visit's first round, then the kick-off's 3, 2, 1. The ghost joystick and the feed treat both alike.
        /// </summary>
        public bool IsBeforeStart => round != null && round.IsPlaced && !round.HasBegun;

        /// <summary>
        /// Whether the cars stand waiting for the player's first steer or boost: the start prompt shows meanwhile and
        /// flashes away at that input, as the kick-off starts, not at GO. Only a visit's first round waits for one.
        /// </summary>
        public bool IsWaitingForPlayer => IsFirstRound && IsBeforeStart && (driver == null || !driver.HasActed);

        /// <summary>
        /// The kick-off's step on the screen: 3, 2 or 1, 0 for GO!, or <see cref="NoKickoff"/> before it starts. The
        /// round state sets each step as its own timing reaches it, so the numbers, their ticks and the round's begin at
        /// GO cannot drift apart; the view only animates the step it is given.
        /// </summary>
        public int KickoffStep { get; private set; } = NoKickoff;

        /// <summary>The unscaled time <see cref="KickoffStep"/> was set, for the view's animation.</summary>
        public float KickoffStepTime { get; private set; }

        /// <summary>How long the current step stays on the screen, in unscaled seconds.</summary>
        public float KickoffStepDuration { get; private set; }

        public const int NoKickoff = -1;

        public void ShowMissionDone() => MissionDone?.Invoke();

        /// <summary>
        /// Shows a step of the kick-off (3, 2, 1, or 0 for GO!) for this many unscaled seconds.
        /// </summary>
        public void ShowKickoff(int step, float duration)
        {
            KickoffStep = step;
            KickoffStepDuration = duration;
            KickoffStepTime = Time.unscaledTime;
        }

        /// <summary>
        /// Whether the car with this index is still on the arena.
        /// </summary>
        public bool IsCarIn(int index)
        {
            return round != null && index >= 0 && index < round.Cars.Count && !round.Cars[index].IsOut;
        }

        public bool IsSettingsOpen { get; private set; }

        /// <summary>
        /// The settings popup over the HUD. The round does not wait for it, and while it is open a finger or the mouse
        /// on the screen belongs to the popup and drives nothing; the keys still drive.
        /// </summary>
        public async UniTask OpenSettings(CancellationToken token)
        {
            if (IsSettingsOpen) {
                return;
            }

            IsSettingsOpen = true;
            controls.IsPopupOpen = true;
            try {
                await navigator.RunAsync<SettingsViewModel, bool>(token);
            }
            finally {
                IsSettingsOpen = false;
                controls.IsPopupOpen = false;
            }
        }

        public void SetPayload(HudRound payload)
        {
            Unsubscribe();
            round = payload.Round;
            driver = payload.Driver;
            if (driver != null) {
                driver.AutoBoosted += OnAutoBoosted;
            }

            Controls = payload.Controls;
            // Capture it now: session progress already advances as this round finishes.
            IsFirstRound = payload.IsFirstRound;
            RoundNumber = payload.RoundNumber;
            IsNewPlayer = payload.IsNewPlayer;
            Streak = payload.Streak;
            // The view model outlives a round: a GO! of the round before must not show again on this one.
            KickoffStep = NoKickoff;
            if (round != null) {
                round.CarOut += OnCarOut;
                round.KnockedOut += OnKnockedOut;
                round.Eliminated += OnEliminated;
                round.Respawned += OnRespawned;
                round.NemesisChanged += OnNemesisChanged;
                player = round.Player;
                if (player != null) {
                    player.ItemRolled += OnItemRolled;
                    player.ItemUsed += OnItemUsed;
                    player.ItemHit += OnItemHit;
                }
            }
        }

        public override void Dispose()
        {
            Unsubscribe();
            base.Dispose();
        }

        private void Unsubscribe()
        {
            if (driver != null) {
                driver.AutoBoosted -= OnAutoBoosted;
            }

            if (round != null) {
                round.CarOut -= OnCarOut;
                round.KnockedOut -= OnKnockedOut;
                round.Eliminated -= OnEliminated;
                round.Respawned -= OnRespawned;
                round.NemesisChanged -= OnNemesisChanged;
            }

            if (player != null) {
                player.ItemRolled -= OnItemRolled;
                player.ItemUsed -= OnItemUsed;
                player.ItemHit -= OnItemHit;
                player = null;
            }
        }

        private void OnAutoBoosted(AutoBoostKind kind) => AutoBoosted?.Invoke();
        private void OnItemRolled(ItemKind kind) => ItemRolled?.Invoke(kind);
        private void OnItemUsed(ItemKind kind) => ItemUsed?.Invoke(kind);
        private void OnItemHit(ItemKind kind, Vector3 point) => ItemHit?.Invoke(kind, point);

        private void OnRespawned(Car car) => CarBack?.Invoke(round.IndexOf(car));

        private void OnNemesisChanged() => NemesisChanged?.Invoke();

        private void OnCarOut(Car car)
        {
            CarOut?.Invoke(round.IndexOf(car));
        }

        private void OnEliminated(int index, int by) => Eliminated?.Invoke(index, by);

        private void OnKnockedOut(Car car, Car knockedOutBy)
        {
            if (knockedOutBy != round.Player || car == round.Player) {
                return;
            }

            // By the time it counts as out the car is below the arena and flying on; the point where it went over the
            // edge, under it, is still on the screen and tells which car it was.
            var position = car.Position;
            var flat = new Vector3(position.x, 0f, position.z);
            PlayerKnockout?.Invoke(round.Edge.ClampToFloor(flat), car == round.Nemesis);
        }
    }

    /// <summary>
    /// The state hands the app-scoped HUD this arena's read-only sources together; the view never resolves services
    /// from a child scope or keeps using the previous round's driver after a restart.
    /// </summary>
    public readonly struct HudRound
    {
        public ArenaRound Round { get; }
        public PlayerDriver Driver { get; }
        public GameTuning.ControlSettings Controls { get; }
        public bool IsFirstRound { get; }
        public int RoundNumber { get; }
        public bool IsNewPlayer { get; }
        public int Streak { get; }

        public HudRound(ArenaRound round, PlayerDriver driver, GameTuning.ControlSettings controls, bool isFirstRound,
            int roundNumber, bool isNewPlayer, int streak = 0)
        {
            IsNewPlayer = isNewPlayer;
            Streak = streak;
            Round = round;
            Driver = driver;
            Controls = controls;
            IsFirstRound = isFirstRound;
            RoundNumber = roundNumber;
        }
    }
}
