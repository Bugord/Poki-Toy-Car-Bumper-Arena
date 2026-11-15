using System;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Cars;
using VContainer.Unity;
using UnityEngine;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// A new player's first rounds cannot be lost (<see cref="RoundDifficulty.IsPlayerSafe"/>,
    /// <see cref="GameTuning.SafetySettings"/>). The bots play as usual, so the arena is as lively as ever, but a fall
    /// of the player's car off the floor is caught as it goes over, like a balloon's catch: the car is carried to safe
    /// floor away from the others, SAVED! shows over it, and the fall is nobody's knock-out. The player's hits grow
    /// harder the longer the round goes (<see cref="Car.PowerBoost"/>), so the round ends in a win sooner or later
    /// rather than in a long duel. Outside those rounds it does nothing.
    /// </summary>
    public sealed class PlayerSafety : ITickable, IDisposable
    {
        private readonly GameTuning tuning;
        private readonly GameTuning.SafetySettings settings;
        private readonly ArenaRound round;
        private readonly IAudioService audioService;
        private readonly bool isSafe;

        public PlayerSafety(GameTuning tuning, ArenaRound round, RoundDifficulty difficulty, IAudioService audioService)
        {
            this.tuning = tuning;
            this.settings = tuning.Safety;
            this.round = round;
            this.audioService = audioService;
            this.isSafe = difficulty.IsPlayerSafe;
        }

        /// <summary>Falls of the player's car caught this round.</summary>
        public int Rescues { get; private set; }

        public void Tick()
        {
            var player = round.Player;
            if (!isSafe || player == null || !round.IsRunning || player.IsOut) {
                return;
            }

            player.PowerBoost = settings.PowerAt(round.Elapsed);
            if (IsFalling(player)) {
                Catch(player);
            }
        }

        public void Dispose()
        {
            if (round.Player != null) {
                round.Player.PowerBoost = 1f;
            }
        }

        /// <summary>
        /// Off the floor, with nothing under it, and sunk below its top: going over the edge. A balloon of its own
        /// catches it first.
        /// </summary>
        private bool IsFalling(Car car)
        {
            var movement = car.Movement;
            if (movement.IsGrounded || movement.IsCarried || car.IsRunning(ItemKind.Balloon)) {
                return false;
            }

            var position = car.Position;
            return position.y < -settings.CatchDepth && position.y > tuning.Round.FallHeight
                && !round.Edge.IsSolid(new Vector3(position.x, 0f, position.z));
        }

        private void Catch(Car car)
        {
            var spot = RespawnSpot.Find(round.Edge, tuning.Match, round.Cars, car);
            car.ForgetHit();
            car.Movement.Carry(spot + Vector3.up * settings.DropHeight, settings.CarryHeight, settings.CarryTime);
            car.ReportItemHit(ItemKind.Balloon, car.Position);
            audioService?.Play(audioService.Library.KnockoutCredit, 0.8f);
            Rescues++;
        }
    }
}
