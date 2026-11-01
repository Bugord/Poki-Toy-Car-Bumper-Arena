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
    /// The floor's cycles in a long match on the real Game scene: the peak keeps room for every car and one coming back,
    /// the arena's variants turn with the cycles (the floor's look and the mechanic, which stops harmless and starts again
    /// afresh), and a round keeps its variant.
    /// </summary>
    public sealed class MatchCyclesTests : CycleFixture
    {
        // The visit round that plays the most bots, so the floor has to hold the fullest match.
        private const int FullRound = 7;

        [UnityTest]
        public IEnumerator ThePeakOfAMatchLeavesRoomToComeBack(
            [Values("sandbox", "tabletop", "candy", "playroom")] string map)
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 60f);
            EnableCycles(1, 1f, 4f, 3f, 2f, 0.8f);
            yield return Load(map, null, visitRound: FullRound);
            var crumble = Container.Resolve<ArenaCrumble>();
            var floor = Container.Resolve<ArenaFloor>();
            Assert.That(Round.Cars.Count, Is.EqualTo(7), "the fullest match has seven cars");
            BeginRound();
            yield return Until(() => crumble.Phase == FloorPhase.Peak, 10f, $"{map}: no peak");
            var fallen = Enumerable.Range(0, floor.WaveCount).Count(w => floor.StateOf(w) != WaveState.Standing);
            Assert.That(fallen, Is.EqualTo(crumble.Schedule.FallingWaves).And.GreaterThan(0), $"{map}: the floor shrank");
            Assert.That(fallen, Is.LessThan(floor.WaveCount), $"{map}: a match keeps part of the floor");

            // Every car at rest on the floor that is left, well apart, then one more place to come back to.
            var spots = new List<Vector3>();
            for (var x = -25f; x <= 25f; x += 0.5f) {
                for (var z = -25f; z <= 25f; z += 0.5f) {
                    var spot = new Vector3(x, 0f, z);
                    if (floor.DistanceToEdge(spot) >= 2f && spots.All(other => (other - spot).magnitude >= 3.2f)) {
                        spots.Add(spot);
                    }
                }
            }

            Debug.Log($"{map}: {fallen} of {floor.WaveCount} waves fall, safe extent {floor.SafeExtent:0.0} m, " +
                $"{spots.Count} places 3.2 m apart");
            Assert.That(spots.Count, Is.GreaterThanOrEqualTo(Round.Cars.Count), $"{map}: no room for every car");
            for (var i = 0; i < Round.Cars.Count; i++) {
                Stand(Round.Cars[i], spots[i]);
            }

            yield return null;
            var settings = tuning.Match;
            var back = RespawnSpot.Find(floor, settings, Round.Cars, null);
            var nearest = Round.Cars.Min(car => (Flat(car.Position) - back).magnitude);
            Debug.Log($"{map}: comes back {floor.DistanceToEdge(back):0.0} m from the edge, {nearest:0.0} m from a car");
            Assert.That(floor.DistanceToEdge(back), Is.GreaterThanOrEqualTo(settings.SpawnMargin - 0.01f),
                $"{map}: no safe floor to come back on");
            Assert.That(nearest, Is.GreaterThanOrEqualTo(3f), $"{map}: it comes back on top of a car");
        }

        [UnityTest]
        public IEnumerator TheVariantsTurnWithTheCycles(
            [Values("tabletop/board", "candy/frosting", "playroom/road")] string arena)
        {
            var parts = arena.Split('/');
            var map = parts[0];
            // Four short cycles, so the start variant comes round again; the mechanics start soon after a cycle does.
            const int cycles = 4;
            var tuning = UseMode(GameTuning.GameMode.Match, cycles * 9f);
            EnableCycles(cycles, 3f, 3f, 1f, 2f, 0.8f);
            SetField(tuning.Ball, "delay", 0.5f);
            SetField(tuning.Train, "delay", 0.5f);
            yield return Load(map, parts[1]);
            var crumble = Container.Resolve<ArenaCrumble>();
            var floor = Container.Resolve<ArenaFloor>();
            var variants = Container.Resolve<ArenaVariants>();
            var settings = Tuning.Maps.First(entry => entry.Id == map);
            var theme = LoadArenaCatalog().Find(map);
            Assert.That(variants.IsRotating, Is.True);
            var order = settings.Variants.Select(variant => variant.Id).ToList();
            var start = order.IndexOf(parts[1]);
            var changes = new List<string>();
            variants.VariantChanged += changes.Add;
            HoldTheCars();
            BeginRound();
            var length = crumble.Schedule.Length;
            var check = new MechanicCheck(map, Container);

            for (var cycle = 0; cycle < cycles; cycle++) {
                var expected = order[(start + cycle) % order.Count];
                var current = cycle;
                yield return Until(() => crumble.Cycle == current, length + 1f, $"{arena}: no cycle {cycle}");
                yield return null;
                yield return null;
                Assert.That(variants.Variant, Is.EqualTo(expected), $"{arena}: cycle {cycle} plays the wrong variant");
                Assert.That(variants.Cycle, Is.EqualTo(cycle));
                var look = theme.FloorMaterialOf(expected);
                Assert.That(floor.CoreMaterial, Is.EqualTo(look), $"{arena}: the core is not in the {expected} look");
                for (var w = 0; w < floor.WaveCount; w++) {
                    Assert.That(floor.MaterialOf(w, 0), Is.EqualTo(look), $"{arena}: wave {w} is not in the {expected} look");
                }

                check.AtStart(cycle, expected);
                yield return Until(() => crumble.Phase == FloorPhase.Peak, length, $"{arena}: no peak in cycle {cycle}");
                check.AtPeak(cycle);
                if (cycle == cycles - 1) {
                    break;
                }

                // The floor comes back: the mechanic stops at once and its look is gone soon after.
                yield return Until(() => crumble.Phase == FloorPhase.Return, length, $"{arena}: no return in cycle {cycle}");
                Assert.That(variants.IsLive, Is.False, $"{arena}: the mechanic of cycle {cycle} still acts in the return");
                check.Harmless(cycle);
                yield return new WaitForSeconds(MechanicRun.LeaveTime + 0.1f);
                check.Gone(cycle);
            }

            Assert.That(changes, Is.EqualTo(Enumerable.Range(1, cycles - 1).Select(k => order[(start + k) % order.Count])),
                $"{arena}: the variants did not turn in order");
            yield return Until(() => Round.IsFinished, length + 2f, $"{arena}: the match did not end");
            Assert.That(variants.Variant, Is.EqualTo(order[(start + cycles - 1) % order.Count]),
                "the last cycle keeps its variant to the end");
        }

        [UnityTest]
        public IEnumerator ARoundKeepsItsVariantThroughTheCycles()
        {
            EnableCycles(2, 1f, 2f, 1f, 2f, 0.8f);
            yield return Load("tabletop", "cloth");
            var crumble = Container.Resolve<ArenaCrumble>();
            var floor = Container.Resolve<ArenaFloor>();
            var variants = Container.Resolve<ArenaVariants>();
            var top = Container.Resolve<ArenaTop>();
            var look = floor.CoreMaterial;
            var changes = 0;
            variants.VariantChanged += _ => changes++;
            Assert.That(variants.IsRotating, Is.False);
            Assert.That(crumble.IsCycling, Is.True);
            HoldTheCars();
            BeginRound();
            yield return Until(() => crumble.Cycle == 1, crumble.Schedule.Length * 2f, "no second cycle");
            yield return Until(() => crumble.Phase == FloorPhase.Return, crumble.Schedule.Length, "no second return");
            yield return new WaitForSeconds(1f);
            Assert.That(changes, Is.Zero);
            Assert.That(variants.Variant, Is.EqualTo("cloth"));
            Assert.That(variants.Cycle, Is.Zero);
            Assert.That(variants.IsLive, Is.True);
            Assert.That(top.IsBuilt, Is.True, "a round's mechanic stays to the end");
            Assert.That(floor.CoreMaterial, Is.EqualTo(look));
            Assert.That(floor.MaterialOf(0, 0), Is.EqualTo(look));
        }

        [UnityTest]
        public IEnumerator CarsOnTheTurntableStayOnItAtThePeak()
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 60f);
            EnableCycles(1, 1f, 3f, 6f, 2f, 0.8f);
            SetField(tuning.Carousel, "startDelay", 0.2f);
            yield return Load("playroom", "puzzle", visitRound: FullRound);
            var crumble = Container.Resolve<ArenaCrumble>();
            var disc = Container.Resolve<CarouselDisc>();
            BeginRound();
            yield return Until(() => crumble.Phase == FloorPhase.Peak, 10f, "no peak");
            Assert.That(disc.Exists, Is.True);
            // At rest round the turntable's rim, where it carries a car fastest and nearest the pits.
            var ring = disc.Radius - 0.6f;
            for (var i = 0; i < Round.Cars.Count; i++) {
                var angle = (i + 0.5f) * Mathf.PI * 2f / Round.Cars.Count;
                Stand(Round.Cars[i], new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring);
            }

            var peakEnd = crumble.Schedule.ReturnAt;
            yield return Until(() => Round.Elapsed >= peakEnd, 10f, "the peak did not end");
            Debug.Log($"playroom: turntable {disc.Radius:0.0} m at the peak");
            Assert.That(Mathf.Abs(disc.AngularVelocity), Is.GreaterThan(0.1f), "the turntable stood still");
            Assert.That(Round.Cars.Where(car => car.IsOut || car.Position.y < -0.5f), Is.Empty,
                "a car carried by the turntable at the peak fell");
        }

        /// <summary>
        /// Stands every car at rest apart, as near the middle as the floor allows (in the holes of a ring floor, the band
        /// round them), so the floor that falls takes as few of them as it can.
        /// </summary>
        private void HoldTheCars()
        {
            var candidates = new List<Vector3>();
            for (var x = -20f; x <= 20f; x += 0.5f) {
                for (var z = -25f; z <= 25f; z += 0.5f) {
                    var spot = new Vector3(x, 0f, z);
                    if (Edge.DistanceToEdge(spot) >= 2.5f) {
                        candidates.Add(spot);
                    }
                }
            }

            var spots = new List<Vector3>();
            foreach (var spot in candidates.OrderBy(spot => spot.magnitude)) {
                if (spots.Count < Round.Cars.Count && spots.All(other => (other - spot).magnitude >= 3.2f)) {
                    spots.Add(spot);
                }
            }

            for (var i = 0; i < Mathf.Min(spots.Count, Round.Cars.Count); i++) {
                Stand(Round.Cars[i], spots[i]);
            }
        }

        /// <summary>What each arena's own mechanics must show at each point of the cycles.</summary>
        private sealed class MechanicCheck
        {
            private readonly string map;
            private readonly ArenaHazards hazards;
            private readonly BallLane lane;
            private readonly ArenaTop top;
            private readonly ArenaPencil pencil;
            private readonly CandyPatches patches;
            private readonly JellyPads jelly;
            private readonly CandyStirrer stirrer;
            private readonly ToyTrain train;
            private readonly JackBoxes jack;
            private readonly CarouselDisc carousel;
            private int ballPasses;

            public MechanicCheck(string map, IObjectResolver container)
            {
                this.map = map;
                this.hazards = container.Resolve<ArenaHazards>();
                this.lane = container.Resolve<BallLane>();
                this.top = container.Resolve<ArenaTop>();
                this.pencil = container.Resolve<ArenaPencil>();
                this.patches = container.Resolve<CandyPatches>();
                this.jelly = container.Resolve<JellyPads>();
                this.stirrer = container.Resolve<CandyStirrer>();
                this.train = container.Resolve<ToyTrain>();
                this.jack = container.Resolve<JackBoxes>();
                this.carousel = container.Resolve<CarouselDisc>();
            }

            public void AtStart(int cycle, string variant)
            {
                switch (variant) {
                    case "board":
                        Assert.That(top.IsBuilt || pencil.IsBuilt, Is.False, "another tabletop mechanic stayed");
                        Assert.That(hazards.All, Is.Empty, "a hazard of another variant is still in the bots' way");
                        ballPasses = lane.Pass;
                        break;
                    case "cloth":
                        Assert.That(top.IsBuilt, Is.True, "the top was not built for its cycle");
                        Assert.That(hazards.All, Is.EqualTo(new IArenaHazard[] { top }));
                        Assert.That(lane.IsActive, Is.False, "the ball rolls on outside its cycle");
                        break;
                    case "notebook":
                        Assert.That(pencil.IsBuilt, Is.True, "the pencil was not built for its cycle");
                        Assert.That(top.IsBuilt, Is.False, "the top stayed after its cycle");
                        Assert.That(hazards.All, Is.EqualTo(new IArenaHazard[] { pencil }));
                        break;
                    case "frosting":
                        Assert.That(patches.Count, Is.EqualTo(6), "the patches were not laid for their cycle");
                        for (var i = 0; i < patches.Count; i++) {
                            var centre = patches.CenterOf(i);
                            Assert.That(patches.Sample(new Vector3(centre.x, 0f, centre.y)), Is.Not.EqualTo(SurfaceFeel.Neutral),
                                $"patch {i} does not act at the start of cycle {cycle}");
                        }

                        Assert.That(jelly.Exists || stirrer.Exists, Is.False, "another candy mechanic stayed");
                        break;
                    case "chocolate":
                        Assert.That(jelly.Exists, Is.True, "the pads were not laid for their cycle");
                        Assert.That(patches.Count, Is.Zero, "the patches stayed after their cycle");
                        break;
                    case "waffle":
                        Assert.That(stirrer.Exists, Is.True, "the stirrer was not built for its cycle");
                        Assert.That(jelly.Exists, Is.False, "the pads stayed after their cycle");
                        Assert.That(hazards.All, Is.EqualTo(new IArenaHazard[] { stirrer }));
                        break;
                    case "road":
                        Assert.That(train.Exists, Is.True, "no train for its cycle");
                        Assert.That(train.IsDerailed, Is.False, "a new cycle's train starts on the rail");
                        Assert.That(jack.Exists || carousel.Exists, Is.False, "another playroom mechanic stayed");
                        Assert.That(hazards.All, Is.EqualTo(new IArenaHazard[] { train }));
                        break;
                    case "blocks":
                        Assert.That(jack.Exists, Is.True, "the boxes were not laid for their cycle");
                        Assert.That(train.Exists, Is.False, "the train stayed after its cycle");
                        Assert.That(hazards.All, Is.EqualTo(new IArenaHazard[] { jack }));
                        break;
                    case "puzzle":
                        Assert.That(carousel.Exists, Is.True, "no turntable for its cycle");
                        Assert.That(jack.Exists, Is.False, "the boxes stayed after their cycle");
                        Assert.That(hazards.All, Is.Empty);
                        break;
                }
            }

            public void AtPeak(int cycle)
            {
                switch (map) {
                    case "tabletop" when cycle % 3 == 0:
                        Assert.That(lane.Pass, Is.GreaterThan(ballPasses), $"no ball in cycle {cycle}");
                        break;
                    case "candy" when cycle % 3 == 0:
                        var vanished = 0;
                        for (var i = 0; i < patches.Count; i++) {
                            var centre = patches.CenterOf(i);
                            if (patches.Sample(new Vector3(centre.x, 0f, centre.y)).Equals(SurfaceFeel.Neutral)) {
                                vanished++;
                            }
                        }

                        Debug.Log($"candy: {vanished} patches vanished by the peak of cycle {cycle}");
                        break;
                    case "playroom" when cycle % 3 == 0:
                        Assert.That(train.Speed > 0f || train.IsDerailed, Is.True, $"the train never set off in cycle {cycle}");
                        Debug.Log($"playroom: the train of cycle {cycle} derailed: {train.IsDerailed}");
                        break;
                }
            }

            /// <summary>Right as the floor starts to come back: the mechanic still shows but touches nobody.</summary>
            public void Harmless(int cycle)
            {
                Assert.That(hazards.All, Is.Empty, $"{map}: a hazard of cycle {cycle} is still in the bots' way");
                Assert.That(lane.IsActive, Is.False, $"{map}: the ball of cycle {cycle} is still in the bots' way");
                Assert.That(carousel.Sample(new Vector3(3f, 0f, 0f)), Is.EqualTo(PlatformMotion.None));
                for (var i = 0; i < patches.Count; i++) {
                    var centre = patches.CenterOf(i);
                    Assert.That(patches.Sample(new Vector3(centre.x, 0f, centre.y)), Is.EqualTo(SurfaceFeel.Neutral),
                        $"{map}: a patch of cycle {cycle} still acts");
                }
            }

            /// <summary>A moment later: nothing of it is left on the floor.</summary>
            public void Gone(int cycle)
            {
                Harmless(cycle);
                Assert.That(top.IsBuilt || pencil.IsBuilt || jelly.Exists || stirrer.Exists || train.Exists || jack.Exists
                    || carousel.Exists, Is.False, $"{map}: the mechanic of cycle {cycle} is still on the floor");
                Assert.That(patches.Count, Is.Zero, $"{map}: the patches of cycle {cycle} are still on the floor");
            }
        }
    }
}
