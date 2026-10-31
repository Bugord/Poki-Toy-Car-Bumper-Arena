using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The round floor's edge answers every question exactly as the code that used to read a radius did: the same float
    /// operations in the same order, so a seeded round plays the same. Each check below is the old expression written out.
    /// </summary>
    public sealed class CircleEdgeTests
    {
        private static readonly float[] Radii = { 15f, 13.5f, 12f, 9.75f, 5f, 18f };

        [Test]
        public void MatchesTheRadiusFormulasBitForBit()
        {
            var random = new System.Random(5);
            foreach (var radius in Radii) {
                var edge = new CircleEdge(radius);
                for (var i = 0; i < 4000; i++) {
                    var position = new Vector3((float)(random.NextDouble() * 40 - 20), (float)random.NextDouble(),
                        (float)(random.NextDouble() * 40 - 20));
                    if (i % 50 == 0) {
                        position = new Vector3(0.001f * i, 0.3f, 0f);
                    }

                    var flat = new Vector3(position.x, 0f, position.z);
                    var fromMiddle = flat.magnitude;

                    Assert.That(edge.DistanceToEdge(position), Is.EqualTo(radius - fromMiddle));
                    Assert.That(edge.DistanceToEdge(position, 1.5f),
                        Is.EqualTo(Mathf.Max(0f, radius - 1.5f) - new Vector2(position.x, position.z).magnitude));
                    Assert.That(Mathf.Max(0f, edge.DistanceToEdge(position, 1.5f)),
                        Is.EqualTo(Mathf.Max(0f, radius - 1.5f - fromMiddle)), "edge hold clearance");
                    Assert.That(edge.DistanceToSolidEdge(position), Is.EqualTo(radius - fromMiddle));
                    Assert.That(edge.Proximity(position, 5f, 2f),
                        Is.EqualTo(Mathf.InverseLerp(radius - 5f, radius - 2f, fromMiddle)), "edge assist share");
                    Assert.That(edge.Nearness(position), Is.EqualTo(Mathf.Clamp01(fromMiddle / Mathf.Max(1f, radius))));
                    Assert.That(edge.Span, Is.EqualTo(Mathf.Max(1f, radius * 2f)));
                    Assert.That(edge.ClampToFloor(flat), Is.EqualTo(Vector3.ClampMagnitude(flat, radius)));

                    // The old assist and hold took the unit vector away from the middle, the bots the reverse of the position.
                    var inward = edge.Inward(position);
                    if (fromMiddle < 0.01f) {
                        Assert.That(inward, Is.EqualTo(Vector3.zero));
                        continue;
                    }

                    Assert.That(inward, Is.EqualTo(-flat), "bots steer back with the reverse of the position");
                    Assert.That((-inward).normalized, Is.EqualTo(flat / fromMiddle), "assist and hold outward");
                    Assert.That(inward.normalized, Is.EqualTo(-flat.normalized), "novice heading");

                    var velocity = new Vector3((float)(random.NextDouble() * 20 - 10), 0.2f,
                        (float)(random.NextDouble() * 20 - 10));
                    Assert.That(Vector3.Dot(velocity, inward) < 0f, Is.EqualTo(position.x * velocity.x + position.z * velocity.z > 0f),
                        "a car sliding off");
                    Assert.That(Vector3.Dot(velocity, -inward) > 0f, Is.EqualTo(Vector3.Dot(velocity, flat) > 0f));

                    // The bots' safe-radius test, as a distance: the same answer except exactly on the line.
                    var safe = Mathf.Max(0f, radius - 3f);
                    var oldOutside = flat.sqrMagnitude > safe * safe;
                    var newOutside = edge.DistanceToEdge(position, 3f) < 0f;
                    if (Mathf.Abs(fromMiddle - safe) > 1e-4f) {
                        Assert.That(newOutside, Is.EqualTo(oldOutside));
                    }
                }
            }
        }

        [Test]
        public void SafeAndSolidFloorFollowTheirRadii()
        {
            var edge = new CircleEdge(15f);
            var changes = 0;
            edge.Changed += () => changes++;

            edge.Set(15f, 15f);
            Assert.That(changes, Is.EqualTo(0), "nothing moved");

            edge.Set(13.5f, 15f);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(edge.SafeExtent, Is.EqualTo(13.5f));
            Assert.That(edge.SolidExtent, Is.EqualTo(15f));
            Assert.That(edge.FullExtent, Is.EqualTo(15f));

            var onShakingRing = new Vector3(14.2f, 0.4f, 0f);
            Assert.That(edge.DistanceToEdge(onShakingRing), Is.LessThan(0f), "a shaking ring is no longer safe");
            Assert.That(edge.IsSolid(onShakingRing), Is.True, "but it still holds a car up");
            Assert.That(edge.IsSolid(new Vector3(15.5f, 0f, 0f)), Is.False);
            Assert.That(edge.SolidHalfChord(Vector3.zero, Vector3.right), Is.EqualTo(15f));
            Assert.That(edge.SolidHalfChord(new Vector3(0f, 0f, 9f), Vector3.right), Is.EqualTo(12f).Within(1e-4f));
        }
    }
}
