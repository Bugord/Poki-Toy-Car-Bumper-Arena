#if BUGORD_SUPERCONSOLE
using System;
using Bugord.SuperConsole.Commands;
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

        public ProgressDevConsole(PlayerProgress progress, AdBreaks adBreaks)
        {
            this.progress = progress;
            this.adBreaks = adBreaks;
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
        }

        public void Dispose()
        {
            CommandRegistry.Unregister("xp");
            CommandRegistry.Unregister("xp_add");
            CommandRegistry.Unregister("ad_play");
            CommandRegistry.Unregister("xp_boost");
        }

        private string Describe() => $"{progress.TotalXp} XP, LVL {progress.Level}";
    }
}
#endif
