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
    /// The mystery boxes and items of a long match, on the real Game scene: boxes appear only in a match and only while
    /// the floor is whole or shrinking, on safe floor and leaning to the cars behind; a broken box spins the slot and
    /// lands on an item drawn for the car's standing, which is held until used (a full slot gets nothing); each of the
    /// six items does what it says on use, with the bomb's and the glove's hits credited to their user and a shield
    /// taking both; a car just back from a fall cannot break a box; the bots take boxes and use items. Each test changes
    /// the shared tuning in memory and the tear-down puts it back.
    /// </summary>
    public sealed class ItemTests : SimulationSceneFixture
    {
        private const int FramesPerSecond = 60;

        private readonly Dictionary<(object owner, string name), object> saved = new();

        private IObjectResolver container;
        private SimulationInput input;
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
        public IEnumerator ARoundOfLastCarStandingHasNoBoxesAndNoItems()
        {
            tuning = UseMode(GameTuning.GameMode.LastStanding);
            yield return Load(false, 7);
            Assert.That(pickups.IsEnabled, Is.False);
            var spawned = 0;
            pickups.Spawned += _ => spawned++;
            Assert.That(pickups.Give(round.Player, ItemKind.Bomb), Is.False, "a round must not fill a slot");
            input.PressItem();
            for (var frame = 0; frame < FramesPerSecond * 8; frame++) {
                yield return null;
            }

            Assert.That(spawned, Is.Zero);
            Assert.That(pickups.Live, Is.Empty);
            Assert.That(round.Cars.All(car => !car.HasItem), Is.True);
            Assert.That(pickups.Use(round.Player), Is.False);
            Assert.That(GameObject.Find("ArenaPickups"), Is.Null, "a round must not build a box object");
        }

        // --- When and where boxes appear ---

        [UnityTest]
        public IEnumerator BoxesAppearOnlyWhileTheFloorIsWholeOrShrinkingAndOnSafeFloorAwayFromCars()
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
            pickups.Spawned += pickup => {
                phases.Add(crumble.Phase);
                Assert.That(round.Cars.Where(car => !car.IsOut).All(car =>
                    Vector3.ProjectOnPlane(car.Position - pickup.Position, Vector3.up).magnitude >= settings.CarClearance - 0.05f),
                    Is.True, "a box appeared too near a car");
                Assert.That(round.Edge.DistanceToEdge(pickup.Position, settings.EdgeMargin), Is.GreaterThanOrEqualTo(-0.05f),
                    "a box appeared too near the edge");
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
                // The boxes are cleared as the phase turns; a couple of frames' grace for the order of the ticks.
                peakFrames = phase is FloorPhase.Peak or FloorPhase.Return ? peakFrames + 1 : 0;
                if (peakFrames > 3) {
                    Assert.That(pickups.Live, Is.Empty, $"boxes lie on the floor in {phase}");
                }
            }

            Assert.That(phases, Is.Not.Empty, "no box appeared in two cycles");
            Assert.That(phases, Is.All.EqualTo(FloorPhase.Open).Or.EqualTo(FloorPhase.Shrink));
            Assert.That(maxLive, Is.LessThanOrEqualTo(settings.MaxCount));
        }

        [UnityTest]
        public IEnumerator BoxesLeanTowardsTheCarBehindInKnockouts()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            var settings = tuning.Pickups;
            Set(settings, "firstDelay", 0.2f);
            Set(settings, "interval", 0.6f);
            Set(settings, "jitter", 0f);
            // Gone before the next appears, or the spacing between boxes would push it away from the car.
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

            yield return Until(() => spawned >= 8, 14f, "too few boxes appeared");
            Assert.That(near, Is.GreaterThanOrEqualTo(spawned * 0.8f),
                $"{near} of {spawned} boxes appeared by the car behind");
        }

        // --- The roll ---

        [Test]
        public void TheRollFavoursTheItemsThatKnockCarsOffForACarBehind()
        {
            var settings = new GameTuning.PickupSettings();
            const int Draws = 6000;
            float Comeback(float trailing)
            {
                var random = new System.Random(11);
                var count = 0;
                var kinds = new HashSet<ItemKind>();
                for (var i = 0; i < Draws; i++) {
                    var kind = ItemRoll.Choose(random, settings, trailing);
                    kinds.Add(kind);
                    if (kind is ItemKind.Shockwave or ItemKind.Bomb or ItemKind.Glove) {
                        count++;
                    }
                }

                Assert.That(kinds.Count, Is.EqualTo(6), "every item can come out of a box");
                return count / (float)Draws;
            }

            var leading = Comeback(0f);
            var trailing = Comeback(1f);
            Assert.That(trailing, Is.GreaterThan(leading + 0.15f),
                $"a car far behind should get comeback items more often ({trailing:P0} against {leading:P0})");

            // Seeded: the same dice give the same items, so a simulation repeats them.
            var first = new System.Random(5);
            var second = new System.Random(5);
            for (var i = 0; i < 50; i++) {
                Assert.That(ItemRoll.Choose(first, settings, 0.5f), Is.EqualTo(ItemRoll.Choose(second, settings, 0.5f)));
            }
        }

        [UnityTest]
        public IEnumerator TheTrailingShareComesFromTheMatchScore()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 7);
            var settings = tuning.Pickups;
            Assert.That(ItemRoll.TrailingOf(round.Score, 0, settings), Is.Zero, "level at the start");

            // Car 1 knocks car 2 off three times: the player trails by three, car 1 not at all.
            for (var i = 0; i < 3; i++) {
                round.Score.RecordFall(2, 1, 1f + i);
            }

            Assert.That(ItemRoll.GapOf(round.Score, 0), Is.EqualTo(3));
            Assert.That(ItemRoll.TrailingOf(round.Score, 0, settings), Is.EqualTo(1f).Within(0.001f));
            Assert.That(ItemRoll.TrailingOf(round.Score, 1, settings), Is.Zero);
        }

        [UnityTest]
        public IEnumerator ABrokenBoxSpinsTheSlotAndTheItemIsHeldUntilUsed()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 60f);
            Set(tuning.Pickups, "firstDelay", 100f);
            yield return Load(true, 7);
            var player = round.Player;
            yield return StandOnTheMiddle(player);
            var got = ItemKind.None;
            var used = new List<ItemKind>();
            pickups.Collected += (car, kind, _) => {
                if (car == player) {
                    got = kind;
                }
            };
            player.ItemUsed += used.Add;

            var box = pickups.Spawn(Vector3.zero);
            yield return Until(() => !box.IsLive, 2f, "the box was not broken");
            Assert.That(got, Is.Not.EqualTo(ItemKind.None), "an empty slot gets an item");
            Assert.That(player.HeldItem, Is.EqualTo(got));
            Assert.That(player.IsItemRolling, Is.True, "the slot spins first");
            Assert.That(pickups.Use(player), Is.False, "a spinning slot cannot be used");

            yield return Until(() => player.IsItemReady, tuning.Pickups.RollTime + 0.5f, "the roll never landed");
            yield return Frames(FramesPerSecond * 2);
            Assert.That(player.HeldItem, Is.EqualTo(got), "the item is kept until it is used");
            Assert.That(player.Ability, Is.EqualTo(ItemKind.None), "nothing runs before it is used");

            // A second box broken with the slot full gives nothing, as in a kart racer.
            got = ItemKind.Heavy;
            var heldBefore = player.HeldItem;
            yield return StandOnTheMiddle(player);
            var second = pickups.Spawn(Vector3.zero);
            yield return Until(() => !second.IsLive, 2f, "the second box was not broken");
            Assert.That(got, Is.EqualTo(ItemKind.None), "a full slot breaks the box for nothing");
            Assert.That(player.HeldItem, Is.EqualTo(heldBefore));

            // The item key uses it: the driver hears the press and the slot empties.
            input.PressItem();
            yield return Frames(2);
            Assert.That(used, Is.EqualTo(new[] { heldBefore }));
            Assert.That(player.HasItem, Is.False);
        }

        // --- The items, on use ---

        [UnityTest]
        public IEnumerator HeavyUsedMakesTheCarThreeTimesAsHeavyForAWhileAndWearsOff()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 1);
            var player = round.Player;
            yield return StandOnTheMiddle(player);
            var normal = player.Movement.Mass;
            Assert.That(normal, Is.EqualTo(tuning.Car.Mass).Within(0.001f));

            Assert.That(pickups.Give(player, ItemKind.Heavy), Is.True);
            yield return Frames(3);
            Assert.That(player.Movement.Mass, Is.EqualTo(normal).Within(0.001f), "holding it does nothing yet");
            Assert.That(pickups.Use(player), Is.True);
            Assert.That(player.Ability, Is.EqualTo(ItemKind.Heavy));
            yield return Frames(3);
            Assert.That(player.Movement.Mass, Is.EqualTo(normal * tuning.Pickups.Heavy.MassScale).Within(0.001f),
                "the physics must see the heavy mass, and so the push of its hits");

            yield return Until(() => player.Ability == ItemKind.None, tuning.Pickups.Heavy.Duration + 1f,
                "heavy never wore off");
            yield return Frames(3);
            Assert.That(player.Movement.Mass, Is.EqualTo(normal).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator AShockwaveUsedPushesTheCarsNearAndCreditsTheUser()
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
            shielded.StartAbility(ItemKind.Shield);
            ghost.SetInvulnerable(true, round.Cars);

            pickups.Give(player, ItemKind.Shockwave);
            Assert.That(pickups.Use(player), Is.True);
            yield return Frames(2);

            Assert.That(near.LastHitBy, Is.SameAs(player), "the ring's push is the user's");
            Assert.That(near.Movement.Velocity.x, Is.GreaterThan(4f), "the ring pushes outward");
            Assert.That(near.Movement.IsStaggered, Is.True);
            Assert.That(far.LastHitBy, Is.Null, "a car beyond the ring is left alone");
            Assert.That(ghost.LastHitBy, Is.Null, "a car just back from a fall is left alone");
            Assert.That(shielded.LastHitBy, Is.Null, "a shield takes the ring");
            Assert.That(shielded.Ability, Is.EqualTo(ItemKind.None), "the ring spends the shield");
            Assert.That(player.Ability, Is.EqualTo(ItemKind.None), "a shockwave leaves nothing running");

            // The fall that follows is the user's knock-out.
            near.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => near.IsOut, 5f, "the pushed car never fell");
            Assert.That(round.Score.KnockoutsOf(0), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AShieldUsedTakesTheNextCarHitWholeAndHazardHitsToo()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 60f);
            yield return Load(true, 7);
            var player = round.Player;
            var bot = round.Cars[1];
            yield return StandOnTheMiddle(player);

            UseNow(player, ItemKind.Shield);
            Assert.That(player.IsShielded, Is.True);

            // A bot rams it from three metres.
            var from = player.Position + Vector3.left * 3f;
            bot.Movement.Place(new Vector3(from.x, 0.2f, from.z), Quaternion.LookRotation(Vector3.right));
            bot.Movement.Push(Vector3.right * 10f);
            yield return Frames(FramesPerSecond / 2);
            Assert.That(player.Ability, Is.EqualTo(ItemKind.None), "the hit spent the shield");
            Assert.That(player.LastHitBy, Is.Null, "a hit the shield took gives no credit");
            Assert.That(player.Movement.IsStaggered, Is.False, "a hit the shield took does not unbalance the car");

            // An arena hazard's hit is taken back whole: no push, no stagger, nobody's hit. The rammer goes out of the way.
            bot.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return StandOnTheMiddle(player);
            UseNow(player, ItemKind.Shield);
            var velocity = player.Movement.Velocity;
            new HazardBlow(tuning, container.Resolve<RoundDifficulty>(), null, null)
                .Apply(player, Vector3.right, 9f, 3f, 0.5f, 1f);
            Assert.That((player.Movement.Velocity - velocity).magnitude, Is.LessThan(0.01f));
            Assert.That(player.Movement.IsStaggered, Is.False);
            Assert.That(float.IsNegativeInfinity(player.LastHazardTime), Is.True);
            Assert.That(player.Ability, Is.EqualTo(ItemKind.None));

            // Without a shield the same hazard hits.
            new HazardBlow(tuning, container.Resolve<RoundDifficulty>(), null, null)
                .Apply(player, Vector3.right, 9f, 3f, 0.5f, 1f);
            Assert.That(player.Movement.IsStaggered, Is.True);

            // And an unspent shield ends with its time.
            UseNow(player, ItemKind.Shield);
            yield return Until(() => player.Ability == ItemKind.None, tuning.Pickups.Shield.Duration + 1f,
                "the shield never ran out");
        }

        [UnityTest]
        public IEnumerator TurboUsedFiresTheBoostAgainAtOnceAndWearsOff()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 1);
            var player = round.Player;
            yield return StandOnTheMiddle(player);
            var turbo = tuning.Pickups.Turbo;

            UseNow(player, ItemKind.Turbo);
            Assert.That(player.Movement.TryBoost(), Is.True);
            Assert.That(player.Movement.TryBoost(), Is.False, "the boost still needs its short wait");
            yield return Until(() => player.Movement.IsBoostReady, turbo.BoostCooldown + 0.5f,
                "the boost did not come back within the turbo's wait");
            Assert.That(tuning.Boost.Cooldown, Is.GreaterThan(turbo.BoostCooldown * 3f), "the test needs a real difference");
            yield return StandOnTheMiddle(player);
            Assert.That(player.Movement.TryBoost(), Is.True, "a second boost within the turbo's few seconds");

            yield return Until(() => player.Ability == ItemKind.None, turbo.Duration + 1f, "turbo never wore off");
            yield return StandOnTheMiddle(player);
            player.Movement.TryBoost();
            yield return Frames(FramesPerSecond);
            Assert.That(player.Movement.IsBoostReady, Is.False, "the tuned cooldown applies again");
        }

        [UnityTest]
        public IEnumerator ABombLandsAheadBlinksAndBlowsTheCarsNearOutwardForTheThrower()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 60f);
            yield return Load(true, 7);
            var player = round.Player;
            var bomb = tuning.Pickups.Bomb;
            var landing = new Vector3(0f, 0f, bomb.ThrowDistance);
            var target = round.Cars[1];
            var shielded = round.Cars[2];
            var far = round.Cars[3];
            player.Movement.Place(new Vector3(0f, 0.2f, 0f), Quaternion.identity);
            target.Movement.Place(landing + new Vector3(bomb.Radius * 0.4f, 0.2f, 0f), Quaternion.identity);
            shielded.Movement.Place(landing + new Vector3(-bomb.Radius * 0.4f, 0.2f, 0f), Quaternion.identity);
            // Beyond the ring on the shielded side: clear of the thrower and of the car the blast sends the other way.
            far.Movement.Place(landing + new Vector3(-(bomb.Radius + 2.5f), 0.2f, 0f), Quaternion.identity);
            ParkTheRest(4);

            yield return Until(() => player.Movement.IsGrounded && target.Movement.IsGrounded, 2f, "the cars never landed");
            shielded.StartAbility(ItemKind.Shield);
            var hits = new List<Vector3>();
            player.ItemHit += (kind, point) => {
                if (kind == ItemKind.Bomb) {
                    hits.Add(point);
                }
            };

            UseNow(player, ItemKind.Bomb);
            // In the air and then blinking on the floor: nobody is hit before the fuse burns.
            yield return Frames(Mathf.CeilToInt((bomb.FlightTime + bomb.Fuse * 0.5f) * FramesPerSecond));
            Assert.That(target.LastHitBy, Is.Null, "the bomb went off too early");
            var ring = GameObject.Find("Bomb Ring");
            Assert.That(ring, Is.Not.Null, "the bomb's ring lies on the floor while it blinks");
            Assert.That(Vector3.ProjectOnPlane(ring.transform.position - landing, Vector3.up).magnitude, Is.LessThan(0.6f),
                "the bomb lands the throw's reach ahead of the nose");

            yield return Until(() => target.LastHitBy != null, bomb.Fuse + 1f, "the bomb never went off");
            Assert.That(target.LastHitBy, Is.SameAs(player), "the blast is the thrower's");
            Assert.That(target.Movement.Velocity.x, Is.GreaterThan(3f), "the blast pushes outward from the bomb");
            Assert.That(shielded.LastHitBy, Is.Null, "a shield takes the blast");
            Assert.That(shielded.Ability, Is.EqualTo(ItemKind.None), "the blast spends the shield");
            Assert.That(far.LastHitBy, Is.Null, "a car beyond the ring is left alone");
            Assert.That(player.LastHitBy, Is.Null, "the thrower is out of its own blast");
            Assert.That(hits, Has.Count.EqualTo(1));

            target.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => target.IsOut, 5f, "the blown car never fell");
            Assert.That(round.Score.KnockoutsOf(0), Is.EqualTo(1), "a car the bomb sends off is the thrower's knock-out");
        }

        [UnityTest]
        public IEnumerator ABombThatLandsOffTheFloorFallsAwayWithoutGoingOff()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 60f);
            yield return Load(true, 7);
            var player = round.Player;
            var extent = round.Edge.SafeExtent;
            // Near the rim, facing out: the throw lands beyond the floor.
            player.Movement.Place(new Vector3(0f, 0.2f, extent - 3f), Quaternion.identity);
            var bystander = round.Cars[1];
            bystander.Movement.Place(new Vector3(2.5f, 0.2f, extent - 3f), Quaternion.identity);
            ParkTheRest(2);
            yield return Frames(10);
            UseNow(player, ItemKind.Bomb);
            var bomb = tuning.Pickups.Bomb;
            yield return Frames(Mathf.CeilToInt((bomb.FlightTime + bomb.Fuse + 1f) * FramesPerSecond));
            Assert.That(bystander.LastHitBy, Is.Null, "a bomb off the floor must not go off");
            Assert.That(GameObject.Find("Bomb Ring"), Is.Null, "no ring is laid off the floor");
        }

        [UnityTest]
        public IEnumerator AGlovePunchesTheFirstCarAheadForTheUserAndAShieldStopsIt()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 60f);
            yield return Load(true, 7);
            var player = round.Player;
            var glove = tuning.Pickups.Glove;
            var first = round.Cars[1];
            var behind = round.Cars[2];
            var shielded = round.Cars[3];
            player.Movement.Place(new Vector3(0f, 0.2f, 0f), Quaternion.identity);
            first.Movement.Place(new Vector3(0f, 0.2f, 4f), Quaternion.identity);
            behind.Movement.Place(new Vector3(0f, 0.2f, 7f), Quaternion.identity);
            shielded.Movement.Place(new Vector3(0f, 0.2f, -4f), Quaternion.identity);
            ParkTheRest(4);

            yield return Until(() => player.Movement.IsGrounded && first.Movement.IsGrounded, 2f, "the cars never landed");
            var pows = 0;
            player.ItemHit += (kind, _) => pows += kind == ItemKind.Glove ? 1 : 0;

            UseNow(player, ItemKind.Glove);
            yield return Until(() => first.LastHitBy != null, glove.ExtendTime + glove.HoldTime + 0.3f,
                "the glove never reached the car ahead");
            Assert.That(first.LastHitBy, Is.SameAs(player), "the punch is the user's");
            Assert.That(behind.LastHitBy, Is.Null, "only the first car on the way is punched");
            Assert.That(pows, Is.EqualTo(1));
            Assert.That(first.Movement.Velocity.z, Is.GreaterThan(4f), "the punch sends it along the punch");
            Assert.That(first.Movement.Velocity.z,
                Is.LessThanOrEqualTo(tuning.Car.MaxBumpSpeed * glove.MaxSpeedShare + 0.5f), "the punch is capped");

            // Facing the other way, at a shielded car: the shield takes the punch.
            yield return Frames(Mathf.CeilToInt(glove.Length * FramesPerSecond) + 5);
            player.Movement.Place(new Vector3(0f, 0.2f, 0f), Quaternion.LookRotation(Vector3.back));
            yield return Frames(5);
            shielded.StartAbility(ItemKind.Shield);
            UseNow(player, ItemKind.Glove);
            yield return Frames(Mathf.CeilToInt(glove.Length * FramesPerSecond) + 5);
            Assert.That(shielded.LastHitBy, Is.Null, "a shield takes the punch");
            Assert.That(shielded.Ability, Is.EqualTo(ItemKind.None), "the punch spends the shield");
            Assert.That(pows, Is.EqualTo(1), "a punch the shield took is no hit");
        }

        // --- Who can break one ---

        [UnityTest]
        public IEnumerator ACarJustBackFromAFallCannotBreakABox()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            Set(tuning.Pickups, "firstDelay", 100f);
            yield return Load(true, 1);
            var player = round.Player;
            player.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => player.IsOut, 5f, "the player never fell");
            yield return Until(() => !player.IsOut, tuning.Match.RespawnDelay + 1f, "the player never came back");
            yield return Until(() => player.Movement.IsGrounded, tuning.Match.MaxDropTime + 1f, "it never landed");
            Assert.That(player.IsInvulnerable, Is.True);

            var box = pickups.Spawn(player.Position);
            yield return Frames(3);
            Assert.That(box.IsLive, Is.True, "a ghost broke a box");
            Assert.That(player.HasItem, Is.False);

            yield return Until(() => !player.IsInvulnerable, tuning.Match.Invulnerability + 3f, "its safe time never ended");
            yield return Until(() => !box.IsLive, 2f, "the car in play never broke it");
            Assert.That(player.HasItem, Is.True);
        }

        [UnityTest]
        public IEnumerator AFallLosesTheHeldItemAndEndsTheRunningOne()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 1);
            var player = round.Player;
            yield return StandOnTheMiddle(player);
            UseNow(player, ItemKind.Heavy);
            pickups.Give(player, ItemKind.Bomb);
            player.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => player.IsOut, 5f, "the player never fell");
            Assert.That(player.Ability, Is.EqualTo(ItemKind.None));
            Assert.That(player.HasItem, Is.False, "a fall loses the item in the slot");
            yield return Frames(3);
            Assert.That(player.Movement.Mass, Is.EqualTo(tuning.Car.Mass).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator ABoxOnAWaveThatStartsToShakeFades()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 40f);
            yield return Load(true, 1);
            var floor = container.Resolve<ArenaFloor>();
            // A spot in the second wave from the outside, far enough in to be safe until the wave shakes.
            var spot = new Vector3(0f, 0f, -(floor.Wave(1).Inner + floor.Wave(1).Outer) * 0.5f);
            Assert.That(floor.Contains(1, spot), Is.True);
            var box = pickups.Spawn(spot);
            yield return Frames(3);
            Assert.That(box.IsLive, Is.True, "the box faded before its wave shook");

            floor.Warn(1);
            yield return Frames(3);
            Assert.That(box.IsLive, Is.False, "a box on a shaking wave must fade");
            Assert.That(pickups.Live, Is.Empty);
        }

        // --- Bots ---

        [UnityTest]
        public IEnumerator ABotDrivesToANearbyBoxAndBreaksIt()
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
            var box = pickups.Spawn(spot);
            Assert.That(box, Is.Not.Null);
            yield return Until(() => takers.Any(car => car != round.Player), 10f, "no bot broke the box");
        }

        [UnityTest]
        public IEnumerator BotsUseTheItemsTheyHold()
        {
            tuning = UseMode(GameTuning.GameMode.Match, 60f);
            Set(tuning.Pickups, "firstDelay", 100f);
            yield return Load(false, 7);
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            botDriver.Seed = 5;
            round.Place();
            round.Begin();
            yield return Frames(2);

            var users = new HashSet<Car>();
            var kinds = new HashSet<ItemKind>();
            var items = new[] { ItemKind.Shield, ItemKind.Shockwave, ItemKind.Heavy, ItemKind.Glove, ItemKind.Turbo, ItemKind.Bomb };
            for (var i = 1; i < round.Cars.Count; i++) {
                var bot = round.Cars[i];
                var kind = items[(i - 1) % items.Length];
                Assert.That(pickups.Give(bot, kind), Is.True);
                bot.ItemUsed += used => {
                    users.Add(bot);
                    kinds.Add(used);
                };
            }

            // Within a few seconds of fighting most bots find their moment.
            yield return Until(() => users.Count >= 3, 15f, "too few bots used their items");
            Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(2), "bots use more than one kind of item");
        }

        // --- Events ---

        [Test]
        public void ItemEventsKeepWithinPokisNumberLimitAndNameTheKind()
        {
            var platform = new MatchTests.RecordingPlatform();
            var all = new[]
            {
                ItemKind.Heavy, ItemKind.Shockwave, ItemKind.Shield, ItemKind.Turbo, ItemKind.Bomb, ItemKind.Glove,
            };
            foreach (var kind in all) {
                platform.MeasureItemGet(12, kind);
                platform.MeasureItemUse(1, kind);
            }

            platform.MeasureMatchPickups(1, 0);
            platform.MeasureMatchPickups(2, 14);
            foreach (var (category, what, action) in platform.Events) {
                Assert.That(GameEvent.CountNumbers(category, what, action), Is.LessThanOrEqualTo(GameEvent.MaxNumbers),
                    $"{category} / {what} / {action}");
            }

            var names = platform.Events.Select(e => $"{e.Item1} / {e.Item2} / {e.Item3}").ToList();
            foreach (var id in new[] { "heavy", "shockwave", "shield", "turbo", "bomb", "glove" }) {
                Assert.That(names, Does.Contain($"match / item-{id}-r4plus / item-get"));
                Assert.That(names, Does.Contain($"match / item-{id}-r1 / item-use"));
            }

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
            input = new SimulationInput();
            CreateParent(input, new SimulationProgress { Round = visitRound }, "sandbox");
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

        /// <summary>Puts the cars from <paramref name="first"/> on in a row at the back of the floor, out of the way.</summary>
        private void ParkTheRest(int first)
        {
            for (var i = first; i < round.Cars.Count; i++) {
                round.Cars[i].Movement.Place(new Vector3(-9f + (i - first) * 3f, 0.2f, -9f), Quaternion.identity);
            }
        }

        /// <summary>Puts <paramref name="kind"/> in the car's slot and uses it at once.</summary>
        private void UseNow(Car car, ItemKind kind)
        {
            Assert.That(pickups.Give(car, kind), Is.True, $"the slot was full for {kind}");
            Assert.That(pickups.Use(car), Is.True, $"{kind} could not be used");
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
            // Game seconds, not frames: a hit-stop slows the game clock for a few frames whenever a bot rams the player.
            var frames = Mathf.CeilToInt(seconds * FramesPerSecond) * 4;
            var start = Time.time;
            for (var frame = 0; frame < frames && Time.time - start < seconds && !condition(); frame++) {
                yield return null;
            }

            Assert.That(condition(), Is.True,
                $"{message} (game time {Time.time - start:0.00} s of {seconds:0.00} s, time scale {Time.timeScale})");
        }
    }
}
