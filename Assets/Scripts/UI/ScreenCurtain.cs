using System.Threading;
using Cysharp.Threading.Tasks;
using UIKit.Runtime.Abstractions;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Fades the curtain over the screen and away again. Both calls do nothing when it is already there or gone.
    /// </summary>
    public sealed class ScreenCurtain
    {
        private readonly IScreenNavigator navigator;

        private ScreenHandle<CurtainViewModel> handle;

        public ScreenCurtain(IScreenNavigator navigator)
        {
            this.navigator = navigator;
        }

        /// <summary>
        /// Whether the curtain is over the screen (or fading in).
        /// </summary>
        public bool IsShown => handle is { IsOpen: true };

        public async UniTask Show(CancellationToken token)
        {
            if (handle is { IsOpen: true }) {
                return;
            }

            handle = await navigator.OpenAsync<CurtainViewModel>(token);
        }

        public UniTask Hide()
        {
            var closing = handle;
            handle = null;
            return closing?.CloseAsync() ?? UniTask.CompletedTask;
        }
    }
}
