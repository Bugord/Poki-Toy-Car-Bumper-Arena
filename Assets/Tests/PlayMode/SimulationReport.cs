using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using ToyCarArena.Gameplay.Round;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Keeps the denominators explicit so simulated round counts can be compared with Poki's first-round metrics.
    /// </summary>
    public sealed class SimulationReport
    {
        private readonly int runs;
        private readonly int seed;
        private readonly Dictionary<int, List<RoundResult>> levels = new();

        public int HitStopRecoveries { get; set; }

        public SimulationReport(int runs, int seed)
        {
            this.runs = runs;
            this.seed = seed;
        }

        public void Add(int level, RoundResult result)
        {
            if (!levels.TryGetValue(level, out var results)) {
                results = new List<RoundResult>();
                levels.Add(level, results);
            }

            results.Add(result);
        }

        public string ToMarkdown(double realSeconds)
        {
            var text = new StringBuilder();
            text.AppendLine("# Симуляция раундов с моделью новичка");
            text.AppendLine();
            text.AppendLine($"Прогонов на уровень: {runs}. Seed = {seed} + индекс прогона (с 0), одинаковые seeds на уровнях. " +
                "Серия поражений: 0. Шаг captureDeltaTime: 1/60 с; физический шаг из проекта.");
            text.AppendLine($"Реальное время с загрузками сцен: {Number(realSeconds)} с. " +
                $"Восстановлений зависшего hit-stop: {HitStopRecoveries}.");
            text.AppendLine("Модель пока не откалибрована по живым игрокам. PhysX не гарантирует побитовую повторяемость.");
            text.AppendLine();
            text.AppendLine("| Параметр новичка | Значение |");
            text.AppendLine("| --- | ---: |");
            // Read the defaults themselves: a calibration change must also appear in the saved report.
            var defaults = new NoviceDriver(0, null, null, null);
            foreach (var field in typeof(NoviceDriver).GetFields(BindingFlags.Instance | BindingFlags.Public)) {
                text.AppendLine($"| {field.Name} | {Number((float)field.GetValue(defaults))} |");
            }

            foreach (var entry in levels) {
                var results = entry.Value;
                var count = results.Count;
                var won = results.Count(value => value.Outcome == RoundOutcome.Won);
                var outs = results.Where(value => value.Outcome == RoundOutcome.Out).ToList();
                var pushed = outs.Count(value => value.KnockedOutBy != RoundResult.Nobody);
                var falls = outs.Where(value => value.KnockedOutBy == RoundResult.Nobody).ToList();
                text.AppendLine();
                text.AppendLine($"## Раунд визита {entry.Key}");
                text.AppendLine();
                text.AppendLine("| Прогонов | Машинок | Победы | 0 KO | Среднее KO | Среднее время игрока, с |");
                text.AppendLine("| ---: | --- | ---: | ---: | ---: | ---: |");
                var cars = string.Join(", ", results.Select(value => value.CarCount).Distinct().OrderBy(value => value));
                text.AppendLine($"| {count} | {cars} | {Percent(won, count)} | " +
                    $"{Percent(results.Count(value => value.Knockouts == 0), count)} | " +
                    $"{Number(results.Average(value => value.Knockouts))} | {Number(results.Average(value => value.Seconds))} |");
                text.AppendLine();
                text.AppendLine("| Место | Раундов | Доля всех раундов |");
                text.AppendLine("| --- | ---: | ---: |");
                for (var place = 1; place <= results.Max(value => value.CarCount); place++) {
                    var placed = results.Count(value => value.Place == place);
                    text.AppendLine($"| {place} | {placed} | {Percent(placed, count)} |");
                }

                var last = results.Count(value => value.Place == value.CarCount);
                text.AppendLine($"| Последнее | {last} | {Percent(last, count)} |");
                text.AppendLine();
                text.AppendLine("| Исход | Раундов | Доля всех раундов |");
                text.AppendLine("| --- | ---: | ---: |");
                foreach (RoundOutcome outcome in Enum.GetValues(typeof(RoundOutcome))) {
                    var total = results.Count(value => value.Outcome == outcome);
                    text.AppendLine($"| {outcome.ToString().ToLowerInvariant()} | {total} | {Percent(total, count)} |");
                }

                text.AppendLine();
                text.AppendLine("| Причина выбывания | Раундов | Доля выбываний |");
                text.AppendLine("| --- | ---: | ---: |");
                text.AppendLine($"| pushed | {pushed} | {Percent(pushed, outs.Count)} |");
                text.AppendLine($"| fell | {falls.Count} | {Percent(falls.Count, outs.Count)} |");
                text.AppendLine();
                text.AppendLine("| FallCause | Раундов | Доля fell |");
                text.AppendLine("| --- | ---: | ---: |");
                foreach (var cause in new[] { FallCause.Ring, FallCause.Boost, FallCause.Slide, FallCause.Drive }) {
                    var total = falls.Count(value => value.FallCause == cause);
                    text.AppendLine($"| {cause.ToString().ToLowerInvariant()} | {total} | {Percent(total, falls.Count)} |");
                }

                text.AppendLine();
                text.AppendLine("| Время игрока, с | Раундов | Доля всех раундов |");
                text.AppendLine("| --- | ---: | ---: |");
                var buckets = new[] { 15f, 30f, 60f, float.PositiveInfinity };
                var labels = new[] { "<15", "15–30", "30–60", "60+" };
                var from = 0f;
                for (var i = 0; i < buckets.Length; i++) {
                    var to = buckets[i];
                    var total = results.Count(value => value.Seconds >= from && value.Seconds < to);
                    text.AppendLine($"| {labels[i]} | {total} | {Percent(total, count)} |");
                    from = to;
                }
            }

            text.AppendLine();
            text.AppendLine("Интервалы времени включают левую границу, исключают правую. Время игрока — RoundResult.Seconds, " +
                "без результата, рестарта и рекламы; это не длина полного цикла. «—» означает пустой знаменатель.");
            return text.ToString();
        }

        private static string Percent(int value, int count)
        {
            return count == 0 ? "—" : Number(100d * value / count) + "%";
        }

        private static string Number(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
