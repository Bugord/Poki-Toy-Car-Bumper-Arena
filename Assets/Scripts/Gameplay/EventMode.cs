namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The mode the player picked in the menu's CHOOSE MODE: none for QUICK MATCH (last car standing, with the
    /// rotation's special rounds), or an event mode that changes the whole round. Kept for the visit only
    /// (<see cref="Round.ISessionProgress.Mode"/>): a new visit opens on QUICK MATCH, and the first visit, which has no
    /// menu, plays only that.
    /// </summary>
    public enum EventMode
    {
        /// <summary>QUICK MATCH: the plain round.</summary>
        None,

        /// <summary>
        /// HOT POTATO: a fenced floor nobody can fall from, a bomb that passes on a bump and knocks its holder out of the
        /// round when its fuse runs out; the last car left wins.
        /// </summary>
        HotPotato,

        /// <summary>CROWN: hold the crown the longest; a bump steals it.</summary>
        Crown,

        /// <summary>GOLD RUSH: grab coins; a bump shakes a rival's coins out.</summary>
        GoldRush,

        /// <summary>ITEM FRENZY: boxes everywhere; the last car standing wins.</summary>
        ItemFrenzy,

        /// <summary>OVERHEAT: every hit heats a car up, and the hotter it is the farther it flies.</summary>
        Overheat,

        /// <summary>TEAM PUSH: blue against red; push the other team off.</summary>
        TeamPush,

        /// <summary>FOOTBALL: push the ball into the other team's goal.</summary>
        Football,
    }

    /// <summary>
    /// The event modes as a whole: the one place that turns a mode of the menu into the kind of round it plays and back
    /// (one to one, <see cref="SpecialRound"/> holds every mode at its end), and the fixed circle the mode of the day
    /// goes round (<see cref="Round.EventSchedule"/>).
    /// </summary>
    public static class EventModes
    {
        /// <summary>Every event mode, in the order the mode of the day goes round them.</summary>
        public static readonly EventMode[] Circle = {
            EventMode.HotPotato, EventMode.Crown, EventMode.GoldRush, EventMode.ItemFrenzy, EventMode.Overheat,
            EventMode.TeamPush, EventMode.Football,
        };

        /// <summary>The kind of round <paramref name="mode"/> plays; <see cref="SpecialRound.None"/> for QUICK MATCH.</summary>
        public static SpecialRound KindOf(EventMode mode) => mode switch {
            EventMode.HotPotato => SpecialRound.HotPotato,
            EventMode.Crown => SpecialRound.Crown,
            EventMode.GoldRush => SpecialRound.GoldRush,
            EventMode.ItemFrenzy => SpecialRound.ItemFrenzy,
            EventMode.Overheat => SpecialRound.Overheat,
            EventMode.TeamPush => SpecialRound.TeamPush,
            EventMode.Football => SpecialRound.Football,
            _ => SpecialRound.None,
        };

        /// <summary>The event mode a kind of round belongs to; <see cref="EventMode.None"/> for a round of no mode.</summary>
        public static EventMode ModeOf(SpecialRound kind)
        {
            foreach (var mode in Circle) {
                if (KindOf(mode) == kind) {
                    return mode;
                }
            }

            return EventMode.None;
        }

        /// <summary>Whether <paramref name="kind"/> is an event mode's round rather than a plain or special one.</summary>
        public static bool IsEvent(SpecialRound kind) => ModeOf(kind) != EventMode.None;

        /// <summary>The mode's id for analytics, the strings and its art: no numbers, never changed.</summary>
        public static string IdOf(EventMode mode) => Round.RoundRules.IdOf(KindOf(mode));

        /// <summary>The event mode of an id (<see cref="IdOf"/>), <see cref="EventMode.None"/> for an unknown one.</summary>
        public static EventMode Parse(string id)
        {
            foreach (var mode in Circle) {
                if (IdOf(mode) == id) {
                    return mode;
                }
            }

            return EventMode.None;
        }
    }
}
