#if BUGORD_SUPERCONSOLE
using System;
using System.Collections.Generic;
using Bugord.SuperConsole.Commands;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using VContainer.Unity;

namespace ToyCarArena.App
{
    /// <summary>
    /// Lets development builds reach an unlock in one round; release players cannot edit their reward this way.
    /// </summary>
    public sealed class ProgressDevConsole : IStartable, IDisposable
    {
        private readonly PlayerProgress progress;
        private readonly AdBreaks adBreaks;
        private readonly PlayerDaily daily;
        private readonly DailyTasks dailyTasks;

        public ProgressDevConsole(PlayerProgress progress, AdBreaks adBreaks, PlayerDaily daily, DailyTasks dailyTasks)
        {
            this.progress = progress;
            this.adBreaks = adBreaks;
            this.daily = daily;
            this.dailyTasks = dailyTasks;
        }

        public void Start()
        {
            CommandRegistry.Register("xp", "Set total saved XP (next result animates from this value)",
                (Func<int, string>)(total => {
                    progress.SetXp(total);
                    return Describe();
                }));
            CommandRegistry.Register("xp_add", "Add to total saved XP (negative values subtract)",
                (Func<int, string>)(amount => {
                    progress.AddXp(amount);
                    return Describe();
                }));
            CommandRegistry.Register("ad_play", "Set the seconds of rounds played; past Ads.FreePlaySeconds NEXT MATCH "
                + "shows a commercial break", (Func<float, string>)(seconds => {
                    adBreaks.SetPlayed(seconds);
                    return $"{adBreaks.Played:0} s of rounds";
                }));
            CommandRegistry.Register("xp_boost", "Set the matches the XP boost runs for (0 ends it); the menu shows it "
                + "when it opens next", (Func<int, string>)(matches => {
                    progress.SetBoost(matches);
                    return $"XP boost: {progress.BoostMatchesLeft} matches";
                }));
            CommandRegistry.Register("daily", "Set the daily gift's last claim N days ago with S days in a row (1 makes "
                + "the next day due, more breaks the streak, a negative N forgets every claim); the menu shows it when "
                + "it opens next", (Func<int, int, string>)((daysAgo, streak) => {
                    var now = DateTime.Now;
                    daily.SetLastClaim(now, daysAgo, streak);
                    return daily.CanClaim(now)
                        ? $"Daily gift due: day {daily.Streak(now)} in a row"
                        : $"Daily gift claimed: day {daily.Streak(now)} in a row";
                }));
            CommandRegistry.Register("daily_tasks", "Draw today's daily tasks again (0) or complete them all (1)",
                (Func<int, string>)(isDone => {
                    var now = DateTime.Now;
                    dailyTasks.Reset(now, isDone != 0);
                    return $"Daily tasks: {dailyTasks.DoneCount(now)}/{DailyTasks.Count} done";
                }));
            CommandRegistry.Register("first_win", "Bring today's first win back (0) or take it (1)",
                (Func<int, string>)(isTaken => {
                    var now = DateTime.Now;
                    daily.SetFirstWin(now, isTaken != 0);
                    return daily.HasFirstWin(now) ? "First win of the day: still to come" : "First win of the day: taken";
                }));
            CommandRegistry.Register("event_day", "Make every day's event mode this one (a stub plays though its build "
                + "has it off), or auto for the schedule; an open menu shows it within a second, and its event tile "
                + "plays it",
                (Func<string, string>)(id => {
                    if (id == ScheduleId) {
                        EventSchedule.Forced = null;
                        return "Event of the day: the schedule";
                    }

                    var mode = EventModes.Parse(id);
                    if (mode == EventMode.None) {
                        return $"No event mode {id}";
                    }

                    EventSchedule.Forced = mode;
                    return $"Event of the day: {EventModes.IdOf(mode)}";
                }), ParamSpec.Of("id", "An event mode's id, or auto").From(() => EventDayIds()));
        }

        /// <summary>
        /// What <c>event_day</c> takes, as the console hints it: every mode of the circle by its id, then
        /// <see cref="ScheduleId"/>. Read from <see cref="EventModes.Circle"/>, so a new mode is offered by itself.
        /// </summary>
        public static IReadOnlyList<string> EventDayIds()
        {
            var ids = new List<string>(EventModes.Circle.Length + 1);
            foreach (var mode in EventModes.Circle) {
                ids.Add(EventModes.IdOf(mode));
            }

            ids.Add(ScheduleId);
            return ids;
        }

        /// <summary>The <c>event_day</c> value that gives the mode of the day back to the schedule.</summary>
        public const string ScheduleId = "auto";

        public void Dispose()
        {
            CommandRegistry.Unregister("xp");
            CommandRegistry.Unregister("xp_add");
            CommandRegistry.Unregister("ad_play");
            CommandRegistry.Unregister("xp_boost");
            CommandRegistry.Unregister("daily");
            CommandRegistry.Unregister("daily_tasks");
            CommandRegistry.Unregister("first_win");
            CommandRegistry.Unregister("event_day");
        }

        private string Describe() => $"{progress.TotalXp} XP, LVL {progress.Level}";
    }
}
#endif
