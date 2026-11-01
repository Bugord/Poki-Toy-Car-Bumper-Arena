using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The floor that comes back in cycles: the schedule as a plain function of time, a fallen floor put back on every
    /// shape, the whole cycle in the real Game scene on the round and two tiled floors, a car caught in a landing tile
    /// lifted onto it, and the old single crumble untouched with the switch off.
    /// </summary>
    public sealed class FloorCyclesTests : CycleFixture
    {
        // --- The schedule ---

        [Test]
        public void EveryWaveFallsWithinTheShrinkAndComesBackWithinTheReturn([Values(1, 5, 11)] int waves)
        {
            var schedule = new FloorCycleSchedule(3, 15f, 25f, 7f, 3f, 1.2f, 2f, waves);
            Assert.That(schedule.Length, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(schedule.Warning, Is.EqualTo(2f));
            for (var w = 0; w < waves; w++) {
                Assert.That(schedule.WarnAt(w), Is.GreaterThanOrEqualTo(schedule.ShrinkAt), $"wave {w} shakes before the shrink");
                Assert.That(schedule.FallAt(w), Is.LessThanOrEqualTo(schedule.PeakAt + 0.0001f), $"wave {w} falls after the shrink");
                Assert.That(schedule.LandAt(w), Is.GreaterThanOrEqualTo(schedule.ReturnAt + 1.2f - 0.0001f));
                Assert.That(schedule.LandAt(w), Is.LessThan(schedule.Length), $"wave {w} lands after the cycle");
                if (w > 0) {
                    Assert.That(schedule.WarnAt(w), Is.GreaterThan(schedule.WarnAt(w - 1)), "the waves go from the outside in");
                    Assert.That(schedule.LandAt(w), Is.LessThan(schedule.LandAt(w - 1)), "the innermost lands first");
                }
            }

            if (waves > 1) {
                Assert.That(schedule.WarnAt(0), Is.EqualTo(schedule.ShrinkAt), "the first shakes as the shrink begins");
            }

            Assert.That(schedule.FallAt(waves - 1), Is.EqualTo(schedule.PeakAt).Within(0.0001f), "the last falls as the shrink ends");
        }

        [Test]
        public void TheFloorIsAFunctionOfTime()
        {
            const int waves = 5;
            var schedule = new FloorCycleSchedule(3, 15f, 25f, 7f, 3f, 1.2f, 2f, waves);
            for (var cycle = 0; cycle < 3; cycle++) {
                var start = cycle * schedule.Length;
                Assert.That(schedule.PhaseAt(start + 1f), Is.EqualTo(FloorPhase.Open));
                Assert.That(schedule.PhaseAt(start + 20f), Is.EqualTo(FloorPhase.Shrink));
                Assert.That(schedule.PhaseAt(start + 42f), Is.EqualTo(FloorPhase.Peak));
                Assert.That(schedule.PhaseAt(start + 48f), Is.EqualTo(FloorPhase.Return));
                schedule.CycleTime(start + 1f, out var found);
                Assert.That(found, Is.EqualTo(cycle));
                for (var w = 0; w < waves; w++) {
                    Assert.That(schedule.StateAt(w, start + 1f), Is.EqualTo(WaveState.Standing));
                    Assert.That(schedule.StateAt(w, start + schedule.WarnAt(w) + 0.5f), Is.EqualTo(WaveState.Warning));
                    Assert.That(schedule.StateAt(w, start + schedule.PeakAt + 0.1f), Is.EqualTo(WaveState.Gone));
                    Assert.That(schedule.StateAt(w, start + schedule.ReturnAt + 0.1f), Is.EqualTo(WaveState.Returning));
                    Assert.That(schedule.StateAt(w, start + schedule.Length - 0.01f), Is.EqualTo(WaveState.Standing));
                }
            }

            // After the last cycle the floor stays as it left it: whole, in the last cycle's return.
            schedule.CycleTime(500f, out var last);
            Assert.That(last, Is.EqualTo(2));
            Assert.That(schedule.PhaseAt(500f), Is.EqualTo(FloorPhase.Return));
            for (var w = 0; w < waves; w++) {
                Assert.That(schedule.StateAt(w, 500f), Is.EqualTo(WaveState.Standing));
            }

            // No limit goes round for ever.
            var endless = new FloorCycleSchedule(0, 15f, 25f, 7f, 3f, 1.2f, 2f, waves);
            endless.CycleTime(1020f, out var many);
            Assert.That(many, Is.EqualTo(20));
            Assert.That(endless.PhaseAt(1020f), Is.EqualTo(FloorPhase.Shrink));
        }

        [Test]
        public void AMatchKeepsTheInnerWavesStanding()
        {
            const int waves = 8;
            var schedule = new FloorCycleSchedule(3, 15f, 25f, 7f, 3f, 1.2f, 2f, waves, 3);
            Assert.That(schedule.FallingWaves, Is.EqualTo(3));
            Assert.That(schedule.WarnAt(0), Is.EqualTo(schedule.ShrinkAt));
            Assert.That(schedule.FallAt(2), Is.EqualTo(schedule.PeakAt).Within(0.0001f), "the last falling wave ends the shrink");
            for (var w = 0; w < waves; w++) {
                var falls = w < 3;
                Assert.That(schedule.Falls(w), Is.EqualTo(falls));
                Assert.That(schedule.StateAt(w, schedule.PeakAt + 1f),
                    Is.EqualTo(falls ? WaveState.Gone : WaveState.Standing), $"wave {w} at the peak");
                Assert.That(schedule.StateAt(w, schedule.ReturnAt + 0.1f),
                    Is.EqualTo(falls ? WaveState.Returning : WaveState.Standing), $"wave {w} in the return");
            }

            Assert.That(schedule.LandAt(2), Is.EqualTo(schedule.ReturnAt + 1.2f).Within(0.0001f), "the innermost fallen lands first");
            Assert.That(schedule.LandAt(0), Is.LessThan(schedule.Length));

            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            Assert.That(tuning.FloorCycles.MatchFallingWaves(0), Is.EqualTo(0));
            Assert.That(tuning.FloorCycles.MatchFallingWaves(1), Is.EqualTo(1), "a lone wave still falls");
            Assert.That(tuning.FloorCycles.MatchFallingWaves(11), Is.InRange(1, 10), "a match never drops every wave");
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void AShortShrinkShortensTheShaking()
        {
            var schedule = new FloorCycleSchedule(1, 1f, 1.5f, 1f, 2f, 0.8f, 2f, 3);
            Assert.That(schedule.Warning, Is.EqualTo(1.5f));
            Assert.That(schedule.FallAt(2), Is.EqualTo(schedule.PeakAt).Within(0.0001f));
        }

        // --- The floor put back ---

        [UnityTest]
        public IEnumerator AFallenFloorComesBack([Values("sandbox", "tabletop", "candy", "playroom")] string map)
        {
            yield return Load(map, null);
            var floor = Container.Resolve<ArenaFloor>();
            var outer = OuterPoint(floor);
            var full = floor.FullExtent;
            var whole = (floor.SafeExtent, floor.SolidExtent);
            var changes = 0;
            floor.Changed += () => changes++;
            Assert.That(HitsFloor(outer), Is.True);

            for (var w = 0; w < floor.WaveCount; w++) {
                floor.Warn(w);
                floor.Drop(w);
            }

            Assert.That(HitsFloor(outer), Is.False, $"{map}: the edge fell");
            Assert.That(floor.DistanceToEdge(outer), Is.LessThan(0f));
            Assert.That(floor.SolidExtent, Is.LessThan(full - 1f));

            for (var w = floor.WaveCount - 1; w >= 0; w--) {
                floor.Raise(w, 0.5f);
                Assert.That(floor.StateOf(w), Is.EqualTo(WaveState.Returning));
            }

            yield return new WaitForSeconds(0.2f);
            Assert.That(HitsFloor(outer), Is.False, $"{map}: a tile in the air holds nothing up");
            Assert.That(floor.DistanceToSolidEdge(outer), Is.LessThan(0f), $"{map}: nor is it floor before it lands");

            var before = changes;
            for (var w = floor.WaveCount - 1; w >= 0; w--) {
                floor.Restore(w);
            }

            yield return null;
            Assert.That(changes, Is.GreaterThan(before), $"{map}: the readers of the edge hear it come back");
            AssertWhole(floor, outer, whole, map);
        }

        // --- The whole cycle in the scene ---

        [UnityTest]
        public IEnumerator TheFloorGoesInCyclesAndComesBack(
            [Values("sandbox/rake", "candy/frosting", "playroom/road")] string arena)
        {
            var parts = arena.Split('/');
            EnableCycles(2, 1f, 4f, 1f, 2f, 0.8f);
            yield return Load(parts[0], parts[1]);
            var floor = Container.Resolve<ArenaFloor>();
            var crumble = Container.Resolve<ArenaCrumble>();
            Assert.That(crumble.IsCycling, Is.True);
            var outer = OuterPoint(floor);
            var full = floor.FullExtent;
            var whole = (floor.SafeExtent, floor.SolidExtent);
            var events = new List<string>();
            var fell = 0;
            var landed = 0;
            crumble.CycleStarted += cycle => events.Add($"start {cycle}");
            crumble.FloorReturning += cycle => events.Add($"return {cycle}");
            var warnedAt = new List<(int wave, float time)>();
            var fellAt = new List<(int wave, float time)>();
            var landedAt = new List<(int wave, float time)>();
            crumble.WaveWarning += wave => warnedAt.Add((wave.Index, Round.Elapsed));
            crumble.WaveFell += wave => {
                fell++;
                fellAt.Add((wave.Index, Round.Elapsed));
            };
            crumble.WaveLanded += wave => {
                landed++;
                landedAt.Add((wave.Index, Round.Elapsed));
            };
            StandOnTheCore(Container.Resolve<ArenaMap>());

            BeginRound();
            var length = crumble.Schedule.Length;
            yield return Until(() => crumble.Cycle == 0 && crumble.Phase == FloorPhase.Peak, length, $"{arena}: no peak");
            Assert.That(fell, Is.EqualTo(floor.WaveCount), $"{arena}: every wave fell in the shrink");
            for (var w = 0; w < floor.WaveCount; w++) {
                Assert.That(floor.StateOf(w), Is.EqualTo(WaveState.Falling).Or.EqualTo(WaveState.Gone));
            }

            Assert.That(floor.DistanceToEdge(outer), Is.LessThan(0f));
            Assert.That(HitsFloor(outer), Is.False);
            Assert.That(floor.SolidExtent, Is.LessThan(full - 1f), $"{arena}: only the core is left");

            // Spread over the shrink, from the outside in, not all at once.
            var schedule = crumble.Schedule;
            var frame = 3f / 60f;
            Assert.That(warnedAt.Select(entry => entry.wave), Is.EqualTo(Enumerable.Range(0, floor.WaveCount)));
            Assert.That(warnedAt.Select(entry => entry.time), Is.Ordered.Ascending);
            Assert.That(warnedAt[0].time, Is.EqualTo(schedule.ShrinkAt).Within(frame), $"{arena}: the shrink began late");
            Assert.That(fellAt[^1].time, Is.EqualTo(schedule.PeakAt).Within(frame), $"{arena}: the last fell off time");
            if (floor.WaveCount > 2) {
                Assert.That(warnedAt[1].time - warnedAt[0].time, Is.GreaterThan(frame), $"{arena}: the waves came together");
            }

            yield return Until(() => crumble.Cycle == 1, length, $"{arena}: no second cycle");
            Assert.That(landed, Is.EqualTo(floor.WaveCount), $"{arena}: every wave landed");
            Assert.That(landedAt.Select(entry => entry.wave),
                Is.EqualTo(Enumerable.Range(0, floor.WaveCount).Reverse()), $"{arena}: the innermost lands first");
            Assert.That(landedAt[0].time, Is.GreaterThanOrEqualTo(schedule.ReturnAt + 0.8f - frame));
            Assert.That(landedAt[^1].time, Is.LessThan(length), $"{arena}: the floor came back after the cycle");
            AssertWhole(floor, outer, whole, arena);

            yield return Until(() => Round.Elapsed > 2f * length + 1f, 2f * length + 2f, $"{arena}: the round stopped");
            Assert.That(events, Is.EqualTo(new[] { "start 0", "return 0", "start 1", "return 1" }));
            Assert.That(fell, Is.EqualTo(2 * floor.WaveCount), "after the last cycle nothing falls");
            AssertWhole(floor, outer, whole, arena);
            Assert.That(Round.Cars.Where(car => car.IsOut), Is.Empty, $"{arena}: a car on the core fell");
        }

        [UnityTest]
        public IEnumerator ACarCaughtInALandingTileIsLiftedOntoIt()
        {
            EnableCycles(1, 1f, 4f, 1f, 2f, 0.8f);
            yield return Load("sandbox", "rake");
            var floor = Container.Resolve<ArenaFloor>();
            var crumble = Container.Resolve<ArenaCrumble>();
            StandOnTheCore(Container.Resolve<ArenaMap>());
            BeginRound();

            // The outermost ring lands last; the car is put in its slab a few frames before, as if it had slipped into the
            // gap, so it has no time to fall clear.
            var landAt = crumble.Schedule.LandTime(0, 0);
            yield return Until(() => Round.Elapsed + 3f / 60f >= landAt, landAt + 1f, "the ring never came back");
            Assert.That(floor.StateOf(0), Is.EqualTo(WaveState.Returning));
            var car = Round.Cars.First(candidate => candidate != Round.Player);
            var spot = new Vector3(0f, -0.6f, -14f);
            car.Movement.Place(spot, Quaternion.LookRotation(Vector3.right));
            yield return Until(() => floor.StateOf(0) == WaveState.Standing, 1f, "the ring did not land");
            Assert.That(car.Position.y, Is.GreaterThan(-0.05f), "lifted onto the tile it was caught in");

            yield return new WaitForSeconds(0.6f);
            Assert.That(car.IsOut, Is.False);
            Assert.That(car.Position.y, Is.GreaterThan(-0.1f).And.LessThan(0.5f), "it stands on the ring");
            Assert.That(Flat(car.Position).magnitude, Is.EqualTo(14f).Within(0.5f));
        }

        // --- The switch off ---

        [UnityTest]
        public IEnumerator WithoutCyclesTheEdgeCrumblesOnce()
        {
            SetField(LoadTuning().FloorCycles, "enabled", false);
            yield return Load("sandbox", "rake");
            var crumble = Container.Resolve<ArenaCrumble>();
            var settings = Container.Resolve<GameTuning.ArenaSettings>();
            Assert.That(crumble.IsCycling, Is.False);
            Assert.That(crumble.Schedule, Is.Null);
            var warnedAt = -1f;
            var fellAt = -1f;
            var cycles = 0;
            crumble.WaveWarning += wave => {
                if (wave.Index == 0) {
                    warnedAt = Round.Elapsed;
                }
            };
            crumble.WaveFell += wave => {
                if (wave.Index == 0) {
                    fellAt = Round.Elapsed;
                }
            };
            crumble.CycleStarted += _ => cycles++;
            crumble.WaveLanded += _ => cycles++;
            BeginRound();

            var due = settings.CrumbleDelay + settings.CrumbleWarning;
            yield return Until(() => fellAt >= 0f, due + 2f, "the edge did not fall");
            Assert.That(warnedAt, Is.EqualTo(settings.CrumbleDelay).Within(0.05f));
            Assert.That(fellAt, Is.EqualTo(due).Within(0.05f));
            Assert.That(cycles, Is.Zero);
            Assert.That(crumble.Cycle, Is.EqualTo(-1));
            Assert.That(Container.Resolve<ArenaFloor>().StateOf(1), Is.EqualTo(WaveState.Standing));
        }

        // --- Helpers ---

        /// <summary>
        /// Stands every car at rest on the floor that never crumbles, well apart and facing away from the nearest void, so
        /// the round runs through the cycles with nobody falling.
        /// </summary>
        private void StandOnTheCore(ArenaMap map)
        {
            var spots = new List<Vector3>();
            var headings = new List<Vector3>();
            if (map.Shape == null) {
                for (var i = 0; i < Round.Cars.Count; i++) {
                    var angle = i * Mathf.PI * 2f / Round.Cars.Count;
                    var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    spots.Add(direction * 3.2f);
                    headings.Add(-direction);
                }
            }
            else {
                var core = new GridEdge(map.Shape);
                core.Update(Enumerable.Repeat(WaveState.Gone, map.Shape.WaveCount).ToArray());
                for (var x = -20f; x <= 20f && spots.Count < Round.Cars.Count; x += 0.5f) {
                    for (var z = -25f; z <= 25f && spots.Count < Round.Cars.Count; z += 0.5f) {
                        var spot = new Vector3(x, 0f, z);
                        if (core.DistanceToEdge(spot) < 2f || spots.Any(other => (other - spot).magnitude < 3.2f)) {
                            continue;
                        }

                        spots.Add(spot);
                        var inward = core.Inward(spot);
                        headings.Add(inward.sqrMagnitude > 0.0001f ? inward : Vector3.forward);
                    }
                }
            }

            Assert.That(spots.Count, Is.EqualTo(Round.Cars.Count), "no room on the core for every car");
            for (var i = 0; i < Round.Cars.Count; i++) {
                var car = Round.Cars[i];
                car.Movement.Place(spots[i] + Vector3.up * 0.05f, Quaternion.LookRotation(headings[i]));
                car.Movement.Drive(Vector3.zero, 0f);
            }
        }

        /// <summary>A point on the outermost wave: the first to fall and the last to come back.</summary>
        private static Vector3 OuterPoint(ArenaFloor floor)
        {
            var wave = floor.Wave(0);
            if (wave.Spots == null) {
                return new Vector3(0f, 0f, -(wave.Inner + wave.Outer) * 0.5f);
            }

            var spot = wave.Spots[0];
            return new Vector3(spot.x, 0f, spot.y);
        }

        private static bool HitsFloor(Vector3 above)
        {
            var mask = 1 << LayerMask.NameToLayer("Arena");
            return Physics.Raycast(above + Vector3.up * 3f, Vector3.down, 8f, mask);
        }

        private static void AssertWhole(ArenaFloor floor, Vector3 outer, (float safe, float solid) whole, string arena)
        {
            for (var w = 0; w < floor.WaveCount; w++) {
                Assert.That(floor.StateOf(w), Is.EqualTo(WaveState.Standing), $"{arena}: wave {w} is not back");
            }

            Assert.That(floor.SafeExtent, Is.EqualTo(whole.safe).Within(0.001f), $"{arena}: the safe floor is not whole");
            Assert.That(floor.SolidExtent, Is.EqualTo(whole.solid).Within(0.001f), $"{arena}: the solid floor is not whole");
            Assert.That(floor.DistanceToEdge(outer), Is.GreaterThan(0f), $"{arena}: the outer floor is not safe");
            Assert.That(floor.DistanceToSolidEdge(outer), Is.GreaterThan(0f));
            Assert.That(HitsFloor(outer), Is.True, $"{arena}: the collider did not come back");
        }
    }
}
