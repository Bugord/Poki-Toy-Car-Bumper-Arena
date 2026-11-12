using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The mode of the day (<see cref="EventSchedule"/>): one mode a calendar day round the fixed circle, only the modes
    /// the build has on, a new one at midnight, and the rotation's event slot playing it. The modes' <c>enabled</c> are
    /// switched in memory and given back after each test; the asset is never saved.
    /// </summary>
    public sealed class EventScheduleTests : ObjectsBFixture
    {
        private readonly Dictionary<object, bool> saved = new();

        [TearDown]
        public void GiveBack()
        {
            foreach (var (settings, enabled) in saved) {
                EnabledOf(settings).SetValue(settings, enabled);
            }

            saved.Clear();
            EventSchedule.Forced = null;
        }

        [Test]
        public void TheDaysGoRoundTheCircle()
        {
            var tuning = LoadTuning();
            foreach (var mode in EventModes.Circle) {
                Enable(tuning, mode, true);
            }

            var start = 738000;
            var first = EventSchedule.Of(tuning, start);
            var at = Array.IndexOf(EventModes.Circle, first);
            Assert.That(at, Is.GreaterThanOrEqualTo(0));
            for (var day = 0; day < EventModes.Circle.Length * 2; day++) {
                var expected = EventModes.Circle[(at + day) % EventModes.Circle.Length];
                Assert.That(EventSchedule.Of(tuning, start + day), Is.EqualTo(expected), $"Day {day}.");
            }

            // A clock far back still lands on the circle.
            Assert.That(EventModes.Circle, Does.Contain(EventSchedule.Of(tuning, -5)));
        }

        [Test]
        public void OnlyTheModesTheBuildHasOnComeRound()
        {
            var tuning = LoadTuning();
            foreach (var mode in EventModes.Circle) {
                Enable(tuning, mode, mode == EventMode.HotPotato || mode == EventMode.Overheat);
            }

            var seen = new HashSet<EventMode>();
            for (var day = 0; day < 20; day++) {
                var today = EventSchedule.Of(tuning, 738000 + day);
                Assert.That(today == EventMode.HotPotato || today == EventMode.Overheat, Is.True, today.ToString());
                Assert.That(EventSchedule.Of(tuning, 738001 + day), Is.Not.EqualTo(today), "The same mode two days running.");
                seen.Add(today);
            }

            Assert.That(seen.Count, Is.EqualTo(2));

            // The build as it ships: HOT POTATO every day.
            foreach (var mode in EventModes.Circle) {
                Enable(tuning, mode, mode == EventMode.HotPotato);
            }

            Assert.That(EventSchedule.Of(tuning, 738000), Is.EqualTo(EventMode.HotPotato));
            Assert.That(EventSchedule.Of(tuning, 738001), Is.EqualTo(EventMode.HotPotato));

            // Every mode off: no event at all.
            Enable(tuning, EventMode.HotPotato, false);
            Assert.That(EventSchedule.Of(tuning, 738000), Is.EqualTo(EventMode.None));
        }

        [Test]
        public void TheModeChangesAtMidnight()
        {
            var tuning = LoadTuning();
            foreach (var mode in EventModes.Circle) {
                Enable(tuning, mode, true);
            }

            var evening = new DateTime(2026, 11, 12, 23, 59, 30);
            var night = new DateTime(2026, 11, 13, 0, 0, 10);
            Assert.That(EventSchedule.Of(tuning, CalendarDay.Of(night)),
                Is.Not.EqualTo(EventSchedule.Of(tuning, CalendarDay.Of(evening))));
            Assert.That(EventSchedule.Of(tuning, CalendarDay.Of(evening.Date.AddHours(9))),
                Is.EqualTo(EventSchedule.Of(tuning, CalendarDay.Of(evening))), "The mode changed within the day.");
            Assert.That(EventSchedule.LeftToday(evening), Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(EventSchedule.LeftToday(night), Is.EqualTo(TimeSpan.FromHours(24) - TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void ForcedPlaysAModeEvenOffButNotInAMatch()
        {
            var tuning = LoadTuning();
            Enable(tuning, EventMode.Football, false);
            EventSchedule.Forced = EventMode.Football;
            Assert.That(EventSchedule.Of(tuning, 738000), Is.EqualTo(EventMode.Football));
            Assert.That(EventSchedule.Today(tuning), Is.EqualTo(EventMode.Football));

            // The fixture's tear-down gives the build's mode back.
            UseMode(GameTuning.GameMode.Match);
            Assert.That(EventSchedule.Of(tuning, 738000), Is.EqualTo(EventMode.None));
        }

        [Test]
        public void TheRotationsEventSlotPlaysTheModeOfTheDay()
        {
            var tuning = LoadTuning();
            var specials = tuning.SpecialRounds;
            // The fixture forces plain rounds for every test; this one reads the build's rotation.
            specials.Force(null);
            var slot = -1;
            for (var round = 1; round < 40 && slot < 0; round++) {
                if (specials.For(round) == SpecialRound.HotPotato) {
                    slot = round;
                }
            }

            Assert.That(slot, Is.GreaterThan(0), "The rotation has no event slot.");
            Assert.That(RoundRules.For(tuning, slot, EventMode.None),
                Is.EqualTo(EventModes.KindOf(EventSchedule.Today(tuning))), "The build as it ships plays the mode of the day there.");

            EventSchedule.Forced = EventMode.Crown;
            Assert.That(RoundRules.For(tuning, slot, EventMode.None), Is.EqualTo(SpecialRound.Crown));
            // The other special rounds and the menu's own pick are not the schedule's.
            Assert.That(RoundRules.For(tuning, specials.FirstRound, EventMode.None), Is.EqualTo(SpecialRound.LowGravity));
            Assert.That(RoundRules.For(tuning, slot, EventMode.HotPotato), Is.EqualTo(SpecialRound.HotPotato));

            // Every mode off: the slot is a plain round.
            EventSchedule.Forced = null;
            foreach (var mode in EventModes.Circle) {
                Enable(tuning, mode, false);
            }

            Assert.That(RoundRules.For(tuning, slot, EventMode.None), Is.EqualTo(SpecialRound.None));
        }

        private void Enable(GameTuning tuning, EventMode mode, bool enabled)
        {
            var settings = SettingsOf(tuning, mode);
            if (!saved.ContainsKey(settings)) {
                saved[settings] = (bool)EnabledOf(settings).GetValue(settings);
            }

            EnabledOf(settings).SetValue(settings, enabled);
        }

        private static object SettingsOf(GameTuning tuning, EventMode mode) => mode switch {
            EventMode.HotPotato => tuning.SpecialRounds.HotPotato,
            EventMode.Crown => tuning.Crown,
            EventMode.GoldRush => tuning.GoldRush,
            EventMode.ItemFrenzy => tuning.ItemFrenzy,
            EventMode.Overheat => tuning.Overheat,
            EventMode.TeamPush => tuning.TeamPush,
            EventMode.Football => tuning.Football,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        private static FieldInfo EnabledOf(object settings) =>
            settings.GetType().GetField("enabled", BindingFlags.NonPublic | BindingFlags.Instance);
    }
}
