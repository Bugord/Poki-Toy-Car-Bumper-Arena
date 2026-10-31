using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The Pencil of the tabletop's notebook variant on the real Game scene: only that variant has it, its band is shown
    /// before it rolls, a car in the band is knocked along the way it rolls with nobody credited, a car past its end is not
    /// in its way, and the bots are told to leave by the nearer end.
    /// </summary>
    public sealed class PencilTests : ArenaObjectFixture
    {
        [UnityTest]
        public IEnumerator OtherVariantsHaveNoPencil()
        {
            yield return Load("tabletop", "cloth");
            BeginRound();
            var pencil = Container.Resolve<ArenaPencil>();
            yield return Until(() => Round.Elapsed > Tuning.Pencil.Delay + 2f, 20f, "The round never ran.");
            Assert.That(pencil.IsActive, Is.False);
        }

        [UnityTest]
        public IEnumerator ThePencilShowsItsBandThenKnocksACarAlongItsWay()
        {
            yield return Load("tabletop", "notebook");
            var pencil = Container.Resolve<ArenaPencil>();
            BeginRound();
            Assert.That(Hazards.All.Count, Is.EqualTo(1));

            yield return Until(() => pencil.IsActive, 20f, "No pencil was announced.");
            var announcedAt = Time.time;
            Assert.That(Round.Elapsed, Is.GreaterThanOrEqualTo(Tuning.Pencil.Delay - 0.1f));
            Assert.That(pencil.IsRolling, Is.False, "It is announced before it rolls.");
            var floorWidth = 0f;
            Edge.SolidChord(Vector3.zero, pencil.Axis, out var behind, out var ahead);
            floorWidth = behind + ahead;
            Assert.That(pencil.HalfLength * 2f, Is.EqualTo(floorWidth * Tuning.Pencil.LengthShare).Within(floorWidth * 0.05f),
                "The pencil is a share of the line it lies along.");

            // The car waits mid-way along the band, on the pencil's middle line.
            var car = Round.Cars[1];
            ParkOthers(car);
            var middle = pencil.Axis * pencil.CenterAxis + pencil.Direction * ((pencil.TravelStart + pencil.TravelEnd) * 0.5f);
            Stand(car, middle);

            Assert.That(pencil.TryAvoid(middle, Tuning.Bots.HazardMargin, Edge, 1f, out var heading), Is.True,
                "A car in the band should be told to leave it.");
            Assert.That(Mathf.Abs(Vector3.Dot(heading, pencil.Axis)), Is.GreaterThan(0.9f), "It leaves by an end of the pencil.");
            var pastEnd = middle + pencil.Axis * (pencil.HalfLength + Tuning.Bots.HazardMargin + 1.5f);
            Assert.That(pencil.TryAvoid(pastEnd, Tuning.Bots.HazardMargin, Edge, 1f, out _), Is.False,
                "A car beyond the pencil's end is out of its way.");

            yield return Until(() => pencil.IsRolling, 5f, "The pencil never rolled.");
            Assert.That(Time.time - announcedAt, Is.GreaterThanOrEqualTo(Tuning.Pencil.Warning - 0.15f),
                "It must be announced for its whole warning.");

            Stand(car, middle);
            var log = "";
            var next = 0f;
            while (float.IsNegativeInfinity(car.LastHazardTime) && pencil.IsActive) {
                if (Time.time > next) {
                    next = Time.time + 0.5f;
                    log += $" [car={car.Position} center={pencil.Center} v={car.Movement.Velocity}]";
                }

                yield return null;
            }

            Assert.That(float.IsNegativeInfinity(car.LastHazardTime), Is.False, "The pencil never touched the car: " + log);
            Assert.That(car.LastHitBy, Is.Null, "A pencil knock is nobody's hit.");
            var along = Vector3.Dot(Flat(car.Movement.Velocity), pencil.Direction);
            Assert.That(along, Is.GreaterThan(3f), "The car should be sent the way the pencil rolls.");

            yield return Until(() => !pencil.IsActive, 15f, "The pencil never finished its pass.");
        }
    }
}
