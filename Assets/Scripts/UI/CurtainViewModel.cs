using UIKit.Runtime.Abstractions;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Opaque cover over everything while the arena scene loads or reloads. A popup, so it stacks above the
    /// current screen instead of replacing it.
    /// </summary>
    public sealed class CurtainViewModel : ViewModel, IPopup
    {
    }
}
