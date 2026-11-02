using System;
using System.Collections.Generic;
using System.Text;
using Bugord.Core.Storage;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The player's three missions, one per <see cref="MissionSlot"/>, saved in the kit's storage so they carry over to
    /// the next visit. A decided round counts towards them (<see cref="Record"/>); a done mission pays its tier's XP
    /// and coins and is replaced at once from its slot's pool. Lives in the app scope, which outlives the Game scene
    /// reloaded every round.
    /// <para>
    /// A player with no missions yet gets each slot's starter, so the first round nearly always ends with one done. A
    /// lesson about items is drawn only for a round that has boxes (<see cref="GameTuning.PickupSettings.FirstRound"/>),
    /// a mission on an arena only once the arena is open.
    /// </para>
    /// </summary>
    public sealed class MissionTracker
    {
        public const int SlotCount = 3;

        private const string SlotsKey = "missions";
        private const string ShownKey = "missions_shown";

        private readonly IKeyValueStorage storage;
        private readonly GameTuning tuning;
        private Random random = new();
        private readonly MissionState[] slots = new MissionState[SlotCount];

        // One constructor: the container takes the one with the most parameters.
        public MissionTracker(IKeyValueStorage storage, GameTuning tuning)
        {
            this.storage = storage;
            this.tuning = tuning;
            Load();
        }

        /// <summary>Makes the next draws repeat, for tests.</summary>
        public void Seed(int seed) => random = new Random(seed);

        /// <summary>The missions in play, by slot.</summary>
        public IReadOnlyList<MissionState> Slots => slots;

        /// <summary>Missions done in this visit, for analytics; not saved.</summary>
        public int DoneThisVisit { get; private set; }

        /// <summary>
        /// Whether the missions were ever shown to this player; the first call returns true once and remembers.
        /// </summary>
        public bool MarkShown()
        {
            if (storage.GetInt(ShownKey) != 0) {
                return false;
            }

            storage.SetInt(ShownKey, 1);
            return true;
        }

        /// <summary>
        /// How many of the missions in play a round with <paramref name="stats"/> so far would complete: the HUD shows
        /// MISSION! as this grows, before the round is decided. Nothing is saved.
        /// </summary>
        public int CountDone(MissionStats stats)
        {
            var done = 0;
            foreach (var slot in slots) {
                if (slot.Progress + Gained(slot.Definition, stats) >= slot.Definition.Target) {
                    done++;
                }
            }

            return done;
        }

        /// <summary>
        /// Counts a decided round towards every mission, saves, and replaces the done ones for the next round
        /// (<paramref name="level"/> after the round opens arenas, <paramref name="nextRound"/> of the visit gates the
        /// item lessons).
        /// </summary>
        public MissionRound Record(MissionStats stats, int level, int nextRound)
        {
            var lines = new MissionLine[SlotCount];
            var completed = new List<GameTuning.MissionDefinition>();
            var xp = 0;
            var coins = 0;
            for (var index = 0; index < SlotCount; index++) {
                var slot = slots[index];
                var definition = slot.Definition;
                var progress = Math.Min(definition.Target, slot.Progress + Gained(definition, stats));
                var isDone = progress >= definition.Target;
                if (isDone) {
                    completed.Add(definition);
                    xp += tuning.Missions.XpOf(definition.Tier);
                    coins += tuning.Missions.CoinsOf(definition.Tier);
                    slots[index] = new MissionState(Draw((MissionSlot)index, level, nextRound, definition.Id), 0);
                }
                else {
                    // A round mission asks for it inside one round, so a round short of it starts over.
                    slots[index] = new MissionState(definition, definition.IsInOneRound ? 0 : progress);
                }

                lines[index] = new MissionLine(definition, slot.Progress, progress, isDone, slots[index]);
            }

            DoneThisVisit += completed.Count;
            Save();
            return new MissionRound(lines, completed, xp, coins);
        }

        /// <summary>What a round with <paramref name="stats"/> adds to a mission counting this.</summary>
        public static int Gained(GameTuning.MissionDefinition definition, MissionStats stats) => definition.Counter switch {
            MissionCounter.Rounds => stats.IsFinished ? 1 : 0,
            MissionCounter.Wins => stats.Outcome == RoundOutcome.Won && stats.IsFinished ? 1 : 0,
            MissionCounter.Knockouts => stats.Knockouts,
            MissionCounter.FinalTwo => stats.IsFinished && stats.Place is > 0 and <= 2 ? 1 : 0,
            MissionCounter.Boosts => stats.Boosts,
            MissionCounter.Revenge => stats.RevengeTaken ? 1 : 0,
            MissionCounter.ItemsUsed => stats.ItemsUsed,
            MissionCounter.Boxes => stats.Boxes,
            MissionCounter.ItemKnockouts => stats.ItemKnockouts,
            MissionCounter.ArenaRounds => stats.IsFinished && stats.Map == definition.Map ? 1 : 0,
            _ => 0,
        };

        private void Load()
        {
            var saved = storage.GetString(SlotsKey);
            var parts = string.IsNullOrEmpty(saved) ? Array.Empty<string>() : saved.Split('|');
            var isNew = parts.Length == 0;
            for (var index = 0; index < SlotCount; index++) {
                var slot = (MissionSlot)index;
                if (index < parts.Length && TryParse(parts[index], slot, out var state)) {
                    slots[index] = state;
                }
                else {
                    var starter = isNew ? Starter(slot) : null;
                    slots[index] = new MissionState(starter ?? Draw(slot, 1, 1, null), 0);
                }
            }

            if (isNew) {
                Save();
            }
        }

        private bool TryParse(string part, MissionSlot slot, out MissionState state)
        {
            state = null;
            var colon = part.LastIndexOf(':');
            if (colon <= 0 || !int.TryParse(part.Substring(colon + 1), out var progress)) {
                return false;
            }

            var definition = Find(part.Substring(0, colon));
            if (definition == null || definition.Slot != slot) {
                return false;
            }

            state = new MissionState(definition, Math.Clamp(progress, 0, definition.Target - 1));
            return true;
        }

        private void Save()
        {
            var text = new StringBuilder();
            for (var index = 0; index < SlotCount; index++) {
                if (index > 0) {
                    text.Append('|');
                }

                text.Append(slots[index].Definition.Id).Append(':').Append(slots[index].Progress);
            }

            storage.SetString(SlotsKey, text.ToString());
        }

        private GameTuning.MissionDefinition Find(string id)
        {
            foreach (var definition in tuning.Missions.Pool) {
                if (definition.Id == id) {
                    return definition;
                }
            }

            return null;
        }

        private GameTuning.MissionDefinition Starter(MissionSlot slot)
        {
            foreach (var definition in tuning.Missions.Pool) {
                if (definition.Slot == slot && definition.IsStarter) {
                    return definition;
                }
            }

            return null;
        }

        /// <summary>
        /// A mission of <paramref name="slot"/> the player can do next: not one already in play nor the one just done,
        /// unless nothing else is left.
        /// </summary>
        private GameTuning.MissionDefinition Draw(MissionSlot slot, int level, int nextRound, string done)
        {
            var candidates = new List<GameTuning.MissionDefinition>();
            foreach (var definition in tuning.Missions.Pool) {
                if (definition.Slot == slot && !definition.IsStarterOnly && IsAvailable(definition, level, nextRound) &&
                    definition.Id != done && !IsInPlay(definition.Id)) {
                    candidates.Add(definition);
                }
            }

            if (candidates.Count == 0) {
                foreach (var definition in tuning.Missions.Pool) {
                    if (definition.Slot == slot && !definition.IsStarterOnly && IsAvailable(definition, level, nextRound)) {
                        candidates.Add(definition);
                    }
                }
            }

            if (candidates.Count == 0) {
                throw new InvalidOperationException($"GameTuning.Missions has no mission for the {slot} slot.");
            }

            return candidates[random.Next(candidates.Count)];
        }

        private bool IsAvailable(GameTuning.MissionDefinition definition, int level, int nextRound)
        {
            if (definition.NeedsItems) {
                var first = tuning.Pickups.FirstRound;
                if (!tuning.Pickups.Enabled || (!tuning.IsMatch && (first <= 0 || nextRound < first))) {
                    return false;
                }
            }

            if (definition.Map.Length == 0) {
                return true;
            }

            foreach (var map in tuning.Maps) {
                if (map.Id == definition.Map) {
                    return map.UnlockLevel <= level;
                }
            }

            return false;
        }

        private bool IsInPlay(string id)
        {
            foreach (var slot in slots) {
                if (slot != null && slot.Definition.Id == id) {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>A mission in play and how far the player got.</summary>
    public sealed class MissionState
    {
        public MissionState(GameTuning.MissionDefinition definition, int progress)
        {
            Definition = definition;
            Progress = progress;
        }

        public GameTuning.MissionDefinition Definition { get; }
        public int Progress { get; }
    }

    /// <summary>
    /// What a round did for the missions, as the result screen shows it: a line per slot, the done missions and their
    /// reward, already saved.
    /// </summary>
    public sealed class MissionRound
    {
        public MissionRound(IReadOnlyList<MissionLine> lines, IReadOnlyList<GameTuning.MissionDefinition> completed,
            int xp, int coins)
        {
            Lines = lines;
            Completed = completed;
            Xp = xp;
            Coins = coins;
        }

        public IReadOnlyList<MissionLine> Lines { get; }
        public IReadOnlyList<GameTuning.MissionDefinition> Completed { get; }
        public int Xp { get; }
        public int Coins { get; }
    }

    /// <summary>A slot through the round: the mission played, its progress before and after, and what replaces it.</summary>
    public readonly struct MissionLine
    {
        public readonly GameTuning.MissionDefinition Definition;
        public readonly int Before;
        public readonly int After;
        public readonly bool IsDone;
        public readonly MissionState Next;

        public MissionLine(GameTuning.MissionDefinition definition, int before, int after, bool isDone, MissionState next)
        {
            this.Definition = definition;
            this.Before = before;
            this.After = after;
            this.IsDone = isDone;
            this.Next = next;
        }
    }

    /// <summary>
    /// What a round gave the missions to count: the result and the player's own counters. <see cref="IsFinished"/> is
    /// false while the round still runs, so only what already happened counts then.
    /// </summary>
    public readonly struct MissionStats
    {
        public readonly bool IsFinished;
        public readonly RoundOutcome Outcome;
        public readonly int Place;
        public readonly int Knockouts;
        public readonly int Boosts;
        public readonly bool RevengeTaken;
        public readonly int ItemsUsed;
        public readonly int Boxes;
        public readonly int ItemKnockouts;
        public readonly string Map;

        public MissionStats(bool isFinished, RoundOutcome outcome, int place, int knockouts, int boosts,
            bool revengeTaken, int itemsUsed, int boxes, int itemKnockouts, string map)
        {
            this.IsFinished = isFinished;
            this.Outcome = outcome;
            this.Place = place;
            this.Knockouts = knockouts;
            this.Boosts = boosts;
            this.RevengeTaken = revengeTaken;
            this.ItemsUsed = itemsUsed;
            this.Boxes = boxes;
            this.ItemKnockouts = itemKnockouts;
            this.Map = map ?? "";
        }
    }
}
