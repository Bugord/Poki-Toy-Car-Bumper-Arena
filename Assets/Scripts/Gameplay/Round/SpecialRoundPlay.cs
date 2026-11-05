using System;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Puts this scene's special rule (<see cref="RoundRules"/>) on the arena: the ice is a layer of the floor's feel,
    /// low gravity is the physics' own for as long as the scene lives, and the giants of a mega round and the boss grow
    /// as soon as the cars stand on the floor, before the curtain is up. The hot potato has its own
    /// (<see cref="Pickups.HotPotato"/>). A plain round does nothing here.
    /// </summary>
    public sealed class SpecialRoundPlay : ITickable, ISurfaceLayer, IDisposable
    {
        // The project's gravity, kept from the first scene that changed it: a later scene must not keep a low one.
        private static Vector3? shippedGravity;

        private readonly GameTuning.SpecialRoundSettings settings;
        private readonly RoundRules rules;
        private readonly ArenaRound round;
        private readonly SurfaceFeel ice;

        private bool isSized;

        public SpecialRoundPlay(GameTuning tuning, RoundRules rules, ArenaRound round, ArenaSurface surface)
        {
            this.settings = tuning.SpecialRounds;
            this.rules = rules;
            this.round = round;
            this.ice = new SurfaceFeel(1f, settings.IceAcceleration, settings.IceGrip, settings.IcePush, 1f);
            if (rules.Kind == SpecialRound.Ice) {
                surface.Add(this);
            }

            if (rules.Kind == SpecialRound.LowGravity) {
                shippedGravity ??= Physics.gravity;
                Physics.gravity = shippedGravity.Value * settings.GravityScale;
            }
        }

        public SurfaceFeel Sample(Vector3 position) => ice;

        public void Tick()
        {
            if (isSized || !round.IsPlaced) {
                return;
            }

            isSized = true;
            if (rules.Kind == SpecialRound.Mega) {
                foreach (var car in round.Cars) {
                    car.SetBaseSize(settings.MegaSize, settings.MegaMass);
                }
            }
            else if (round.Boss != null) {
                round.Boss.SetBaseSize(settings.BossSize, settings.BossMass);
            }
        }

        public void Dispose()
        {
            if (rules.Kind == SpecialRound.LowGravity && shippedGravity.HasValue) {
                Physics.gravity = shippedGravity.Value;
            }
        }
    }
}
