using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Moves the parts of a topper that live (an aerial or a plume that sways, a star that blinks, a ring that orbits),
    /// set up by <see cref="CarTopper.Build"/>. It sits on the topper's root and moves only its own children, so the
    /// topper lives wherever it is shown: on the arena, in the garage, on the podium's copy (Instantiate carries the
    /// serialized parts over to the copy's own children) and in the result's popups. Unscaled time: the hit-stop and the
    /// ad pause do not freeze it. Nothing is allocated a frame.
    /// </summary>
    public sealed class TopperMotion : MonoBehaviour
    {
        private enum Kind
        {
            Sway,
            Spin,
            Pulse,
        }

        [Serializable]
        private struct Move
        {
            public Kind Kind;
            public Transform Part;
            public Quaternion Rest;
            public Vector3 Scale;
            public Vector3 Axis;
            public float Speed;
            public float Amount;
            public float Phase;
        }

        [SerializeField]
        private List<Move> moves = new();

        /// <summary>
        /// A part that sways round <paramref name="axis"/> (its own) by up to <paramref name="angle"/> degrees,
        /// <paramref name="speed"/> radians a second. Several sways of one part add up.
        /// </summary>
        public void AddSway(Transform part, Vector3 axis, float speed, float angle, float phase) =>
            Add(Kind.Sway, part, axis, speed, angle, phase);

        /// <summary>A part that turns round <paramref name="axis"/> (its own), <paramref name="speed"/> degrees a second.</summary>
        public void AddSpin(Transform part, Vector3 axis, float speed) => Add(Kind.Spin, part, axis, speed, 0f, 0f);

        /// <summary>A part that grows and shrinks by <paramref name="amount"/> of its size, <paramref name="speed"/> radians a second.</summary>
        public void AddPulse(Transform part, float speed, float amount) =>
            Add(Kind.Pulse, part, Vector3.zero, speed, amount, 0f);

        private void Add(Kind kind, Transform part, Vector3 axis, float speed, float amount, float phase)
        {
            // The rest pose is the part's as built, even when a second move of it is added after the first has run.
            var rest = part.localRotation;
            var scale = part.localScale;
            foreach (var move in moves) {
                if (move.Part == part) {
                    rest = move.Rest;
                    scale = move.Scale;
                    break;
                }
            }

            moves.Add(new Move {
                Kind = kind, Part = part, Rest = rest, Scale = scale, Axis = axis, Speed = speed, Amount = amount,
                Phase = phase,
            });
        }

        private void LateUpdate()
        {
            var time = Time.unscaledTime;
            Transform last = null;
            for (var i = 0; i < moves.Count; i++) {
                var move = moves[i];
                if (move.Part == null) {
                    continue;
                }

                // A part's first move sets it from its rest pose, the ones after it add to that.
                var first = move.Part != last;
                last = move.Part;
                var rotation = first ? move.Rest : move.Part.localRotation;
                switch (move.Kind) {
                    case Kind.Sway:
                        // Two waves, so the sway never repeats as a plain tick.
                        var t = time * move.Speed + move.Phase;
                        var wave = Mathf.Sin(t) * 0.75f + Mathf.Sin(t * 2.1f + 1.1f) * 0.25f;
                        move.Part.localRotation = rotation * Quaternion.AngleAxis(wave * move.Amount, move.Axis);
                        break;
                    case Kind.Spin:
                        move.Part.localRotation = rotation * Quaternion.AngleAxis(time * move.Speed, move.Axis);
                        break;
                    case Kind.Pulse:
                        move.Part.localScale = move.Scale * (1f + Mathf.Sin(time * move.Speed) * move.Amount);
                        break;
                }
            }
        }
    }
}
