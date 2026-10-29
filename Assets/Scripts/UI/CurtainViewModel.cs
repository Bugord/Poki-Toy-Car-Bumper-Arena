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
        public const float LobbyDuration = 0.5f;
        private const float MaxExtraDelay = 0.3f;

        public event Action LobbyShown;
        public IReadOnlyList<LobbyRow> Rows { get; private set; }
        public double StartedAt { get; private set; }
        public bool IsReady { get; private set; }

        public void ShowLobby(ISessionRoster roster, int botCount, int nemesis)
        {
            var rows = new List<LobbyRow> { new(0, roster.ColorOf(0), "YOU", null, false, false) };
            for (var index = 1; index <= SessionRoster.MaxBots; index++) {
                var left = roster.LeftNameOf(index);
                if (index <= botCount || left != null) {
                    rows.Add(new LobbyRow(index, roster.ColorOf(index), index <= botCount ? roster.NameOf(index) : null, left,
                        roster.Joined(index), index == nemesis));
                }
            }
            Rows = rows.AsReadOnly();
            StartedAt = Time.realtimeSinceStartupAsDouble;
            IsReady = false;
            LobbyShown?.Invoke();
        }

        /// <summary>Loading and the old screen's fade have finished. The lobby adds at most 300 ms after them.</summary>
        public async UniTask Ready(CancellationToken token)
        {
            var deadline = Math.Min(StartedAt + LobbyDuration, Time.realtimeSinceStartupAsDouble + MaxExtraDelay);
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
        public readonly string LeftName;
        public readonly bool IsNew;
        public readonly bool IsNemesis;

        public LobbyRow(int index, int colorId, string name, string leftName, bool isNew, bool isNemesis)
        {
            this.Index = index;
            this.ColorId = colorId;
            this.Name = name;
            this.LeftName = leftName;
            this.IsNew = isNew;
            this.IsNemesis = isNemesis;
        }
    }
}
