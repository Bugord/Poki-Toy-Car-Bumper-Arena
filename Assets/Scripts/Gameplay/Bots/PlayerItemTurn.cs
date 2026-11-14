using System;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;

namespace ToyCarArena.Gameplay.Bots
{
    /// <summary>
    /// One bot's item at the player at a time: once an item goes at the player (<see cref="Bot.Threatens(Car)"/>), no other
    /// bot's may for <see cref="GameTuning.PickupBotSettings.PlayerItemGap"/> seconds of the round; the others use theirs on
    /// other cars meanwhile, or wait their turn. Without it the bots that held their items through the round's grace
    /// (<see cref="RoundDifficulty.PlayerGrace"/>) let them all go at the player the moment it ended; so the grace ends as if
    /// an item had just gone at the player, and the first may go a gap after it. The bots' own
    /// steering and the event modes that let items go (ITEM FRENZY) both ask it; nothing is drawn from any dice here.
    /// </summary>
    public sealed class PlayerItemTurn
    {
        private readonly GameTuning tuning;
        private readonly ArenaRound round;
        private readonly ArenaPickups pickups;
        private float freeAt;

        public PlayerItemTurn(GameTuning tuning, ArenaRound round, ArenaPickups pickups, RoundDifficulty difficulty)
        {
            this.freeAt = difficulty.PlayerGrace + tuning.Pickups.Bots.PlayerItemGap;
            this.tuning = tuning;
            this.round = round;
            this.pickups = pickups;
        }

        /// <summary>A bot's item went at the player: the bot, the item, and whether it was let go rather than aimed.</summary>
        public event Action<Car, ItemKind, bool> Taken;

        /// <summary>Whether a bot's item may go at the player now.</summary>
        public bool IsFree => round.Elapsed >= freeAt;

        /// <summary>The car the bots' items keep off until its turn comes round: the player, or null while it is free.</summary>
        public Car Waiting => IsFree ? null : round.Player;

        /// <summary>
        /// Uses <paramref name="bot"/>'s item if it finds its moment (<see cref="Bot.WantsToUseItem"/>) with
        /// <paramref name="spared"/> spared and the player kept clear while the turn is taken, and takes the turn if the item
        /// went at the player. True when the item was used.
        /// </summary>
        public bool TryUse(Bot bot, IArenaEdge edge, float deltaTime, Car spared)
        {
            if (!bot.WantsToUseItem(edge, round.Cars, deltaTime, spared, Waiting)) {
                return false;
            }

            return Use(bot, false);
        }

        /// <summary>Whether <paramref name="bot"/> may let its item go now with no aim: not at the player out of turn.</summary>
        public bool MayLetGo(Bot bot)
        {
            return IsFree || !bot.Threatens(round.Player);
        }

        /// <summary>
        /// Uses <paramref name="bot"/>'s item now, aimed (<paramref name="isLetGo"/> false) or let go, and takes the turn if
        /// it goes at the player. True when the item was used.
        /// </summary>
        public bool Use(Bot bot, bool isLetGo)
        {
            var car = bot.Car;
            var kind = car.HeldItem;
            var isAtPlayer = bot.Threatens(round.Player);
            if (!pickups.Use(car)) {
                return false;
            }

            if (isAtPlayer) {
                freeAt = round.Elapsed + tuning.Pickups.Bots.PlayerItemGap;
                Taken?.Invoke(car, kind, isLetGo);
            }

            return true;
        }
    }
}
