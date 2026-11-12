using System;
using System.Collections.Generic;
using Bugord.App.Localization;
using ToyCarArena.Gameplay;
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

        public static string Topper(string id) => Strings.Get($"topper.{id}.name");

        /// <summary>A special round's name (MEGA ROUND!), or null for a plain round.</summary>
        public static string Special(SpecialRound kind) =>
            kind == SpecialRound.None ? null : Strings.Get($"special.{Gameplay.Round.RoundRules.IdOf(kind)}.name");

        /// <summary>A special round's rule in one line, or null for a plain round.</summary>
        public static string SpecialHint(SpecialRound kind) =>
            kind == SpecialRound.None ? null : Strings.Get($"special.{Gameplay.Round.RoundRules.IdOf(kind)}.hint");

        /// <summary>
        /// A mission's task with its number: the text follows what it counts and whether it asks for it in one round, so
        /// a new mission in the pool needs no new string; <paramref name="arena"/> names the arena of one on an arena.
        /// </summary>
        public static string Mission(GameTuning.MissionDefinition mission, string arena)
        {
            if (mission.Counter == MissionCounter.EventRounds) {
                // PLAY CROWN: the mode of the day by its menu name.
                var look = ModeLooks.Of(EventModes.Parse(mission.Mode));
                return Strings.Format("mission.play_event", look != null ? Strings.Get(look.NameKey) : "");
            }

            var target = mission.Target;
            var key = mission.Counter switch {
                MissionCounter.Rounds => target == 1 ? "mission.rounds_one" : "mission.rounds",
                MissionCounter.Wins => target == 1 ? "mission.win_one" : "mission.wins",
                MissionCounter.Knockouts => mission.IsInOneRound ? "mission.round_knockouts" : "mission.knockouts",
                MissionCounter.FinalTwo => "mission.final_two",
                MissionCounter.Boosts => mission.IsInOneRound ? "mission.round_boosts" : "mission.boosts",
                MissionCounter.Revenge => "mission.revenge",
                MissionCounter.ItemsUsed => "mission.items_used",
                MissionCounter.Boxes => "mission.boxes",
                MissionCounter.ItemKnockouts => "mission.item_knockouts",
                _ => "mission.arena_rounds",
            };
            return Strings.Format(key, target, arena ?? "");
        }
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
