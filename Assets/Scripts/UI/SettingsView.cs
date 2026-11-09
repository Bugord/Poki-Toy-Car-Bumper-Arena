using System;
using System.Collections.Generic;
using Bugord.App.Localization;
using ToyCarArena.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A popup over the screen that opened it, as SMUP's settings: round music and sound toggles, grey with the icon
    /// crossed out while off, and the language coin with its list (a row per language: flag, own name, a tick on the
    /// current one). BACK, Esc, Space and Enter close it; Esc or a press outside closes an open list first.
    /// </summary>
    public sealed class SettingsView : FadingView<SettingsViewModel>
    {
        private const string OffClass = "settings__toggle--off";
        private const string ToggleOpenClass = "settings__toggle--open";
        private const string ListOpenClass = "language-list--open";
        private const string CurrentRowClass = "language-row--current";
        private const string FlagClassPrefix = "language-flag--";

        // Where the list sits against the coin, in reference px: its right edge past the coin's centre (the notch's
        // right in ui_screens.uss follows it), its top under the coin's drop.
        private const float ListRightOfCentre = 157f;
        private const float ListBelowCoin = 18f;

        private readonly IAudioService audioService;
        private readonly Dictionary<string, VisualElement> languageRows = new();
        private Button musicToggle;
        private Button soundToggle;
        private Button languageToggle;
        private Button backButton;
        private VisualElement languageFlag;
        private VisualElement languageList;
        private string flagClass;
        private bool isClosing;

        public SettingsView(IAudioService audioService)
        {
            this.audioService = audioService;
        }

        protected override void OnAttached()
        {
            // The outlined texts of the UXML get their drop layer (StrokeText).
            StrokeText.AddDrops(Root);
            SafeArea.Pad(Root);
            Root.focusable = true;
            Root.delegatesFocus = true;
            Root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            Root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            musicToggle = Prepare("music-toggle", ToggleMusic);
            soundToggle = Prepare("sound-toggle", ToggleSound);
            languageToggle = Prepare("language-toggle", ToggleLanguages);
            backButton = Prepare("settings-back", Back);
            languageFlag = Root.Q<VisualElement>("language-flag");
            languageList = Root.Q<VisualElement>("language-list");
            foreach (var code in ViewModel.Languages) {
                languageRows[code] = AddLanguageRow(code);
            }
            languageList.parent.RegisterCallback<GeometryChangedEvent>(OnCardGeometryChanged);

            Strings.Changed += RefreshLanguage;
            RefreshLanguage();
            Refresh();
        }

        public override void Dispose()
        {
            Strings.Changed -= RefreshLanguage;
            Root?.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Root?.UnregisterCallback<NavigationCancelEvent>(OnCancel);
            Root?.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            languageList?.parent?.UnregisterCallback<GeometryChangedEvent>(OnCardGeometryChanged);
            if (musicToggle != null) {
                musicToggle.clicked -= ToggleMusic;
                soundToggle.clicked -= ToggleSound;
                languageToggle.clicked -= ToggleLanguages;
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

        private void RefreshLanguage()
        {
            var language = Strings.Language;
            if (flagClass != null) {
                languageFlag.RemoveFromClassList(flagClass);
            }

            flagClass = FlagClassPrefix + language;
            languageFlag.AddToClassList(flagClass);
            foreach (var row in languageRows) {
                row.Value.EnableInClassList(CurrentRowClass, row.Key == language);
            }
        }

        private VisualElement AddLanguageRow(string code)
        {
            var row = new Button(() => PickLanguage(code));
            row.RemoveFromClassList(Button.ussClassName);
            row.focusable = false;
            row.AddToClassList("language-row");

            var flag = new VisualElement { pickingMode = PickingMode.Ignore };
            flag.AddToClassList("language-row__flag");
            flag.AddToClassList("language-flag");
            flag.AddToClassList(FlagClassPrefix + code);
            row.Add(flag);

            var name = new Label(LanguageChoice.NativeName(code)) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("language-row__name");
            row.Add(name);

            var check = new VisualElement { pickingMode = PickingMode.Ignore };
            check.AddToClassList("language-row__check");
            var tick = new VisualElement { pickingMode = PickingMode.Ignore };
            tick.AddToClassList("language-row__tick");
            check.Add(tick);
            row.Add(check);

            languageList.Add(row);
            return row;
        }

        private bool IsLanguageListOpen => languageList.ClassListContains(ListOpenClass);

        private void ToggleLanguages()
        {
            if (!IsShown || isClosing) {
                return;
            }

            audioService.PlayUiClick();
            if (IsLanguageListOpen) {
                CloseLanguages();
                return;
            }

            languageList.AddToClassList(ListOpenClass);
            languageToggle.AddToClassList(ToggleOpenClass);
            PlaceLanguageList();
        }

        private void PickLanguage(string code)
        {
            if (!IsShown || isClosing) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.PickLanguage(code);
            CloseLanguages();
        }

        private void CloseLanguages()
        {
            languageList.RemoveFromClassList(ListOpenClass);
            languageToggle.RemoveFromClassList(ToggleOpenClass);
        }

        private void OnCardGeometryChanged(GeometryChangedEvent evt) => PlaceLanguageList();

        /// <summary>
        /// Puts the list under the coin, in the card's padding box (where absolute offsets count from). It goes by the
        /// list's right edge, because the list has no width yet the moment it opens (it was not displayed); so the notch
        /// over the coin's centre is a fixed offset from that edge, set in the USS.
        /// </summary>
        private void PlaceLanguageList()
        {
            if (!IsLanguageListOpen) {
                return;
            }

            var card = languageList.parent;
            var coin = languageToggle.layout;
            var bottomCentre = languageToggle.parent.ChangeCoordinatesTo(card, new Vector2(coin.center.x, coin.yMax));
            var cardStyle = card.resolvedStyle;
            var paddingBoxWidth = cardStyle.width - cardStyle.borderLeftWidth - cardStyle.borderRightWidth;
            var centreX = bottomCentre.x - cardStyle.borderLeftWidth;
            languageList.style.right = paddingBoxWidth - centreX - ListRightOfCentre;
            languageList.style.top = bottomCentre.y - cardStyle.borderTopWidth + ListBelowCoin;
        }

        /// <summary>
        /// A press outside the open list and its coin closes the list and goes no further, so it presses nothing under it.
        /// </summary>
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (languageList == null || !IsLanguageListOpen || evt.target is not VisualElement target
                || languageList.Contains(target) || languageToggle.Contains(target)) {
                return;
            }

            CloseLanguages();
            evt.StopPropagation();
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
                if (evt.keyCode == KeyCode.Escape && IsLanguageListOpen) {
                    CloseLanguages();
                    return;
                }

                Back();
            }
        }

        private void OnCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            if (IsLanguageListOpen) {
                CloseLanguages();
                return;
            }

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
