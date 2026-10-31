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
                "{\"id\":\"sandbox\",\"unlockLevel\":1,\"arena\":{\"radius\":15},\"variants\":[" +
                "{\"id\":\"rake\",\"mechanic\":0},{\"id\":\"molds\",\"mechanic\":4},{\"id\":\"bricks\",\"mechanic\":5}]}," +
                "{\"id\":\"tabletop\",\"unlockLevel\":2,\"arena\":{\"radius\":12},\"variants\":[" +
                "{\"id\":\"board\",\"mechanic\":3},{\"id\":\"cloth\",\"mechanic\":6},{\"id\":\"notebook\",\"mechanic\":7}]}," +
                "{\"id\":\"candy\",\"unlockLevel\":4,\"arena\":{\"radius\":15},\"variants\":[" +
                "{\"id\":\"frosting\",\"mechanic\":1},{\"id\":\"chocolate\",\"mechanic\":8},{\"id\":\"waffle\",\"mechanic\":9}]}," +
                "{\"id\":\"playroom\",\"unlockLevel\":6,\"arena\":{\"radius\":18},\"variants\":[" +
                "{\"id\":\"puzzle\",\"mechanic\":2},{\"id\":\"road\",\"mechanic\":10},{\"id\":\"blocks\",\"mechanic\":11}]}]}", tuning);
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
        public void VisitStartsOnTheFirstVariantOfTheFirstArena()
        {
            Assert.That(rotation.CurrentVariant, Is.EqualTo("rake"));
        }

        [Test]
        public void VisitStartIsRakeEvenWhenAnotherSandboxVariantWasPlayedLast()
        {
            rotation.MarkPlayed("sandbox", "molds");
            Assert.That(new ArenaRotation(tuning, storage).CurrentVariant, Is.EqualTo("rake"));
        }

        [Test]
        public void FirstTimeOnAnArenaIsItsFirstVariant()
        {
            rotation.Next(2);
            Assert.That(rotation.Current, Is.EqualTo("tabletop"));
            Assert.That(rotation.CurrentVariant, Is.EqualTo("board"));
        }

        [Test]
        public void NeverTheSameVariantTwiceInARowOnAnArena()
        {
            // Only the first arena is open, so every round comes back to it.
            var seen = new HashSet<string>();
            var previous = rotation.CurrentVariant;
            for (var round = 0; round < 60; round++) {
                rotation.MarkPlayed(rotation.Current, rotation.CurrentVariant);
                rotation.Next(1);
                Assert.That(rotation.Current, Is.EqualTo("sandbox"));
                Assert.That(rotation.CurrentVariant, Is.Not.EqualTo(previous), $"Round {round}.");
                seen.Add(rotation.CurrentVariant);
                previous = rotation.CurrentVariant;
            }

            Assert.That(seen, Is.EquivalentTo(new[] { "rake", "molds", "bricks" }), "Chance reaches every variant.");
        }

        [Test]
        public void VariantOfAnArenaFollowsWhatWasPlayedThereNotElsewhere()
        {
            rotation.MarkPlayed("candy", "waffle");
            rotation.MarkPlayed("tabletop", "board");
            for (var round = 0; round < 30; round++) {
                // The arenas go round in order, so candy comes up whenever the walk reaches it.
                do {
                    rotation.Next(6);
                }
                while (rotation.Current != "candy");

                Assert.That(rotation.CurrentVariant, Is.Not.EqualTo("waffle"));
                Assert.That(new[] { "frosting", "chocolate" }, Contains.Item(rotation.CurrentVariant));
                // Pretend the round did not begin: the saved variant stays, and so does the rule.
            }
        }

        [Test]
        public void PlayedVariantIsSavedPerArenaAndOutlivesTheSession()
        {
            rotation.MarkPlayed("candy", "waffle");
            Assert.That(storage.GetString("arena_variant_candy"), Is.EqualTo("waffle"));
            Assert.That(storage.HasKey("arena_variant_tabletop"), Is.False);
            var next = new ArenaRotation(tuning, storage);
            do {
                next.Next(6);
            }
            while (next.Current != "candy");

            Assert.That(next.CurrentVariant, Is.Not.EqualTo("waffle"));
        }

        [Test]
        public void UnknownSavedVariantCountsAsTheFirstTime()
        {
            storage.SetString("arena_variant_tabletop", "gone");
            rotation.Next(2);
            Assert.That(rotation.CurrentVariant, Is.EqualTo("board"));
        }

        [Test]
        public void FixedRotationKeepsTheVariantItWasGiven()
        {
            var fixedRotation = new FixedArenaRotation("tabletop", "cloth");
            Assert.That(fixedRotation.CurrentVariant, Is.EqualTo("cloth"));
            fixedRotation.Next(9);
            Assert.That(fixedRotation.Current, Is.EqualTo("tabletop"));
            Assert.That(fixedRotation.CurrentVariant, Is.EqualTo("cloth"));
            Assert.That(new FixedArenaRotation("tabletop").CurrentVariant, Is.Null);
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
                Assert.That(map.Variant, Is.EqualTo("board"));
                Assert.That(map.Mechanic, Is.EqualTo(GameTuning.ArenaMechanic.Ball));
            }
            finally {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void MapTakesTheMechanicOfTheVariantAndRejectsAnUnknownOne()
        {
            var catalog = ScriptableObject.CreateInstance<ArenaCatalog>();
            try {
                var map = new ArenaMap(tuning, new FixedArenaRotation("tabletop", "cloth"), catalog);
                Assert.That(map.Variant, Is.EqualTo("cloth"));
                Assert.That(map.Mechanic, Is.EqualTo(GameTuning.ArenaMechanic.Top));
                map = new ArenaMap(tuning, new FixedArenaRotation("tabletop"), catalog);
                Assert.That(map.Variant, Is.EqualTo("board"), "No variant named is the arena's first.");
                Assert.Throws<System.ArgumentException>(() => new ArenaMap(tuning, new FixedArenaRotation("tabletop", "nope"), catalog));
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
