using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// An event mode's own logic in the Game scene: one entry point per mode, registered in
    /// <see cref="GameplayScope"/> always, active only in a round of its kind and silent in every other (the way
    /// <see cref="Pickups.HotPotato"/> is). What the mode changes in the shared round it states in its
    /// <see cref="ModeRules"/>; what it adds (a bomb, a crown, coins, a ball) lives here.
    /// </summary>
    public interface IRoundMode
    {
        /// <summary>The kind of round the mode plays.</summary>
        SpecialRound Kind { get; }

        /// <summary>Whether this scene's round is the mode's: fixed when the scene is built.</summary>
        bool IsActive { get; }
    }

    /// <summary>
    /// A mode that drives the bots its own way (the hot potato's chase and flight): <see cref="BotDriver"/> asks the
    /// active mode for every bot in play before its usual pick and steering.
    /// </summary>
    public interface IBotSteering
    {
        /// <summary>
        /// Steers <paramref name="bot"/> for this frame and gives true, in place of the usual targets, boxes and items;
        /// false leaves the bot to the usual play (a mode that only adds a rule, or only some of the bots).
        /// </summary>
        bool SteerBot(Bot bot, IArenaEdge edge, float deltaTime);
    }

    /// <summary>
    /// Every event mode of the scene and the one active in its round, for what is shared (the bots, the HUD): they ask
    /// here instead of naming a mode.
    /// </summary>
    public sealed class RoundModes
    {
        public RoundModes(IReadOnlyList<IRoundMode> modes)
        {
            All = modes;
            foreach (var mode in modes) {
                if (mode.IsActive) {
                    Active = mode;
                    break;
                }
            }

            BotSteering = Active as IBotSteering;
        }

        public IReadOnlyList<IRoundMode> All { get; }

        /// <summary>The mode this scene's round plays, null for a round of no mode.</summary>
        public IRoundMode Active { get; }

        /// <summary>The active mode's own bot play, null when it has none.</summary>
        public IBotSteering BotSteering { get; }

        /// <summary>The scene's mode of <typeparamref name="T"/>, active or not; null when none is registered.</summary>
        public T Get<T>() where T : class, IRoundMode
        {
            foreach (var mode in All) {
                if (mode is T found) {
                    return found;
                }
            }

            return null;
        }
    }
}
