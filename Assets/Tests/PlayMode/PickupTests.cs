using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bugord.App.Platform;
using NUnit.Framework;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The abilities on the floor of a long match, on the real Game scene: they appear only in a match and only while the
    /// floor is whole or shrinking, on safe floor and leaning to the cars behind; each kind does what it says and ends;
    /// a car just back from a fall cannot take one; a pickup on a wave that starts to shake fades; and a bot drives to
    /// one. Each test changes the shared tuning in memory and the tear-down puts it back.
    /// </summary>
    public sealed class PickupTests : SimulationSceneFixture
    {
        private const int FramesPerSecond = 60;

        private readonly Dictionary<(object owner, string name), object> saved = new();

        private IObjectResolver container;
        private ArenaRound round;
        private ArenaPickups pickups;
        private ArenaCrumble crumble;
        private GameTuning tuning;

        [TearDown]
        public void RestoreTuning()
        {
            foreach (var pair in saved) {
                pair.Key.owner.GetType().GetField(pair.Key.name, BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(pair.Key.owner, pair.Value);
            }

            saved.Clear();
        }

        // --- Only in a match ---

        [UnityTest]
        public IEnumerator ARoundOfLastCarStandingHasNoPickups()
        {
            tuning = UseMode(GameTuning.GameMode.LastStanding);
            yield return Load(false, 7);
            Assert.That(pickups.IsEnabled, Is.False);
            var spawned = 0;
            pickups.Spawned += _ => spawned++;
            for (var frame = 0; frame < FramesPerSecond * 8; frame++) {
                yield return null;
            }

            Assert.That(spawned, Is.Zero);
            Assert.That(pickups.Live, Is.Empty);
            Assert.That(GameObject.Find("ArenaPickups"), Is.Null, "a round must not build a pickup object");
        }

        // --- When and where they appear ---

        [UnityTest]
        public IEnumerator PickupsAppearOnlyWhileTheFloorIsWholeOrShrinkingAndOnSafeFloorAwayFromCars()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            SetCycles(3, 2f, 3f, 2f, 2f, 0.8f);
            Set(tuning.Pickups, "firstDelay", 0.2f);
            Set(tuning.Pickups, "interval", 0.6f);
            Set(tuning.Pickups, "jitter", 0f);
            Set(tuning.Pickups, "lifetime", 1.5f);
            yield return Load(true, 7);

            var settings = tuning.Pickups;
            var phases = new List<FloorPhase>();
            var spots = new List<Vector3>();
            pickups.Spawned += pickup => {
                phases.Add(crumble.Phase);
                spots.Add(pickup.Position);
                Assert.That(round.Cars.Where(car => !car.IsOut).All(car =>
                    Vector3.ProjectOnPlane(car.Position - pickup.Position, Vector3.up).magnitude >= settings.CarClearance - 0.05f),
                    Is.True, "a pickup appeared too near a car");
                Assert.That(round.Edge.DistanceToEdge(pickup.Position, settings.EdgeMargin), Is.GreaterThanOrEqualTo(-0.05f),
                    "a pickup appeared too near the edge");
            };

            var peakFrames = 0;
            var maxLive = 0;
            // The schedule is made on the first tick.
            yield return Frames(2);
            var length = crumble.Schedule.Length;
            for (var frame = 0; frame < (int)(2f * length * FramesPerSecond); frame++) {
                yield return null;
                maxLive = Mathf.Max(maxLive, pickups.Live.Count);
                var phase = crumble.Phase;
                // The pickups are cleared as the phase turns; a couple of frames' grace for the order of the ticks.
                peakFrames = phase is FloorPhase.Peak or FloorPhase.Return ? peakFrames + 1 : 0;
                if (peakFrames > 3) {
                    Assert.That(pickups.Live, Is.Empty, $"pickups lie on the floor in {phase}");
                }
            }

            Assert.That(phases, Is.Not.Empty, "no pickup appeared in two cycles");
            Assert.That(phases, Is.All.EqualTo(FloorPhase.Open).Or.EqualTo(FloorPhase.Shrink));
            Assert.That(maxLive, Is.LessThanOrEqualTo(settings.MaxCount));
        }

        [UnityTest]
        public IEnumerator PickupsLeanTowardsTheCarBehindInKnockouts()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            var settings = tuning.Pickups;
            Set(settings, "firstDelay", 0.2f);
            Set(settings, "interval", 0.6f);
            Set(settings, "jitter", 0f);
            // Gone before the next appears, or the spacing between pickups would push it away from the car.
            Set(settings, "lifetime", 0.5f);
            Set(settings, "leanPerKnockout", 1f);
            Set(settings, "leanMax", 1f);
            yield return Load(true, 7);

            // Car 3 has scored nothing while every other car has two knock-outs.
            foreach (var by in new[] { 0, 1, 2, 4, 5, 6 }) {
                round.Score.RecordFall(3, by, 1f);
                round.Score.RecordFall(3, by, 1f);
            }

            var behind = round.Cars[3];
            var near = 0;
            var spawned = 0;
            pickups.Spawned += pickup => {
                spawned++;
                var distance = Vector3.ProjectOnPlane(behind.Position - pickup.Position, Vector3.up).magnitude;
                if (distance <= settings.LeanRange.y + 0.1f) {
                    near++;
                }
            };

            yield return Until(() => spawned >= 8, 14f, "too few pickups appeared");
            Assert.That(near, Is.GreaterThanOrEqualTo(spawned * 0.8f),
                $"{near} of {spawned} pickups appeared by the car behind");
        }

        // --- The kinds ---

        [UnityTest]
        public IEnumerator HeavyMakesTheCarThreeTimesAsHeavyForAWhileAndWearsOff()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 1);
            var player = round.Player;
            yield return StandOnTheMiddle(player);
            var normal = player.Movement.Mass;
            Assert.That(normal, Is.EqualTo(tuning.Car.Mass).Within(0.001f));

            // Taken by driving over it, as a player does.
            var pickup = pickups.Spawn(PickupKind.Heavy, Vector3.zero);
            yield return Until(() => !pickup.IsLive, 2f, "the heavy pickup was not taken");
            Assert.That(player.Ability, Is.EqualTo(PickupKind.Heavy));
            yield return Frames(3);
            Assert.That(player.Movement.Mass, Is.EqualTo(normal * tuning.Pickups.Heavy.MassScale).Within(0.001f),
                "the physics must see the heavy mass, and so the push of its hits");
            Assert.That(player.AbilityShare, Is.GreaterThan(0.5f));

            yield return Until(() => player.Ability == PickupKind.None, tuning.Pickups.Heavy.Duration + 1f,
                "heavy never wore off");
            yield return Frames(3);
            Assert.That(player.Movement.Mass, Is.EqualTo(normal).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator AShockwavePushesTheCarsNearAndCreditsThePicker()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 7);
            var player = round.Player;
            var near = round.Cars[1];
            var far = round.Cars[2];
            var ghost = round.Cars[3];
            var shielded = round.Cars[4];
            var radius = tuning.Pickups.Shockwave.Radius;
            player.Movement.Place(new Vector3(0f, 0.3f, 0f), Quaternion.identity);
            near.Movement.Place(new Vector3(radius * 0.5f, 0.3f, 0f), Quaternion.identity);
            far.Movement.Place(new Vector3(0f, 0.3f, radius + 3f), Quaternion.identity);
            shielded.Movement.Place(new Vector3(0f, 0.3f, -radius * 0.5f), Quaternion.identity);
            ghost.Movement.Place(new Vector3(-radius * 0.5f, 0.3f, 0f), Quaternion.identity);
            yield return Frames(20);
            shielded.TakePickup(PickupKind.Shield);
            ghost.SetInvulnerable(true, round.Cars);
            var shieldedVelocity = shielded.Movement.Velocity;

            pickups.Grant(player, PickupKind.Shockwave);
            yield return Frames(2);

            Assert.That(near.LastHitBy, Is.SameAs(player), "the ring's push is the picker's");
            Assert.That(near.Movement.Velocity.x, Is.GreaterThan(4f), "the ring pushes outward");
            Assert.That(near.Movement.IsStaggered, Is.True);
            Assert.That(far.LastHitBy, Is.Null, "a car beyond the ring is left alone");
            Assert.That(ghost.LastHitBy, Is.Null, "a car just back from a fall is left alone");
            Assert.That(shielded.LastHitBy, Is.Null, "a shield takes the ring");
            Assert.That(shielded.Ability, Is.EqualTo(PickupKind.None), "the ring spends the shield");
            Assert.That(player.Ability, Is.EqualTo(PickupKind.None), "a shockwave leaves nothing running");

            // The fall that follows is the picker's knock-out.
            near.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => near.IsOut, 5f, "the pushed car never fell");
            Assert.That(round.Score.KnockoutsOf(0), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AShieldTakesTheNextCarHitWholeAndHazardHitsToo()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 60f);
            yield return Load(true, 7);
            var player = round.Player;
            var bot = round.Cars[1];
            yield return StandOnTheMiddle(player);

            pickups.Grant(player, PickupKind.Shield);
            Assert.That(player.IsShielded, Is.True);

            // A bot rams it from three metres.
            var from = player.Position + Vector3.left * 3f;
            bot.Movement.Place(new Vector3(from.x, 0.2f, from.z), Quaternion.LookRotation(Vector3.right));
            bot.Movement.Push(Vector3.right * 10f);
            yield return Frames(FramesPerSecond / 2);
            Assert.That(player.Ability, Is.EqualTo(PickupKind.None), "the hit spent the shield");
            Assert.That(player.LastHitBy, Is.Null, "a hit the shield took gives no credit");
            Assert.That(player.Movement.IsStaggered, Is.False, "a hit the shield took does not unbalance the car");

            // An arena hazard's hit is taken back whole: no push, no stagger, nobody's hit. The rammer goes out of the way.
            bot.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return StandOnTheMiddle(player);
            pickups.Grant(player, PickupKind.Shield);
            var velocity = player.Movement.Velocity;
            new HazardBlow(tuning, container.Resolve<RoundDifficulty>(), null, null)
                .Apply(player, Vector3.right, 9f, 3f, 0.5f, 1f);
            Assert.That((player.Movement.Velocity - velocity).magnitude, Is.LessThan(0.01f));
            Assert.That(player.Movement.IsStaggered, Is.False);
            Assert.That(float.IsNegativeInfinity(player.LastHazardTime), Is.True);
            Assert.That(player.Ability, Is.EqualTo(PickupKind.None));

            // Without a shield the same hazard hits.
            new HazardBlow(tuning, container.Resolve<RoundDifficulty>(), null, null)
                .Apply(player, Vector3.right, 9f, 3f, 0.5f, 1f);
            Assert.That(player.Movement.IsStaggered, Is.True);

            // And an unspent shield ends with its time.
            pickups.Grant(player, PickupKind.Shield);
            yield return Until(() => player.Ability == PickupKind.None, tuning.Pickups.Shield.Duration + 1f,
                "the shield never ran out");
        }

        [UnityTest]
        public IEnumerator TurboFiresTheBoostAgainAtOnceAndWearsOff()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 1);
            var player = round.Player;
            yield return StandOnTheMiddle(player);
            var turbo = tuning.Pickups.Turbo;

            pickups.Grant(player, PickupKind.Turbo);
            Assert.That(player.Movement.TryBoost(), Is.True);
            Assert.That(player.Movement.TryBoost(), Is.False, "the boost still needs its short wait");
            yield return Until(() => player.Movement.IsBoostReady, turbo.BoostCooldown + 0.5f,
                "the boost did not come back within the turbo's wait");
            Assert.That(tuning.Boost.Cooldown, Is.GreaterThan(turbo.BoostCooldown * 3f), "the test needs a real difference");
            yield return StandOnTheMiddle(player);
            Assert.That(player.Movement.TryBoost(), Is.True, "a second boost within the turbo's few seconds");

            yield return Until(() => player.Ability == PickupKind.None, turbo.Duration + 1f, "turbo never wore off");
            yield return StandOnTheMiddle(player);
            player.Movement.TryBoost();
            yield return Frames(FramesPerSecond);
            Assert.That(player.Movement.IsBoostReady, Is.False, "the tuned cooldown applies again");
        }

        // --- Who can take one ---

        [UnityTest]
        public IEnumerator ACarJustBackFromAFallCannotTakeAPickup()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 1);
            var player = round.Player;
            player.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => player.IsOut, 5f, "the player never fell");
            yield return Until(() => !player.IsOut, tuning.Match.RespawnDelay + 1f, "the player never came back");
            yield return Until(() => player.Movement.IsGrounded, tuning.Match.MaxDropTime + 1f, "it never landed");
            Assert.That(player.IsInvulnerable, Is.True);

            var pickup = pickups.Spawn(PickupKind.Heavy, player.Position);
            yield return Frames(3);
            Assert.That(pickup.IsLive, Is.True, "a ghost took a pickup");
            Assert.That(player.Ability, Is.EqualTo(PickupKind.None));

            yield return Until(() => !player.IsInvulnerable, tuning.Match.Invulnerability + 3f, "its safe time never ended");
            yield return Until(() => !pickup.IsLive, 2f, "the car in play never took it");
            Assert.That(player.Ability, Is.EqualTo(PickupKind.Heavy));
        }

        [UnityTest]
        public IEnumerator AFallEndsTheAbility()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 1);
            var player = round.Player;
            yield return StandOnTheMiddle(player);
            pickups.Grant(player, PickupKind.Heavy);
            player.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => player.IsOut, 5f, "the player never fell");
            Assert.That(player.Ability, Is.EqualTo(PickupKind.None));
            yield return Frames(3);
            Assert.That(player.Movement.Mass, Is.EqualTo(tuning.Car.Mass).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator APickupOnAWaveThatStartsToShakeFades()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 1);
            var floor = container.Resolve<ArenaFloor>();
            // A spot in the second wave from the outside, far enough in to be safe until the wave shakes.
            var spot = new Vector3(0f, 0f, -(floor.Wave(1).Inner + floor.Wave(1).Outer) * 0.5f);
            Assert.That(floor.Contains(1, spot), Is.True);
            var pickup = pickups.Spawn(PickupKind.Shield, spot);
            yield return Frames(3);
            Assert.That(pickup.IsLive, Is.True, "the pickup faded before its wave shook");

            floor.Warn(1);
            yield return Frames(3);
            Assert.That(pickup.IsLive, Is.False, "a pickup on a shaking wave must fade");
            Assert.That(pickups.Live, Is.Empty);
        }

        // --- Bots ---

        [UnityTest]
        public IEnumerator ABotDrivesToANearbyPickupAndTakesIt()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 60f);
            // None of the automatic ones: only the one this test lays down.
            Set(tuning.Pickups, "firstDelay", 100f);
            yield return Load(false, 7);
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            botDriver.Seed = 3;
            round.Place();
            round.Begin();
            var takers = new List<Car>();
            pickups.Collected += (car, _, _) => takers.Add(car);
            // The player's car keeps out of it.
            round.Player.Movement.Place(new Vector3(60f, 0.5f, 0f), Quaternion.identity);
            yield return Frames(2);

            var bot = round.Cars[1];
            var spot = Vector3.ProjectOnPlane(bot.Position, Vector3.up) * 0.55f;
            var pickup = pickups.Spawn(PickupKind.Turbo, spot);
            Assert.That(pickup, Is.Not.Null);
            yield return Until(() => takers.Any(car => car != round.Player), 10f, "no bot took the pickup");
        }

        // --- Events ---

        [Test]
        public void PickupEventsKeepWithinPokisNumberLimitAndNameTheKind()
        {
            var platform = new MatchTests.RecordingPlatform();
            foreach (var kind in new[] { PickupKind.Heavy, PickupKind.Shockwave, PickupKind.Shield, PickupKind.Turbo }) {
                platform.MeasurePickup(12, kind);
            }

            platform.MeasureMatchPickups(1, 0);
            platform.MeasureMatchPickups(2, 14);
            foreach (var (category, what, action) in platform.Events) {
                Assert.That(GameEvent.CountNumbers(category, what, action), Is.LessThanOrEqualTo(GameEvent.MaxNumbers),
                    $"{category} / {what} / {action}");
            }

            var names = platform.Events.Select(e => $"{e.Item1} / {e.Item2} / {e.Item3}").ToList();
            Assert.That(names, Does.Contain("match / pickup-heavy-r4plus / pickup"));
            Assert.That(names, Does.Contain("match / pickup-shockwave-r4plus / pickup"));
            Assert.That(names, Does.Contain("match / pickup-shield-r4plus / pickup"));
            Assert.That(names, Does.Contain("match / pickup-turbo-r4plus / pickup"));
            Assert.That(names, Does.Contain("match / pickups-0-r1 / pickups"));
            Assert.That(names, Does.Contain("match / pickups-10plus-r2 / pickups"));
        }

        // --- Helpers ---

        /// <summary>
        /// Loads the Game scene in the mode the test set. With <paramref name="freezeBots"/> the bots stand still and the
        /// cars are placed and the round begun (a round of last-car-standing always is); otherwise the test does it.
        /// </summary>
        private IEnumerator Load(bool freezeBots, int visitRound)
        {
            CreateParent(new SimulationInput(), new SimulationProgress { Round = visitRound }, "sandbox");
            yield return LoadGame();
            container = GameScope().Container;
            round = container.Resolve<ArenaRound>();
            pickups = container.Resolve<ArenaPickups>();
            crumble = container.Resolve<ArenaCrumble>();
            if (freezeBots) {
                var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
                typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(botDriver, true);
            }

            if (freezeBots || !tuning.IsMatch) {
                round.Place();
                round.Begin();
            }
        }

        /// <summary>Stands a car at rest on the middle of the floor and waits for its wheels to touch it.</summary>
        private IEnumerator StandOnTheMiddle(Car car)
        {
            car.Movement.Place(new Vector3(0f, 0.2f, 0f), Quaternion.identity);
            car.Movement.Drive(Vector3.zero, 0f);
            yield return Until(() => car.Movement.IsGrounded, 2f, "the car never touched the floor");
        }

        private void Set(object owner, string name, object value)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{owner.GetType().Name} has no field {name}");
            if (!saved.ContainsKey((owner, name))) {
                saved[(owner, name)] = field.GetValue(owner);
            }

            field.SetValue(owner, value);
        }

        private void SetCycles(int cycles, float open, float shrink, float peak, float returnTime, float returnWarning)
        {
            var settings = tuning.FloorCycles;
            Set(settings, "cycles", cycles);
            Set(settings, "open", open);
            Set(settings, "shrink", shrink);
            Set(settings, "peak", peak);
            Set(settings, "returnTime", returnTime);
            Set(settings, "returnWarning", returnWarning);
        }

        private static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++) {
                yield return null;
            }
        }

        private static IEnumerator Until(System.Func<bool> condition, float seconds, string message)
        {
            var frames = Mathf.CeilToInt(seconds * FramesPerSecond);
            for (var frame = 0; frame < frames && !condition(); frame++) {
                yield return null;
            }

            Assert.That(condition(), Is.True, message);
        }
    }
}
