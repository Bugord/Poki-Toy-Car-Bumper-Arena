using UnityEngine;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// How hard this round is for the player, blended between <see cref="GameTuning.BotSettings.Easy"/> and
    /// <see cref="GameTuning.BotSettings.Normal"/>: how many bots there are, how hard they play the player, and how much
    /// the player's car is kept from driving off the edge on its own. The brief's soft start: the first round of a
    /// visit is easy, so a new player wins about half the time and wants another go; over
    /// <see cref="GameTuning.BotSettings.RampRounds"/> rounds it grows to normal, and a run of lost rounds steps it back
    /// part of the way, so a struggling player is not stuck losing. Read from the session's progress as the scene is
    /// built, and fixed for the round, so nothing changes mid-round.
    /// </summary>
    public sealed class RoundDifficulty
    {
        public RoundDifficulty(GameTuning tuning, ISessionProgress progress)
        {
            var settings = tuning.Bots;
            var ramp = settings.RampRounds > 0 ? Mathf.Clamp01((progress.Round - 1f) / settings.RampRounds) : 1f;
            var streak = progress.LosingStreak - settings.StreakStart + 1;
            var ease = streak > 0 ? Mathf.Min(settings.MaxStreakEase, settings.StreakEase * streak) : 0f;
            this.Level = Mathf.Clamp01(ramp - ease);

            var easy = settings.Easy;
            var normal = settings.Normal;
            var level = this.Level;
            this.BotCount = Mathf.RoundToInt(Mathf.Lerp(easy.Bots, normal.Bots, level));
            this.PlayerPace = Mathf.Lerp(easy.PlayerPace, normal.PlayerPace, level);
            this.PlayerWeight = Mathf.Lerp(easy.PlayerWeight, normal.PlayerWeight, level);
            this.PlayerGrace = Mathf.Lerp(easy.PlayerGrace, normal.PlayerGrace, level);
            this.MaxPlayerHunters = Mathf.RoundToInt(Mathf.Lerp(easy.MaxPlayerHunters, normal.MaxPlayerHunters, level));
            this.ReactionTime = Vector2.Lerp(easy.ReactionTime, normal.ReactionTime, level);
            this.AimError = Mathf.Lerp(easy.AimError, normal.AimError, level);
            this.BoostDelay = Vector2.Lerp(easy.BoostDelay, normal.BoostDelay, level);
            this.EscapeBoostChance = Mathf.Lerp(easy.EscapeBoostChance, normal.EscapeBoostChance, level);
            this.EdgeAssist = Mathf.Lerp(easy.EdgeAssist, normal.EdgeAssist, level);
        }

        /// <summary>
        /// From 0, easy, to 1, normal.
        /// </summary>
        public float Level { get; }

        /// <summary>
        /// Bots in the round, beside the player.
        /// </summary>
        public int BotCount { get; }

        public float PlayerPace { get; }
        public float PlayerWeight { get; }
        public float PlayerGrace { get; }
        public int MaxPlayerHunters { get; }
        public Vector2 ReactionTime { get; }
        public float AimError { get; }
        public Vector2 BoostDelay { get; }
        public float EscapeBoostChance { get; }

        /// <summary>
        /// Share of the player's steer off the arena that is taken away near the edge, from 0 to 1.
        /// </summary>
        public float EdgeAssist { get; }
    }
}
