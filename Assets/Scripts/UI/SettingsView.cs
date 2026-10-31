using System;
using ToyCarArena.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A popup over the screen that opened it: two round toggles, grey with a red slash while off. BACK, Esc, Space and
    /// Enter close it.
    /// </summary>
    public sealed class SettingsView : FadingView<SettingsViewModel>
    {
        private const string OffClass = "settings__toggle--off";

        private readonly IAudioService audioService;
        private Button musicToggle;
        private Button soundToggle;
        private Button backButton;
        private bool isClosing;

        public SettingsView(IAudioService audioService)
        {
            this.audioService = audioService;
        }

        protected override void OnAttached()
        {
            Root.focusable = true;
            Root.delegatesFocus = true;
            Root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            musicToggle = Prepare("music-toggle", ToggleMusic);
            soundToggle = Prepare("sound-toggle", ToggleSound);
            backButton = Prepare("settings-back", Back);
            Refresh();
        }

        public override void Dispose()
        {
            Root?.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Root?.UnregisterCallback<NavigationCancelEvent>(OnCancel);
            if (musicToggle != null) {
                musicToggle.clicked -= ToggleMusic;
                soundToggle.clicked -= ToggleSound;
                backButton.clicked -= Back;
            }
            base.Dispose();
        }

        private Button Prepare(string name, Action onClick)
        {
            var button = Root.Q<Button>(name);
            button.RemoveFromClassList(Button.ussClassName);
            button.clicked += onClick;
            return button;
        }

        private void Refresh()
        {
            musicToggle.EnableInClassList(OffClass, !ViewModel.IsMusicOn);
            soundToggle.EnableInClassList(OffClass, !ViewModel.IsSoundOn);
        }

        private void ToggleMusic()
        {
            if (!IsShown || isClosing) {
                return;
            }

            ViewModel.ToggleMusic();
            audioService.PlayUiClick();
            Refresh();
        }

        private void ToggleSound()
        {
            if (!IsShown || isClosing) {
                return;
            }

            // Switched back on, the click says so; switched off, it is already silent.
            ViewModel.ToggleSound();
            audioService.PlayUiClick();
            Refresh();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode is KeyCode.Escape or KeyCode.Space or KeyCode.Return or KeyCode.KeypadEnter) {
                evt.StopImmediatePropagation();
                Back();
            }
        }

        private void OnCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            Back();
        }

        private void Back()
        {
            if (!IsShown || isClosing) {
                return;
            }

            isClosing = true;
            audioService.PlayUiClick();
            ViewModel.Back();
        }
    }
}
