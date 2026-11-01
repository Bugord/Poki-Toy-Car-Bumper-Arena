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
            IReadOnlyList<int> levels = null, int nemesis = RoundResult.Nobody, Texture podium = null)
        {
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
                Nemesis, Podium);

        public RoundRecord WithKnocker(string name) =>
            new(Result, BestPlace, BestKnockouts, IsBestPlace, IsBestKnockouts, XpGain, name, Names, Levels, Nemesis,
                Podium);

        /// <summary>The round's names and levels by index, and its REVENGE target, for the result's table.</summary>
        public RoundRecord WithRoster(IReadOnlyList<string> names, IReadOnlyList<int> levels, int nemesis) =>
            new(Result, BestPlace, BestKnockouts, IsBestPlace, IsBestKnockouts, XpGain, KnockedOutByName, names, levels,
                nemesis, Podium);

        public RoundRecord WithPodium(Texture podium) =>
            new(Result, BestPlace, BestKnockouts, IsBestPlace, IsBestKnockouts, XpGain, KnockedOutByName, Names, Levels,
                Nemesis, podium);
    }
}
