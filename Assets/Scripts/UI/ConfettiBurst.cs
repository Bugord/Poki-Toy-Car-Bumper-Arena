using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Confetti for a win: outlined paper pieces burst up out of the title, then flutter down over the whole screen and
    /// shrink away. The pieces are built once and moved by the code on unscaled time, so a hit-stop does not freeze them.
    /// </summary>
    public sealed class ConfettiBurst
    {
        private const int Count = 44;
        private const float Duration = 2.6f;
        private const float Gravity = 2600f;
        private const float Drag = 1.6f;
        private const float ShrinkTime = 0.35f;

        private static readonly Color[] Colors = {
            new(1f, 0.302f, 0.302f),
            new(0.22f, 0.714f, 1f),
            new(0.357f, 0.827f, 0.29f),
            new(1f, 0.624f, 0.11f),
            new(0.69f, 0.424f, 1f),
            new(1f, 0.788f, 0.235f),
        };

        private readonly VisualElement layer;
        private readonly Piece[] pieces = new Piece[Count];

        public ConfettiBurst(VisualElement root)
        {
            layer = new VisualElement { name = "confetti", pickingMode = PickingMode.Ignore };
            layer.AddToClassList("confetti-layer");
            root.Add(layer);
            for (var i = 0; i < Count; i++) {
                var element = new VisualElement { pickingMode = PickingMode.Ignore };
                element.AddToClassList("confetti");
                element.style.backgroundColor = Colors[i % Colors.Length];
                layer.Add(element);
                pieces[i] = new Piece { Element = element };
            }
        }

        /// <summary>Bursts from the centre of <paramref name="source"/> once it is laid out.</summary>
        public async UniTask Play(VisualElement source, CancellationToken token)
        {
            await UniTask.WaitUntil(() => source.panel != null && source.worldBound.width > 0f, cancellationToken: token);
            Launch(layer.WorldToLocal(source.worldBound.center), source.worldBound.width * 0.5f);
            layer.style.display = DisplayStyle.Flex;
            for (var time = 0f; time < Duration; time += Time.unscaledDeltaTime) {
                Step(time, Mathf.Min(Time.unscaledDeltaTime, 0.05f));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            layer.style.display = DisplayStyle.None;
        }

        private void Launch(Vector2 origin, float spread)
        {
            for (var i = 0; i < Count; i++) {
                ref var piece = ref pieces[i];
                piece.Position = origin + new Vector2(Random.Range(-spread, spread), Random.Range(-40f, 40f));
                piece.Velocity = new Vector2(Random.Range(-900f, 900f), Random.Range(-1900f, -700f));
                piece.Angle = Random.Range(0f, 360f);
                piece.Spin = Random.Range(-720f, 720f);
                piece.Flutter = Random.Range(0f, Mathf.PI * 2f);
                piece.Scale = Random.Range(0.7f, 1.2f);
            }
        }

        private void Step(float time, float delta)
        {
            var shrink = Mathf.Clamp01((Duration - time) / ShrinkTime);
            for (var i = 0; i < Count; i++) {
                ref var piece = ref pieces[i];
                piece.Velocity.y += Gravity * delta;
                piece.Velocity -= piece.Velocity * (Drag * delta);
                piece.Position += piece.Velocity * delta;
                piece.Angle += piece.Spin * delta;
                // Paper turning over as it falls: its width swings, its height holds.
                var turn = Mathf.Cos(time * 9f + piece.Flutter);
                var style = piece.Element.style;
                style.translate = new Translate(piece.Position.x, piece.Position.y);
                style.rotate = new Rotate(piece.Angle);
                style.scale = new Scale(new Vector2(piece.Scale * turn * shrink, piece.Scale * shrink));
            }
        }

        private struct Piece
        {
            public VisualElement Element;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Angle;
            public float Spin;
            public float Flutter;
            public float Scale;
        }
    }
}
