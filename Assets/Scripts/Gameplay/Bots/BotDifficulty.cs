using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Bots
{
    /// <summary>
    /// How hard the bots play the player this round, blended between <see cref="GameTuning.BotSettings.Easy"/> and
    /// <see cref="GameTuning.BotSettings.Normal"/>. The brief's soft start: the first round of a visit is easy, so a new
    /// player wins about half the time and wants another go; over <see cref="GameTuning.BotSettings.RampRounds"/>
    /// rounds it grows to normal, and a run of lost rounds steps it back part of the way, so a struggling player is not
    /// stuck losing. Fixed for the round, so the bots do not change mid-round.
    /// </summary>
    public sealed class BotDifficulty
    {
        public BotDifficulty(GameTuning.BotSettings settings, ISessionProgress progress)
        {
            var ramp = settings.RampRounds > 0 ? Mathf.Clamp01((progress.Round - 1f) / settings.RampRounds) : 1f;
            var streak = progress.LosingStreak - settings.StreakStart + 1;
            var ease = streak > 0 ? Mathf.Min(settings.MaxStreakEase, settings.StreakEase * streak) : 0f;
            this.Level = Mathf.Clamp01(ramp - ease);

            var easy = settings.Easy;
            var normal = settings.Normal;
            var level = this.Level;
            this.PlayerPace = Mathf.Lerp(easy.PlayerPace, normal.PlayerPace, level);
            this.PlayerWeight = Mathf.Lerp(easy.PlayerWeight, normal.PlayerWeight, level);
            this.PlayerGrace = Mathf.Lerp(easy.PlayerGrace, normal.PlayerGrace, level);
            this.MaxPlayerHunters = Mathf.RoundToInt(Mathf.Lerp(easy.MaxPlayerHunters, normal.MaxPlayerHunters, level));
            this.ReactionTime = Vector2.Lerp(easy.ReactionTime, normal.ReactionTime, level);
            this.AimError = Mathf.Lerp(easy.AimError, normal.AimError, level);
            this.BoostDelay = Vector2.Lerp(easy.BoostDelay, normal.BoostDelay, level);
            this.EscapeBoostChance = Mathf.Lerp(easy.EscapeBoostChance, normal.EscapeBoostChance, level);
        }

        /// <summary>
        /// From 0, easy, to 1, normal.
        /// </summary>
        public float Level { get; }

        public float PlayerPace { get; }
        public float PlayerWeight { get; }
        public float PlayerGrace { get; }
        public int MaxPlayerHunters { get; }
        public Vector2 ReactionTime { get; }
        public float AimError { get; }
        public Vector2 BoostDelay { get; }
        public float EscapeBoostChance { get; }
    }
}
