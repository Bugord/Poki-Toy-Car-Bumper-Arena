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
        public event Action<BoostInputMethod> BoostPressed;
        public event Action ItemPressed;
        public event Action<Vector2> Swiped;

        public Vector2 CurrentMoveInput { get; private set; }
        private PointerDrag? pointer;

        public bool TryGetDrag(out PointerDrag drag)
        {
            drag = pointer.GetValueOrDefault();
            return pointer.HasValue;
        }

        public void SetMove(Vector2 move)
        {
            this.CurrentMoveInput = move;
            if (move != Vector2.zero) {
                MoveUsed?.Invoke(InputMethod.Keyboard);
            }
        }

        public void SetPointer(PointerDrag? drag)
        {
            var started = !pointer.HasValue && drag.HasValue;
            this.pointer = drag;
            if (started) {
                MoveUsed?.Invoke(drag.Value.IsMouse ? InputMethod.Mouse : InputMethod.Touch);
            }
        }

        public void Reset()
        {
            this.pointer = null;
            this.CurrentMoveInput = Vector2.zero;
        }

        public void PressBoost(BoostInputMethod method = BoostInputMethod.Key)
        {
            BoostPressed?.Invoke(method);
        }

        public void PressItem()
        {
            ItemPressed?.Invoke();
        }

        /// <summary>
        /// A finger flicked the screen and lifted: the stroke in screen pixels, as <see cref="InputService"/> reports it.
        /// </summary>
        public void Swipe(Vector2 stroke)
        {
            MoveUsed?.Invoke(InputMethod.Touch);
            Swiped?.Invoke(stroke);
        }

        public void CancelTapBoost()
        {
        }
    }
}
