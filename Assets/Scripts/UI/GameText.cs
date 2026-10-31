using System;
using System.Collections.Generic;
using Bugord.App.Localization;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The game's localized names. Ids and the gameplay's "YOU" stay English (analytics and the round read them); only
    /// what a screen shows goes through the string table.
    /// </summary>
    internal static class GameText
    {
        public static string You => Strings.Get("common.you");

        public static string Car(CarCatalog.Entry car) => car == null ? null : Strings.Get($"car.{car.Id}.name");

        public static string Arena(ArenaCatalog.Entry arena) =>
            arena == null ? null : Strings.Get($"arena.{arena.Id}.name");

        public static string Level(int level) => Strings.Format("common.level_short", level);
    }

    /// <summary>
    /// Labels a screen fills from code, filled again when the language changes under it (the settings popup opens over
    /// the result and the garage). UXML labels have their own <see cref="LocalizedText"/> bindings.
    /// </summary>
    internal sealed class LiveText : IDisposable
    {
        private readonly List<(TextElement label, Func<string> text)> labels = new();
        private readonly List<Action> actions = new();

        public LiveText()
        {
            Strings.Changed += Apply;
        }

        /// <summary>Sets the label now and on every language change.</summary>
        public void Set(TextElement label, Func<string> text)
        {
            for (var i = 0; i < labels.Count; i++) {
                if (labels[i].label == label) {
                    labels.RemoveAt(i);
                    break;
                }
            }

            labels.Add((label, text));
            label.text = text();
        }

        /// <summary>Runs a screen's own refresh on every language change.</summary>
        public void OnChanged(Action action) => actions.Add(action);

        public void Dispose()
        {
            Strings.Changed -= Apply;
            labels.Clear();
            actions.Clear();
        }

        private void Apply()
        {
            foreach (var (label, text) in labels) {
                label.text = text();
            }

            foreach (var action in actions) {
                action();
            }
        }
    }
}
