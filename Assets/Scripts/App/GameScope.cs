using Bugord.App;
using UniState;
using VContainer;

namespace ToyCarArena.App
{
    /// <summary>
    /// The app scope in the Boot scene. The kit's <see cref="AppLifetimeScope"/> brings storage, logging, the platform
    /// (Poki with <c>POKI_ENABLED</c>, a local stand-in otherwise) and the state machine; this adds the game's states
    /// and services.
    /// </summary>
    public sealed class GameScope : AppLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<LevelScene>(Lifetime.Singleton);

            builder.RegisterState<ArenaState>();
            builder.RegisterAppEntryPoint<ArenaState>();
        }
    }
}
