using ToyCarArena.Audio;
using UIKit.Runtime.Abstractions;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The settings popup over the result or the garage: music and sound effects on or off, saved at once by the audio
    /// service. A round has no pause, so it never opens over a running round.
    /// </summary>
    public sealed class SettingsViewModel : ResultViewModel<bool>
    {
        private readonly IAudioService audioService;

        public SettingsViewModel(IAudioService audioService)
        {
            this.audioService = audioService;
        }

        public bool IsMusicOn => !audioService.IsMusicMuted;
        public bool IsSoundOn => !audioService.IsSoundMuted;

        public void ToggleMusic() => audioService.IsMusicMuted = !audioService.IsMusicMuted;

        public void ToggleSound() => audioService.IsSoundMuted = !audioService.IsSoundMuted;

        public void Back() => SetResult(true);
    }
}
