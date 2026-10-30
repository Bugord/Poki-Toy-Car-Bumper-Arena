using System.Collections.Generic;
using Bugord.Core.Storage;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Protects the arena order: the same first round for everyone, a lap through the arenas the level opened, never the
    /// same twice in a row, and the new-arena flag that outlives the session.
    /// </summary>
    public sealed class ArenaRotationTests
    {
        private GameTuning tuning;
        private MemoryStorage storage;
        private ArenaRotation rotation;

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<GameTuning>();
            JsonUtility.FromJsonOverwrite("{\"maps\":[" +
                "{\"id\":\"sandbox\",\"unlockLevel\":1,\"arena\":{\"radius\":15}}," +
                "{\"id\":\"tabletop\",\"unlockLevel\":2,\"arena\":{\"radius\":12}}," +
                "{\"id\":\"candy\",\"unlockLevel\":4,\"arena\":{\"radius\":15}}," +
                "{\"id\":\"playroom\",\"unlockLevel\":6,\"arena\":{\"radius\":18}}]}", tuning);
            storage = new MemoryStorage();
            rotation = new ArenaRotation(tuning, storage);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void VisitStartsOnTheFirstArena()
        {
            Assert.That(rotation.Current, Is.EqualTo("sandbox"));
        }

        [Test]
        public void OnlyTheFirstArenaOpenStaysOnIt()
        {
            for (var round = 0; round < 5; round++) {
                rotation.Next(1);
                Assert.That(rotation.Current, Is.EqualTo("sandbox"));
            }
        }

        [Test]
        public void LevelOpensArenasInCatalogOrder()
        {
            rotation.Next(2);
            Assert.That(rotation.Current, Is.EqualTo("tabletop"));
            rotation.Next(2);
            Assert.That(rotation.Current, Is.EqualTo("sandbox"));
            rotation.Next(3);
            Assert.That(rotation.Current, Is.EqualTo("tabletop"));
            rotation.Next(4);
            Assert.That(rotation.Current, Is.EqualTo("candy"));
            rotation.Next(4);
            Assert.That(rotation.Current, Is.EqualTo("sandbox"));
        }

        [Test]
        public void AllOpenGoesRoundWithoutRepeating()
        {
            var order = new List<string>();
            for (var round = 0; round < 8; round++) {
                rotation.Next(6);
                order.Add(rotation.Current);
            }

            Assert.That(order, Is.EqualTo(new[] {
                "tabletop", "candy", "playroom", "sandbox", "tabletop", "candy", "playroom", "sandbox",
            }));
        }

        [Test]
        public void NeverTheSameTwiceInARowWhileAnotherIsOpen()
        {
            for (var level = 2; level <= 8; level++) {
                var previous = rotation.Current;
                for (var round = 0; round < 12; round++) {
                    rotation.Next(level);
                    Assert.That(rotation.Current, Is.Not.EqualTo(previous), $"Level {level}, round {round}.");
                    previous = rotation.Current;
                }
            }
        }

        [Test]
        public void ArenaWithoutItsLevelIsNeverPicked()
        {
            for (var round = 0; round < 10; round++) {
                rotation.Next(3);
                Assert.That(rotation.Current, Is.Not.EqualTo("candy"));
                Assert.That(rotation.Current, Is.Not.EqualTo("playroom"));
            }
        }

        [Test]
        public void SeenFlagsAreKeptPerArena()
        {
            Assert.That(rotation.IsNew("candy"), Is.True);
            rotation.MarkSeen("candy");
            Assert.That(rotation.IsNew("candy"), Is.False);
            Assert.That(rotation.IsNew("playroom"), Is.True);
            Assert.That(storage.GetBool("arena_seen_candy"), Is.True);
            Assert.That(new ArenaRotation(tuning, storage).IsNew("candy"), Is.False, "Seen outlives the session.");
        }

        [Test]
        public void OpenedBetweenListsArenasOfTheCrossedLevels()
        {
            Assert.That(ArenaRotation.OpenedBetween(tuning, 1, 1), Is.Empty);
            Assert.That(ArenaRotation.OpenedBetween(tuning, 1, 2), Is.EqualTo(new[] { "tabletop" }));
            Assert.That(ArenaRotation.OpenedBetween(tuning, 1, 6), Is.EqualTo(new[] { "tabletop", "candy", "playroom" }));
            Assert.That(ArenaRotation.OpenedBetween(tuning, 4, 5), Is.Empty);
        }

        [Test]
        public void MapTakesTheNumbersOfTheCurrentArena()
        {
            var catalog = ScriptableObject.CreateInstance<ArenaCatalog>();
            try {
                rotation.Next(2);
                var map = new ArenaMap(tuning, rotation, catalog);
                Assert.That(map.Id, Is.EqualTo("tabletop"));
                Assert.That(map.Settings.Radius, Is.EqualTo(12f));
                Assert.That(map.HasTheme, Is.False, "No catalog entry keeps the scene's look.");
            }
            finally {
                Object.DestroyImmediate(catalog);
            }
        }

        private sealed class MemoryStorage : IKeyValueStorage
        {
            private readonly Dictionary<string, string> values = new();

            public bool HasKey(string key) => values.ContainsKey(key);
            public string GetString(string key, string defaultValue = "") =>
                values.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetString(string key, string value) => values[key] = value;
            public void DeleteKey(string key) => values.Remove(key);
            public void DeleteAll() => values.Clear();
        }
    }
}
