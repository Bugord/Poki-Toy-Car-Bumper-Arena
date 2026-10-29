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

        public ProgressDevConsole(PlayerProgress progress)
        {
            this.progress = progress;
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
        }

        public void Dispose()
        {
            CommandRegistry.Unregister("xp");
            CommandRegistry.Unregister("xp_add");
        }

        private string Describe() => $"{progress.TotalXp} XP, LVL {progress.Level}";
    }
}
#endif
