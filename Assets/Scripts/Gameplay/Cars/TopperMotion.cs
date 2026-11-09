using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Moves the parts of a topper that live (the league hats' flames, sparks and wings), built with it by
    /// <see cref="CarTopper.Build"/>. It sits on the topper's root and moves only its own children, so the topper lives
    /// wherever it is shown: on the arena, in the garage, on the podium's copy (Instantiate carries the serialized parts
    /// over to the copy's own children) and in the result's popups. Unscaled time: the hit-stop and the ad pause do not
    /// freeze a flame. Nothing is allocated a frame.
    /// </summary>
    public sealed class TopperMotion : MonoBehaviour
    {
        [Serializable]
        private struct Flame
        {
            public Transform Part;
            public Vector3 Scale;
            public float Speed;
            public float Phase;
            public float Amount;
        }

        [Serializable]
        private struct Spark
        {
            public Transform Part;
            public Vector3 From;
            public Vector3 Drift;
            public float Size;
            public float Period;
            public float Phase;
        }

        [Serializable]
        private struct Wing
        {
            public Transform Part;
            public Quaternion Rest;
            public Vector3 Axis;
            public float Speed;
            public float Angle;
        }

        [SerializeField]
        private List<Flame> flames = new();

        [SerializeField]
        private List<Spark> sparks = new();

        [SerializeField]
        private List<Wing> wings = new();

        /// <summary>
        /// A flame that flickers: it stretches up and squeezes in round its foot by up to <paramref name="amount"/> of
        /// its size, <paramref name="speed"/> radians a second; flames out of step have other phases.
        /// </summary>
        public void AddFlame(Transform part, float speed, float phase, float amount) =>
            flames.Add(new Flame { Part = part, Scale = part.localScale, Speed = speed, Phase = phase, Amount = amount });

        /// <summary>
        /// A spark that rises from where it is by <paramref name="drift"/> over <paramref name="period"/> seconds,
        /// shrinking to nothing, then starts again from where it was built.
        /// </summary>
        public void AddSpark(Transform part, Vector3 drift, float period, float phase)
        {
            sparks.Add(new Spark {
                Part = part, From = part.localPosition, Drift = drift, Size = part.localScale.x, Period = period,
                Phase = phase,
            });
        }

        /// <summary>A wing that beats round <paramref name="axis"/> (its own), up to <paramref name="angle"/> degrees.</summary>
        public void AddWing(Transform part, Vector3 axis, float speed, float angle) =>
            wings.Add(new Wing { Part = part, Rest = part.localRotation, Axis = axis, Speed = speed, Angle = angle });

        private void LateUpdate()
        {
            var time = Time.unscaledTime;
            for (var i = 0; i < flames.Count; i++) {
                var flame = flames[i];
                if (flame.Part == null) {
                    continue;
                }

                var t = time * flame.Speed + flame.Phase;
                // Two waves, so the flicker never repeats as a plain pulse.
                var wave = Mathf.Sin(t) * 0.65f + Mathf.Sin(t * 2.3f + 1.7f) * 0.35f;
                var up = 1f + wave * flame.Amount;
                var round = 1f - wave * flame.Amount * 0.45f;
                flame.Part.localScale = new Vector3(flame.Scale.x * round, flame.Scale.y * up, flame.Scale.z * round);
            }

            for (var i = 0; i < sparks.Count; i++) {
                var spark = sparks[i];
                if (spark.Part == null) {
                    continue;
                }

                var age = Mathf.Repeat(time + spark.Phase, spark.Period) / spark.Period;
                spark.Part.localPosition = spark.From + spark.Drift * age;
                spark.Part.localScale = Vector3.one * (spark.Size * (1f - age));
            }

            for (var i = 0; i < wings.Count; i++) {
                var wing = wings[i];
                if (wing.Part == null) {
                    continue;
                }

                // A quick downstroke and a slower lift, as a bird beats.
                var beat = Mathf.Sin(time * wing.Speed);
                beat = beat > 0f ? beat : beat * 0.6f;
                wing.Part.localRotation = wing.Rest * Quaternion.AngleAxis(beat * wing.Angle, wing.Axis);
            }
        }
    }
}
