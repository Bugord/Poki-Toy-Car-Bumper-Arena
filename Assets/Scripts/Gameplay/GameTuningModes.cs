namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The event modes' table, the one shared place that names every mode's settings: filled in for all of them at
    /// once, so a mode's own work never edits it. Each mode's numbers live in its own partial file
    /// (<c>Modes/GameTuning&lt;Mode&gt;.cs</c>, the hot potato's in <see cref="SpecialRoundSettings"/>).
    /// </summary>
    public sealed partial class GameTuning
    {
        /// <summary>Whether this build has event modes at all: a build of long matches has its own variety.</summary>
        public bool HasEvents => !IsMatch;

        /// <summary>
        /// The rules a round of <paramref name="kind"/> plays: the long match's in a build of matches, an event mode's
        /// own, else the plain round's (the rotation's special rounds change one rule each and keep the plain round).
        /// </summary>
        public ModeRules RulesOf(SpecialRound kind)
        {
            if (IsMatch) {
                return ModeRules.Match(this);
            }

            return kind switch {
                SpecialRound.HotPotato => SpecialRounds.HotPotato.Rules,
                SpecialRound.Crown => Crown.Rules,
                SpecialRound.GoldRush => GoldRush.Rules,
                SpecialRound.ItemFrenzy => ItemFrenzy.Rules,
                SpecialRound.Overheat => Overheat.Rules,
                SpecialRound.TeamPush => TeamPush.Rules,
                SpecialRound.Football => Football.Rules,
                _ => ModeRules.Plain,
            };
        }

        /// <summary>
        /// Whether the build plays <paramref name="mode"/>: the menu, the mode of the day and the rotation offer only
        /// modes that are on (a test still plays one that is off by asking for it).
        /// </summary>
        public bool IsModeEnabled(EventMode mode) => HasEvents && mode switch {
            EventMode.HotPotato => SpecialRounds.HotPotato.Enabled,
            EventMode.Crown => Crown.Enabled,
            EventMode.GoldRush => GoldRush.Enabled,
            EventMode.ItemFrenzy => ItemFrenzy.Enabled,
            EventMode.Overheat => Overheat.Enabled,
            EventMode.TeamPush => TeamPush.Enabled,
            EventMode.Football => Football.Enabled,
            _ => false,
        };
    }
}
