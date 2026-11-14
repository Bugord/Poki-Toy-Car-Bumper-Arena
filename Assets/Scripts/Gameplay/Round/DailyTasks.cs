using System;
using System.Collections.Generic;
using System.Text;
using Bugord.Core.Storage;
using ToyCarArena.Gameplay.Cars;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The three daily tasks, apart from the missions (<see cref="MissionTracker"/>): every calendar day on the player's
    /// clock (<see cref="CalendarDay"/>) draws a set of its own, one task of each tier from
    /// <see cref="GameTuning.DailySettings.Tasks"/>, the same set for the whole day however many visits it has. The
    /// day's decided rounds count towards them with the missions' counters (<see cref="MissionTracker.Gained"/>); a
    /// task done pays its tier's XP and coins with the round that did it, and stays done until the day is over. Saved
    /// in the kit's storage at once; lives in the app scope beside the missions.
    /// </summary>
    /// <remarks>
    /// Every call takes the time, so the tests drive the days. The day's set is drawn from a seed of the day itself, so
    /// it does not hang on when the player first looks at it; what the player can do (arenas open, boosts and items in
    /// the build) is checked at the draw.
    /// </remarks>
    public sealed class DailyTasks
    {
        public const int Count = 3;

        /// <summary>
        /// The id of the task of the mode of the day, as saved under <c>daily_tasks</c> and measured: constant, whichever
        /// mode the day has.
        /// </summary>
        public const string EventTaskId = "play-event";

        private const string TasksKey = "daily_tasks";

        private readonly IKeyValueStorage storage;
        private readonly GameTuning tuning;
        private readonly IPlayerCar player;
        private readonly DailyTask[] tasks = new DailyTask[Count];
        private int day = int.MinValue;
        private GameTuning.MissionDefinition eventTask;
        private int eventTaskDay = int.MinValue;

        public DailyTasks(IKeyValueStorage storage, GameTuning tuning, IPlayerCar player)
        {
            this.storage = storage;
            this.tuning = tuning;
            this.player = player;
        }

        private GameTuning.DailySettings Settings => tuning.Daily;

        public bool IsEnabled => Settings.HasTasks;

        /// <summary>Today's tasks, drawn on the first look of the day; empty when the build has none.</summary>
        public IReadOnlyList<DailyTask> Today(DateTime now)
        {
            if (!IsEnabled) {
                return Array.Empty<DailyTask>();
            }

            Load(CalendarDay.Of(now));
            return tasks;
        }

        /// <summary>How many of today's tasks are done.</summary>
        public int DoneCount(DateTime now)
        {
            var done = 0;
            foreach (var task in Today(now)) {
                if (task.IsDone) {
                    done++;
                }
            }

            return done;
        }

        /// <summary>The time left until the next set: to the next midnight on the player's clock.</summary>
        public static TimeSpan TimeToNext(DateTime now) => now.Date.AddDays(1) - now;

        public int XpOf(GameTuning.MissionDefinition task) => Settings.TaskXpOf(task.Tier);
        public int CoinsOf(GameTuning.MissionDefinition task) => Settings.TaskCoinsOf(task.Tier);

        /// <summary>
        /// How many of today's tasks still to do a round with <paramref name="stats"/> so far would complete: the HUD
        /// pops DAILY TASK! as this grows. Nothing is saved.
        /// </summary>
        public int CountDone(MissionStats stats, DateTime now)
        {
            var done = 0;
            foreach (var task in Today(now)) {
                if (!task.IsDone && task.Progress + MissionTracker.Gained(task.Definition, stats) >= task.Target) {
                    done++;
                }
            }

            return done;
        }

        /// <summary>
        /// Counts a decided round towards today's tasks and saves: the ones it completes pay their XP and coins, which
        /// the caller adds to the round's reward. A round-slot task asks for it inside one round.
        /// </summary>
        public DailyTaskRound Record(MissionStats stats, DateTime now)
        {
            var completed = new List<GameTuning.MissionDefinition>();
            var xp = 0;
            var coins = 0;
            if (!IsEnabled) {
                return new DailyTaskRound(completed, 0, 0, 0);
            }

            Load(CalendarDay.Of(now));
            for (var index = 0; index < Count; index++) {
                var task = tasks[index];
                if (task.IsDone) {
                    continue;
                }

                var definition = task.Definition;
                var progress = Math.Min(task.Target, task.Progress + MissionTracker.Gained(definition, stats));
                if (progress >= task.Target) {
                    completed.Add(definition);
                    xp += XpOf(definition);
                    coins += CoinsOf(definition);
                }
                else if (definition.IsInOneRound) {
                    progress = 0;
                }

                tasks[index] = new DailyTask(definition, progress);
            }

            Save();
            var done = 0;
            foreach (var task in tasks) {
                if (task.IsDone) {
                    done++;
                }
            }

            return new DailyTaskRound(completed, xp, coins, done);
        }

        /// <summary>Development commands and tests: draws today's set again from nothing, or completes it.</summary>
        public void Reset(DateTime now, bool isDone = false)
        {
            storage.DeleteKey(TasksKey);
            day = int.MinValue;
            Load(CalendarDay.Of(now));
            if (isDone) {
                for (var index = 0; index < Count; index++) {
                    tasks[index] = new DailyTask(tasks[index].Definition, tasks[index].Target);
                }

                Save();
            }
        }

        // Today's set from the storage, or a new one when the saved set is another day's (or none).
        private void Load(int today)
        {
            if (day == today) {
                return;
            }

            day = today;
            if (TryParse(storage.GetString(TasksKey), today)) {
                return;
            }

            Draw(today);
            Save();
        }

        private bool TryParse(string saved, int today)
        {
            if (string.IsNullOrEmpty(saved)) {
                return false;
            }

            var parts = saved.Split('|');
            if (parts.Length != Count + 1 || !int.TryParse(parts[0], out var savedDay) || savedDay != today) {
                return false;
            }

            var parsed = new DailyTask[Count];
            for (var index = 0; index < Count; index++) {
                var part = parts[index + 1];
                var colon = part.LastIndexOf(':');
                if (colon <= 0 || !int.TryParse(part.Substring(colon + 1), out var progress)) {
                    return false;
                }

                var definition = Find(part.Substring(0, colon));
                if (definition == null) {
                    return false;
                }

                parsed[index] = new DailyTask(definition, Math.Clamp(progress, 0, definition.Target));
            }

            Array.Copy(parsed, tasks, Count);
            return true;
        }

        private void Save()
        {
            var text = new StringBuilder();
            text.Append(day);
            foreach (var task in tasks) {
                text.Append('|').Append(task.Definition.Id).Append(':').Append(task.Progress);
            }

            storage.SetString(TasksKey, text.ToString());
        }

        // One task of each tier, easiest first, from a seed of the day: a tier with nothing the player can do lends
        // its place to the others. While the build has an event of the day, the easy one is always to play it.
        private void Draw(int today)
        {
            var random = new Random(unchecked(today * 7919 + 0x5DA1));
            var taken = new List<GameTuning.MissionDefinition>();
            var first = 0;
            var played = EventTaskOf(today);
            if (played != null) {
                taken.Add(played);
                tasks[0] = new DailyTask(played, 0);
                first = 1;
            }

            for (var index = first; index < Count; index++) {
                var tier = (MissionTier)Math.Min(index, (int)MissionTier.Hard);
                var definition = Pick(random, taken, tier) ?? Pick(random, taken, null);
                if (definition == null) {
                    throw new InvalidOperationException("GameTuning.Daily has too few tasks the player can do.");
                }

                taken.Add(definition);
                tasks[index] = new DailyTask(definition, 0);
            }
        }

        private GameTuning.MissionDefinition Pick(Random random, List<GameTuning.MissionDefinition> taken,
            MissionTier? tier)
        {
            var candidates = new List<GameTuning.MissionDefinition>();
            foreach (var definition in Settings.Tasks) {
                if ((tier == null || definition.Tier == tier) && !taken.Contains(definition) && CanDo(definition) &&
                    !IsSameCounter(definition, taken)) {
                    candidates.Add(definition);
                }
            }

            return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
        }

        // Two tasks of one day never count the same thing (KNOCK OUT 5 CARS beside KNOCK OUT 12 CARS).
        private static bool IsSameCounter(GameTuning.MissionDefinition definition,
            List<GameTuning.MissionDefinition> taken)
        {
            foreach (var other in taken) {
                if (other.Counter == definition.Counter) {
                    return true;
                }
            }

            return false;
        }

        // What the player can do in this build at this level: boosts only where cars boost, the item tasks where boxes
        // fall at all, an arena once it is open.
        private bool CanDo(GameTuning.MissionDefinition definition)
        {
            if (definition.Counter == MissionCounter.Boosts && !tuning.Boost.IsEnabled) {
                return false;
            }

            if (definition.NeedsItems) {
                var pickups = tuning.Pickups;
                if (!pickups.Enabled || (!tuning.IsMatch && pickups.FirstRound <= 0)) {
                    return false;
                }
            }

            if (definition.Map.Length == 0) {
                return true;
            }

            foreach (var map in tuning.Maps) {
                if (map.Id == definition.Map) {
                    return map.UnlockLevel <= player.Level;
                }
            }

            return false;
        }

        /// <summary>
        /// The task of the day's event mode: finish one round of it (the menu's event, or the rotation's event slot that
        /// plays it), an easy one; null when the build has no event that day.
        /// </summary>
        private GameTuning.MissionDefinition EventTaskOf(int today)
        {
            var mode = EventSchedule.Of(tuning, today);
            if (mode == EventMode.None) {
                return null;
            }

            var id = EventModes.IdOf(mode);
            if (eventTask == null || eventTaskDay != today || eventTask.Mode != id) {
                eventTask = new GameTuning.MissionDefinition(EventTaskId, MissionSlot.Total, MissionCounter.EventRounds,
                    1, MissionTier.Easy, mode: id);
                eventTaskDay = today;
            }

            return eventTask;
        }

        private GameTuning.MissionDefinition Find(string id)
        {
            if (id == EventTaskId) {
                return EventTaskOf(day);
            }

            foreach (var definition in Settings.Tasks) {
                if (definition.Id == id) {
                    return definition;
                }
            }

            return null;
        }
    }

    /// <summary>A daily task and how far the player got today.</summary>
    public sealed class DailyTask
    {
        public DailyTask(GameTuning.MissionDefinition definition, int progress)
        {
            Definition = definition;
            Progress = progress;
        }

        public GameTuning.MissionDefinition Definition { get; }
        public int Progress { get; }
        public int Target => Definition.Target;
        public bool IsDone => Progress >= Target;
    }

    /// <summary>What a decided round did for the daily tasks: the ones it completed and their reward, already saved.</summary>
    public sealed class DailyTaskRound
    {
        public static readonly DailyTaskRound None = new(Array.Empty<GameTuning.MissionDefinition>(), 0, 0, 0);

        public DailyTaskRound(IReadOnlyList<GameTuning.MissionDefinition> completed, int xp, int coins, int doneToday)
        {
            Completed = completed;
            Xp = xp;
            Coins = coins;
            DoneToday = doneToday;
        }

        public IReadOnlyList<GameTuning.MissionDefinition> Completed { get; }
        public int Xp { get; }
        public int Coins { get; }

        /// <summary>Today's tasks done once the round counted, for the analytics.</summary>
        public int DoneToday { get; }
    }
}
