using System.Collections.Generic;
using Bugord.App.Localization;
using ToyCarArena.Audio;
using UIKit.Runtime.Abstractions;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The settings popup over the result, the garage or the running round: music and sound effects on or off, saved at
    /// once by the audio service, and the language, saved by the kit's language choice. A round has no pause, so over
    /// the HUD the round goes on under it.
    /// </summary>
    public sealed class SettingsViewModel : ResultViewModel<bool>
    {
        private readonly IAudioService audioService;
        private readonly LanguageChoice languages;

        public SettingsViewModel(IAudioService audioService, LanguageChoice languages)
        {
            this.audioService = audioService;
            this.languages = languages;
        }

        public IReadOnlyList<string> Languages => languages.Languages;

        public bool IsMusicOn => !audioService.IsMusicMuted;
        public bool IsSoundOn => !audioService.IsSoundMuted;

        public void ToggleMusic() => audioService.IsMusicMuted = !audioService.IsMusicMuted;

        public void ToggleSound() => audioService.IsSoundMuted = !audioService.IsSoundMuted;

        public void PickLanguage(string code) => languages.Choose(code);

        public void Back() => SetResult(true);
    }
}
