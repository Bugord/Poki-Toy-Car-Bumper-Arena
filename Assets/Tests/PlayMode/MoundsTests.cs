using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The Mounds of the sandbox's bricks variant on the real Game scene: only that variant has them, each is announced
    /// before it pops, tosses a car on it with nobody credited, slows what drives over it for a while and then sinks; and the
    /// composite surface multiplies its layers.
    /// </summary>
    public sealed class MoundsTests : ArenaObjectFixture
    {
        private sealed class FakeLayer : ISurfaceLayer
        {
            private readonly SurfaceFeel feel;

            public FakeLayer(SurfaceFeel feel) => this.feel = feel;

            public SurfaceFeel Sample(Vector3 position) => feel;
        }

        [Test]
        public void TheCompositeSurfaceMultipliesItsLayers()
        {
            var surface = new ArenaSurface();
            Assert.That(surface.Sample(Vector3.zero).Speed, Is.EqualTo(1f));
            surface.Add(new FakeLayer(new SurfaceFeel(0.5f, 0.8f, 2f, 1f, 1f)));
            surface.Add(new FakeLayer(new SurfaceFeel(0.6f, 1f, 1.5f, 0.5f, 1f)));
            var feel = surface.Sample(Vector3.zero);
            Assert.That(feel.Speed, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(feel.Acceleration, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(feel.Grip, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(feel.Push, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator OtherVariantsHaveNoMounds()
        {
            yield return Load("sandbox", "rake");
            Round.Begin();
            var mounds = Container.Resolve<ArenaMounds>();
            yield return Until(() => Round.Elapsed > Tuning.Mounds.Delay + Tuning.Mounds.Warning + 2f, 30f, "The round never ran.");
            Assert.That(mounds.ActiveCount, Is.EqualTo(0));
            Assert.That(Container.Resolve<IArenaSurface>().Sample(Vector3.zero).Speed, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator AMoundIsAnnouncedThenTossesACarAndSlowsTheSandAndSinks()
        {
            yield return Load("sandbox", "bricks");
            var mounds = Container.Resolve<ArenaMounds>();
            var surface = Container.Resolve<IArenaSurface>();
            var settings = Tuning.Mounds;
            Round.Begin();
            Assert.That(Hazards.All.Count, Is.EqualTo(1));

            yield return Until(() => mounds.ActiveCount > 0, 20f, "No mound was announced.");
            Assert.That(Round.Elapsed, Is.GreaterThanOrEqualTo(settings.Delay - 0.1f));
            var announcedAt = Time.time;
            var index = -1;
            for (var i = 0; i < 4; i++) {
                if (mounds.TryGetMound(i, out _, out _)) {
                    index = i;
                    break;
                }
            }

            mounds.TryGetMound(index, out var spot, out var isRisen);
            Assert.That(isRisen, Is.False, "It is announced before it rises.");
            Assert.That(surface.Sample(spot).Speed, Is.EqualTo(1f), "Announced sand is not slow yet.");

            // A bot standing on the circle is told to leave it; a car far away is not.
            Assert.That(mounds.TryAvoid(spot, Tuning.Bots.HazardMargin, Edge, 1f, out var heading), Is.True);
            Assert.That(heading.magnitude, Is.GreaterThan(0.9f));
            Assert.That(mounds.TryAvoid(spot + Vector3.right * 12f, Tuning.Bots.HazardMargin, Edge, 1f, out _), Is.False);

            // A car stays on the spot through the pop.
            var car = Round.Cars[1];
            ParkOthers(car);
            Stand(car, spot);
            var firstVersion = mounds.Version;
            Assert.That(firstVersion, Is.GreaterThan(0));
            yield return Until(() => {
                mounds.TryGetMound(index, out _, out var risen);
                return risen;
            }, 5f, "The mound never rose.");
            Assert.That(Time.time - announcedAt, Is.GreaterThanOrEqualTo(settings.Warning - 0.15f),
                "The mound rose before its warning was over.");
            Assert.That(float.IsNegativeInfinity(car.LastHazardTime), Is.False, "The pop should have tossed the car.");
            Assert.That(car.LastHitBy, Is.Null, "A mound toss is nobody's hit.");

            var risenAt = Time.time + 0.4f;
            yield return Until(() => Time.time > risenAt, 2f, "Time stood still.");
            Assert.That(Flat(car.Position - spot).magnitude, Is.GreaterThan(0.5f), "The car should leave the middle.");
            Assert.That(surface.Sample(spot).Speed, Is.EqualTo(settings.Speed).Within(0.02f), "Risen sand is slow.");
            Assert.That(surface.Sample(spot + Vector3.right * (settings.Radius + 2f)).Speed, Is.EqualTo(1f));

            yield return Until(() => surface.Sample(spot).Speed > 0.99f, settings.Stay + 3f, "The mound never sank.");

            Assert.That(surface.Sample(spot).Speed, Is.GreaterThan(0.99f), "A sunk mound leaves plain floor.");
        }
    }
}
