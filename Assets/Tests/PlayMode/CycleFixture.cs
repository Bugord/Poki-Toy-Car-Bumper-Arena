using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.Gameplay;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// What the tests of the floor's cycles share: the shared tuning asset changed in memory for one test (short cycles,
    /// a mechanic's delay), never saved, and put back by the tear-down whatever the test did.
    /// </summary>
    public abstract class CycleFixture : ArenaObjectFixture
    {
        private readonly List<(object target, FieldInfo field, object value)> saved = new();

        [TearDown]
        public void RestoreTuningFields()
        {
            // Backwards, so a field set twice ends on its first value.
            for (var i = saved.Count - 1; i >= 0; i--) {
                var (target, field, value) = saved[i];
                field.SetValue(target, value);
            }

            saved.Clear();
        }

        /// <summary>Sets a private serialized field of a tuning section for this test only.</summary>
        protected void SetField(object target, string name, object value)
        {
            var field = FindField(target.GetType(), name);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            saved.Add((target, field, field.GetValue(target)));
            field.SetValue(target, value);
        }

        /// <summary>Switches the cycles on with short phases, for this test only.</summary>
        protected GameTuning.FloorCycleSettings EnableCycles(int cycles, float open, float shrink, float peak,
            float returnTime, float returnWarning)
        {
            var settings = LoadTuning().FloorCycles;
            SetField(settings, "enabled", true);
            SetField(settings, "cycles", cycles);
            SetField(settings, "open", open);
            SetField(settings, "shrink", shrink);
            SetField(settings, "peak", peak);
            SetField(settings, "returnTime", returnTime);
            SetField(settings, "returnWarning", returnWarning);
            return settings;
        }

        private static FieldInfo FindField(System.Type type, string name)
        {
            for (; type != null; type = type.BaseType) {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null) {
                    return field;
                }
            }

            return null;
        }
    }
}
