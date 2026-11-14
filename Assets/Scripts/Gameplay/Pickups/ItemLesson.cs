using System;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// The item lesson on the floor: until the player has used an item once (<see cref="IItemLesson"/>), each round with
    /// boxes starts with one waiting <see cref="GameTuning.PickupSettings.LessonDistance"/> metres in front of their car,
    /// towards the middle, laid as the cars are placed, so the countdown already shows it and the first move takes an
    /// item; the HUD's hint then says how to use it. The player's first use, in any round, ends the lesson for good.
    /// <para>
    /// ITEM FRENZY lays a box in front of every car itself, so the lesson lays none there. A scene without boxes builds
    /// nothing and draws no dice.
    /// </para>
    /// </summary>
    public sealed class ItemLesson : ITickable, IDisposable
    {
        // How many steps the box may come back towards the player where the floor ends sooner.
        private const int Tries = 6;

        private readonly GameTuning.PickupSettings settings;
        private readonly ArenaRound round;
        private readonly ArenaPickups pickups;
        private readonly IItemLesson lesson;
        private readonly bool isLaying;
        private Car player;
        private bool isStarted;

        public ItemLesson(GameTuning tuning, ArenaRound round, ArenaPickups pickups, IItemLesson lesson, RoundRules rules)
        {
            this.settings = tuning.Pickups;
            this.round = round;
            this.pickups = pickups;
            this.lesson = lesson;
            this.isLaying = rules.Kind != SpecialRound.ItemFrenzy;
        }

        /// <summary>Where the lesson's box was laid this round; null when it laid none.</summary>
        public Vector3? Box { get; private set; }

        public void Tick()
        {
            if (isStarted || !pickups.IsEnabled || !round.IsPlaced) {
                return;
            }

            isStarted = true;
            player = round.Player;
            if (player == null) {
                return;
            }

            player.ItemUsed += OnPlayerUsed;
            if (isLaying && !lesson.HasUsedItem && settings.LessonDistance > 0f) {
                LayBox();
            }
        }

        public void Dispose()
        {
            if (player != null) {
                player.ItemUsed -= OnPlayerUsed;
            }
        }

        /// <summary>
        /// The box in front of the player, towards the middle, on safe floor clear of the edge; brought back towards the
        /// car where the floor ends sooner, and none when no step fits.
        /// </summary>
        private void LayBox()
        {
            var edge = round.Edge;
            var forward = Flat(player.transform.forward);
            if (forward.sqrMagnitude < 0.01f) {
                forward = -Flat(player.Position);
            }

            if (forward.sqrMagnitude < 0.01f) {
                return;
            }

            forward.Normalize();
            var nearest = Mathf.Min(settings.PickRadius * 1.5f, settings.LessonDistance);
            for (var step = 0; step < Tries; step++) {
                var distance = Mathf.Lerp(settings.LessonDistance, nearest, step / (Tries - 1f));
                var spot = Flat(player.Position) + forward * distance;
                if (edge.DistanceToEdge(spot, settings.VanishMargin + settings.PickRadius) < 0f) {
                    continue;
                }

                if (pickups.Spawn(spot, player) != null) {
                    Box = spot;
                }

                return;
            }
        }

        private void OnPlayerUsed(ItemKind kind) => lesson.MarkItemUsed();

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
