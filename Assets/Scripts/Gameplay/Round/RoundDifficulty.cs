using UnityEngine;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// How hard this round is for the player, blended between <see cref="GameTuning.BotSettings.Easy"/> and
    /// <see cref="GameTuning.BotSettings.Normal"/>: how many bots there are, how hard they play the player, and how much
    /// the player's car is kept from driving off the edge on its own. The brief's soft start: a new player's first round
    /// is easy, so a new player wins about half the time and wants another go; over
    /// <see cref="GameTuning.BotSettings.RampRounds"/> rounds it grows to normal, and a run of lost rounds in the visit
    /// steps it back part of the way, so a struggling player is not stuck losing. The ramps count the player's rounds
    /// on this device, not the visit's: a returning player has learned the game and does not get the newcomer's rounds
    /// again, only a short warm-up from <see cref="GameTuning.BotSettings.ReturnLevel"/> over the visit's first
    /// <see cref="GameTuning.BotSettings.ReturnRounds"/> rounds. Read from the session's progress as the scene is
    /// built, and fixed for the round. Duel settings take over when only the player and one bot remain: on the Poki
    /// test 38% of newcomers lost to that last bot, so the final fight needs its own levers.
    /// </summary>
    public sealed class RoundDifficulty
    {
        public RoundDifficulty(GameTuning tuning, ISessionProgress progress)
        {
            var settings = tuning.Bots;
            this.Level = LevelFor(settings, progress);

            var easy = settings.Easy;
            var normal = settings.Normal;
            var level = this.Level;
            this.BotCount = BotCountFor(tuning, progress);
            this.PlayerPace = Mathf.Lerp(easy.PlayerPace, normal.PlayerPace, level);
            this.PlayerWeight = Mathf.Lerp(easy.PlayerWeight, normal.PlayerWeight, level);
            // A player on a streak is the leader to stop: a little more wanted, beyond the usual cap.
            this.StreakWeight = progress.WinStreak >= tuning.Streaks.Start ? settings.StreakWeight : 0f;
            this.PlayerGrace = Mathf.Lerp(easy.PlayerGrace, normal.PlayerGrace, level);
            this.MaxPlayerHunters = Mathf.RoundToInt(Mathf.Lerp(easy.MaxPlayerHunters, normal.MaxPlayerHunters, level));
            this.ReactionTime = Vector2.Lerp(easy.ReactionTime, normal.ReactionTime, level);
            this.AimError = Mathf.Lerp(easy.AimError, normal.AimError, level);
            this.BoostDelay = Vector2.Lerp(easy.BoostDelay, normal.BoostDelay, level);
            this.EscapeBoostChance = Mathf.Lerp(easy.EscapeBoostChance, normal.EscapeBoostChance, level);
            this.EdgeAssist = Mathf.Lerp(easy.EdgeAssist, normal.EdgeAssist,
                LevelFor(settings, progress, settings.EdgeAssistRounds));
            this.AutoBoost = Mathf.Lerp(easy.AutoBoost, normal.AutoBoost, level);
            this.PlayerBump = Mathf.Lerp(easy.PlayerBump, normal.PlayerBump, level);
            this.PlayerStaggerScale = Mathf.Lerp(easy.PlayerStaggerScale, normal.PlayerStaggerScale, level);
            this.StaggerFromPlayer = Mathf.Lerp(easy.StaggerFromPlayer, normal.StaggerFromPlayer, level);
            this.PlayerKnockoutWindow = Mathf.Lerp(easy.PlayerKnockoutWindow, normal.PlayerKnockoutWindow, level);
            this.DuelPace = Mathf.Lerp(easy.DuelPace, normal.DuelPace, level);
            this.DuelEscapeBoostChance = Mathf.Lerp(easy.DuelEscapeBoostChance, normal.DuelEscapeBoostChance, level);
        }

        /// <summary>
        /// From 0, easy, to 1, normal.
        /// </summary>
        public float Level { get; }

        /// <summary>
        /// Bots in the round, beside the player.
        /// </summary>
        public int BotCount { get; }

        /// <summary>The lobby and arena use the same calculation, before and after scene loading.</summary>
        public static int BotCountFor(GameTuning tuning, ISessionProgress progress) =>
            Mathf.RoundToInt(Mathf.Lerp(tuning.Bots.Easy.Bots, tuning.Bots.Normal.Bots, LevelFor(tuning.Bots, progress)));

        private static float LevelFor(GameTuning.BotSettings settings, ISessionProgress progress) =>
            LevelFor(settings, progress, settings.RampRounds);

        private static float LevelFor(GameTuning.BotSettings settings, ISessionProgress progress, int rampRounds)
        {
            var before = Mathf.Max(0, progress.RoundsBefore);
            var ramp = rampRounds > 0 ? Mathf.Clamp01((before + progress.Round - 1f) / rampRounds) : 1f;
            // A returning player warms up in the visit's first rounds; one who has played only a round or two is still
            // below the warm-up on the ramp itself.
            if (before > 0) {
                var warmUp = settings.ReturnRounds > 0
                    ? Mathf.Lerp(settings.ReturnLevel, 1f, (progress.Round - 1f) / settings.ReturnRounds)
                    : 1f;
                ramp = Mathf.Min(ramp, warmUp);
            }

            var streak = progress.LosingStreak - settings.StreakStart + 1;
            var ease = streak > 0 ? Mathf.Min(settings.MaxStreakEase, settings.StreakEase * streak) : 0f;
            return Mathf.Clamp01(ramp - ease);
        }

        public float PlayerPace { get; }
        public float PlayerWeight { get; }

        /// <summary>What the player's car counts for in the bots' picks on top of the capped weight, on a streak.</summary>
        public float StreakWeight { get; }
        public float PlayerGrace { get; }
        public int MaxPlayerHunters { get; }
        public Vector2 ReactionTime { get; }
        public float AimError { get; }
        public Vector2 BoostDelay { get; }
        public float EscapeBoostChance { get; }

        /// <summary>
        /// Share of the player's steer off the arena that is taken away near the edge, from 0 to 1. Ramps over
        /// <see cref="GameTuning.BotSettings.EdgeAssistRounds"/>, longer than the bots.
        /// </summary>
        public float EdgeAssist { get; }

        /// <summary>
        /// Chance, from 0 to 1, the game boosts for the player at a ram or a rescue (<see cref="Cars.AutoBoost"/>).
        /// </summary>
        public float AutoBoost { get; }
        public float PlayerBump { get; }
        public float PlayerStaggerScale { get; }
        public float StaggerFromPlayer { get; }
        public float PlayerKnockoutWindow { get; }
        public float DuelPace { get; }
        public float DuelEscapeBoostChance { get; }
    }
}
