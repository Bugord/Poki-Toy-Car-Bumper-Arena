using System.Collections.Generic;
using Bugord.Core.Storage;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Which arena the visit plays on. Lives in the app scope, so it outlives the Game scene reloaded every round; the
    /// scene reads <see cref="Current"/> as it is built.
    /// </summary>
    public interface IArenaRotation
    {
        /// <summary>The id of the arena of the round being played, or of the next one once <see cref="Next"/> ran.</summary>
        string Current { get; }

        /// <summary>Moves on to the arena of the next round, among those open at the player's <paramref name="level"/>.</summary>
        void Next(int level);

        /// <summary>Whether the player has never started a round on this arena.</summary>
        bool IsNew(string id);

        void MarkSeen(string id);
    }

    /// <summary>
    /// Goes through the arenas of <see cref="GameTuning.Maps"/> in order, skipping those the level has not opened and
    /// never repeating one while another is open. The visit starts on the first, which keeps the first round the same
    /// for everyone. No chance in it: what comes next is known from the level alone.
    /// </summary>
    public sealed class ArenaRotation : IArenaRotation
    {
        private const string SeenKeyPrefix = "arena_seen_";

        private readonly GameTuning tuning;
        private readonly IKeyValueStorage storage;

        public ArenaRotation(GameTuning tuning, IKeyValueStorage storage)
        {
            this.tuning = tuning;
            this.storage = storage;
            this.Current = tuning.Maps[0].Id;
        }

        public string Current { get; private set; }

        public void Next(int level)
        {
            var maps = tuning.Maps;
            var from = -1;
            for (var index = 0; index < maps.Count; index++) {
                if (maps[index].Id == Current) {
                    from = index;
                    break;
                }
            }

            // A full lap ends on the arena itself, which is what stays when it is the only one open.
            for (var step = 1; step <= maps.Count; step++) {
                var map = maps[(from + step) % maps.Count];
                if (map.UnlockLevel <= level) {
                    Current = map.Id;
                    return;
                }
            }
        }

        public bool IsNew(string id) => !storage.GetBool(SeenKeyPrefix + id);

        public void MarkSeen(string id) => storage.SetBool(SeenKeyPrefix + id, true);

        /// <summary>The arenas the levels after <paramref name="before"/> up to <paramref name="after"/> opened.</summary>
        public static List<string> OpenedBetween(GameTuning tuning, int before, int after)
        {
            var opened = new List<string>();
            foreach (var map in tuning.Maps) {
                if (map.UnlockLevel > before && map.UnlockLevel <= after) {
                    opened.Add(map.Id);
                }
            }

            return opened;
        }
    }

    /// <summary>
    /// One arena for the whole play session: the scene opened on its own, which has no app scope above it, and a
    /// simulation measuring a chosen arena.
    /// </summary>
    public sealed class FixedArenaRotation : IArenaRotation
    {
        public FixedArenaRotation(string id)
        {
            Current = id;
        }

        public string Current { get; }

        public void Next(int level)
        {
        }

        public bool IsNew(string id) => false;

        public void MarkSeen(string id)
        {
        }
    }
}
