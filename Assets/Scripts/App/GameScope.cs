using Bugord.App;
using ToyCarArena.UI;
using UIKit.Runtime.Integrations.VContainer;
using UniState;
using VContainer;

namespace ToyCarArena.App
{
    /// <summary>
    /// The app scope in the Boot scene. The kit's <see cref="AppLifetimeScope"/> brings storage, logging, the platform
    /// (Poki with <c>POKI_ENABLED</c>, a local stand-in otherwise), UIKit and the state machine; this adds the game's
    /// screens, states and services. Rounds go round from <see cref="RoundState"/> to <see cref="ResultState"/> and back.
    /// </summary>
    public sealed class GameScope : AppLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<LevelScene>(Lifetime.Singleton);
            builder.Register<ScreenCurtain>(Lifetime.Singleton);

            builder.RegisterScreen<CurtainViewModel, CurtainView>();
            builder.RegisterScreen<HudViewModel, HudView>();
            builder.RegisterScreen<RoundOverViewModel, RoundOverView>();

            builder.RegisterState<RoundState>();
            builder.RegisterState<ResultState>();
            builder.RegisterAppEntryPoint<RoundState>();
        }
    }
}
