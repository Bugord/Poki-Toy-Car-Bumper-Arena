using System;
using ToyCarArena.Gameplay.Input;
using UnityEngine;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Sends a novice's decisions through PlayerDriver, so edge assist and boost rules stay the game's own.
    /// </summary>
    public sealed class SimulationInput : IInputService
    {
        public event Action<InputMethod> MoveUsed;
        public event Action BoostPressed;

        public Vector2 CurrentMoveInput { get; private set; }

        public bool TryGetDrag(out PointerDrag drag)
        {
            drag = default;
            return false;
        }

        public void SetMove(Vector2 move)
        {
            this.CurrentMoveInput = move;
            if (move != Vector2.zero) {
                MoveUsed?.Invoke(InputMethod.Keyboard);
            }
        }

        public void PressBoost()
        {
            BoostPressed?.Invoke();
        }
    }
}
