using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// A round's result beside the player's personal bests from before it, as <see cref="PersonalBests.Record"/> made it:
    /// what the result screen needs to say "NEW BEST!", or what the best to beat is. RoundState attaches the saved XP
    /// reward too, so the result can replay it without being responsible for saving progress.
    /// </summary>
    public readonly struct RoundRecord
    {
        public readonly RoundResult Result;

        /// <summary>The reward is already saved before the result screen starts animating it.</summary>
        public readonly XpGain XpGain;

        /// <summary>The played handle, captured before the visit roster moves on.</summary>
        public readonly string KnockedOutByName;
        public IReadOnlyList<string> Names { get; }

        /// <summary>The cars' levels as the round showed them, by index, captured before the visit roster moves on.</summary>
        public IReadOnlyList<int> Levels { get; }

        /// <summary>The index of the REVENGE target as the round ended, or <see cref="RoundResult.Nobody"/>.</summary>
        public readonly int Nemesis;

        /// <summary>The saved coin reward, beside the XP.</summary>
        public readonly CoinGain Coins;

        /// <summary>What the round did for the missions, already saved; null where nothing tracks them.</summary>
        public readonly MissionRound Missions;

        /// <summary>The win streak going into the round and after it.</summary>
        public readonly StreakChange Streak;

        /// <summary>The match winner turning on its podium, drawn by the arena still loaded; null without one.</summary>
        public readonly Texture Podium;

        /// <summary>
        /// The best place before this round, 1 being a win; 0 when no round with a place was played yet.
        /// </summary>
        public readonly int BestPlace;

        /// <summary>
        /// The most cars knocked out in one round before this one.
        /// </summary>
        public readonly int BestKnockouts;

        /// <summary>
        /// Whether this round's place beat <see cref="BestPlace"/>. The very first round sets the best without beating
        /// it: any place would be a "best" then, which says nothing.
        /// </summary>
        public readonly bool IsBestPlace;

        /// <summary>
        /// Whether this round's knock-outs beat <see cref="BestKnockouts"/>; never for none.
        /// </summary>
        public readonly bool IsBestKnockouts;

        public RoundRecord(RoundResult result, int bestPlace, int bestKnockouts, bool isBestPlace, bool isBestKnockouts,
            XpGain xpGain = default, string knockedOutByName = "", IReadOnlyList<string> names = null,
            IReadOnlyList<int> levels = null, int nemesis = RoundResult.Nobody, Texture podium = null,
            CoinGain coins = default, MissionRound missions = null, StreakChange streak = default)
        {
            this.Coins = coins;
            this.Missions = missions;
            this.Streak = streak;
            this.Result = result;
            this.BestPlace = bestPlace;
            this.BestKnockouts = bestKnockouts;
            this.IsBestPlace = isBestPlace;
            this.IsBestKnockouts = isBestKnockouts;
            this.XpGain = xpGain;
            this.KnockedOutByName = knockedOutByName;
            this.Names = names ?? Array.Empty<string>();
            this.Levels = levels ?? Array.Empty<int>();
            this.Nemesis = nemesis;
            this.Podium = podium;
        }

        /// <summary>Attaches the saved XP reward without changing the bests captured before this round.</summary>
        public RoundRecord WithXp(XpGain gain) =>
            new(Result, BestPlace, BestKnockouts, IsBestPlace, IsBestKnockouts, gain, KnockedOutByName, Names, Levels,
                Nemesis, Podium, Coins, Missions, Streak);

        public RoundRecord WithKnocker(string name) =>
            new(Result, BestPlace, BestKnockouts, IsBestPlace, IsBestKnockouts, XpGain, name, Names, Levels, Nemesis,
                Podium, Coins, Missions, Streak);

        /// <summary>The round's names and levels by index, and its REVENGE target, for the result's table.</summary>
        public RoundRecord WithRoster(IReadOnlyList<string> names, IReadOnlyList<int> levels, int nemesis) =>
            new(Result, BestPlace, BestKnockouts, IsBestPlace, IsBestKnockouts, XpGain, KnockedOutByName, names, levels,
                nemesis, Podium, Coins, Missions, Streak);

        /// <summary>The round's coins, missions and streak, saved beside its XP.</summary>
        public RoundRecord WithMeta(CoinGain coins, MissionRound missions, StreakChange streak) =>
            new(Result, BestPlace, BestKnockouts, IsBestPlace, IsBestKnockouts, XpGain, KnockedOutByName, Names, Levels,
                Nemesis, Podium, coins, missions, streak);

        public RoundRecord WithPodium(Texture podium) =>
            new(Result, BestPlace, BestKnockouts, IsBestPlace, IsBestKnockouts, XpGain, KnockedOutByName, Names, Levels,
                Nemesis, podium, Coins, Missions, Streak);
    }

    /// <summary>
    /// The win streak through a round: <see cref="Before"/> going in (from <see cref="GameTuning.StreakSettings.Start"/>
    /// that round was a streak round, multiplied) and <see cref="After"/> going into the next.
    /// </summary>
    public readonly struct StreakChange
    {
        public readonly int Before;
        public readonly int After;
        public readonly int Start;

        public StreakChange(int before, int after, int start)
        {
            this.Before = before;
            this.After = after;
            this.Start = start;
        }

        /// <summary>Whether the round just played was a streak round, its reward multiplied.</summary>
        public bool WasStreakRound => Start > 0 && Before >= Start;

        /// <summary>Whether the next round is a streak round.</summary>
        public bool IsStreak => Start > 0 && After >= Start;

        /// <summary>Whether a streak of <see cref="Start"/> or more ended with this round.</summary>
        public bool IsLost => WasStreakRound && After == 0;
    }
}
