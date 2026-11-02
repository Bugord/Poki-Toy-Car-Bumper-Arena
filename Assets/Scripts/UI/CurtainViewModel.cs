using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Opaque cover over everything while the arena scene loads or reloads. A popup, so it stacks above the
    /// current screen instead of replacing it.
    /// </summary>
    public sealed class CurtainViewModel : ViewModel, IPopup
    {
        /// <summary>
        /// How long the lobby shows at the least, from when it is shown: long enough to read who is in the match, even
        /// when the arena loads faster.
        /// </summary>
        public const float LobbyDuration = 1.6f;

        public event Action LobbyShown;
        public IReadOnlyList<LobbyRow> Rows { get; private set; }
        public double StartedAt { get; private set; }
        public bool IsReady { get; private set; }

        /// <summary>Name of the arena of the coming round, or null when the lobby does not say.</summary>
        public string ArenaName { get; private set; }

        /// <summary>Whether the player has not yet played that arena.</summary>
        public bool ArenaIsNew { get; private set; }

        public void ShowLobby(ISessionRoster roster, int botCount, int nemesis, string arenaName = null, bool arenaIsNew = false)
        {
            ArenaName = arenaName;
            ArenaIsNew = arenaIsNew;
            var rows = new List<LobbyRow> { new(0, roster.ColorOf(0), GameText.You, roster.LevelOf(0), null, false) };
            for (var index = 1; index <= SessionRoster.MaxBots; index++) {
                var left = roster.LeftNameOf(index);
                if (index <= botCount || left != null) {
                    var isSeated = index <= botCount;
                    rows.Add(new LobbyRow(index, roster.ColorOf(index), isSeated ? roster.NameOf(index) : null,
                        isSeated ? roster.LevelOf(index) : 0, left, index == nemesis));
                }
            }
            Rows = rows.AsReadOnly();
            StartedAt = Time.realtimeSinceStartupAsDouble;
            IsReady = false;
            LobbyShown?.Invoke();
        }

        /// <summary>Loading and the old screen's fade have finished; READY! waits for the lobby's least time.</summary>
        public async UniTask Ready(CancellationToken token)
        {
            var deadline = StartedAt + LobbyDuration;
            while (Time.realtimeSinceStartupAsDouble < deadline) {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            IsReady = true;
        }
    }

    public readonly struct LobbyRow
    {
        public readonly int Index;
        public readonly int ColorId;
        public readonly string Name;
        /// <summary>The seated player's level, 0 for a row that only leaves.</summary>
        public readonly int Level;
        public readonly string LeftName;
        public readonly bool IsNemesis;

        public LobbyRow(int index, int colorId, string name, int level, string leftName, bool isNemesis)
        {
            this.Index = index;
            this.ColorId = colorId;
            this.Name = name;
            this.Level = level;
            this.LeftName = leftName;
            this.IsNemesis = isNemesis;
        }
    }
}
