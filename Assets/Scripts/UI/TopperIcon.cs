using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A topper drawn flat for the garage, in the colours of the swatches <c>CarTopper</c> paints it with and the navy
    /// outline of the stickers: the cards and the preview show these, since the car portraits are pictures. An empty id
    /// draws "none", a crossed-out ring.
    /// </summary>
    internal static class TopperIcon
    {
        private static readonly Color32 Navy = new(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color32 White = new(0xde, 0xde, 0xea, 0xff);
        private static readonly Color32 Dark = new(0x3f, 0x3f, 0x46, 0xff);
        private static readonly Color32 Grey = new(0x9c, 0xa4, 0xc4, 0xff);
        private static readonly Color32 Cream = new(0xf8, 0xd7, 0xaf, 0xff);
        private static readonly Color32 Gold = new(0xff, 0xb5, 0x4a, 0xff);
        private static readonly Color32 Orange = new(0xff, 0x86, 0x44, 0xff);
        private static readonly Color32 Red = new(0xe6, 0x5f, 0x48, 0xff);
        private static readonly Color32 Blue = new(0x5f, 0x74, 0xcb, 0xff);
        private static readonly Color32 Sky = new(0x98, 0xbc, 0xeb, 0xff);
        private static readonly Color32 Pink = new(0xca, 0x77, 0xeb, 0xff);
        private static readonly Color32 Green = new(0x3f, 0xa8, 0x7a, 0xff);

        public static VisualElement Create(string id, string className)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("topper-icon");
            icon.AddToClassList(className);
            Set(icon, id);
            return icon;
        }

        /// <summary>Redraws <paramref name="icon"/> as another topper.</summary>
        public static void Set(VisualElement icon, string id)
        {
            icon.userData = id ?? "";
            icon.generateVisualContent -= Draw;
            icon.generateVisualContent += Draw;
            icon.MarkDirtyRepaint();
        }

        private static void Draw(MeshGenerationContext context)
        {
            var element = context.visualElement;
            var rect = element.contentRect;
            // A square in the middle of the element, its units 0..1 from the top left.
            var size = Mathf.Min(rect.width, rect.height);
            var origin = new Vector2((rect.width - size) * 0.5f, (rect.height - size) * 0.5f);
            var painter = context.painter2D;
            var line = Mathf.Max(3f, size * 0.05f);
            Vector2 P(float x, float y) => origin + new Vector2(x, y) * size;

            void Shape(Color32 fill, params Vector2[] points)
            {
                painter.BeginPath();
                painter.MoveTo(P(points[0].x, points[0].y));
                for (var i = 1; i < points.Length; i++) {
                    painter.LineTo(P(points[i].x, points[i].y));
                }

                painter.ClosePath();
                FillAndStroke(fill);
            }

            void Circle(Color32 fill, float x, float y, float radius)
            {
                painter.BeginPath();
                painter.Arc(P(x, y), radius * size, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.ClosePath();
                FillAndStroke(fill);
            }

            void FillAndStroke(Color32 fill)
            {
                painter.fillColor = fill;
                painter.Fill();
                painter.strokeColor = Navy;
                painter.lineWidth = line;
                painter.lineJoin = LineJoin.Round;
                painter.Stroke();
            }

            void Rect(Color32 fill, float x0, float y0, float x1, float y1) =>
                Shape(fill, new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1));

            switch (element.userData as string) {
                case "top-hat":
                    Rect(Dark, 0.1f, 0.78f, 0.9f, 0.88f);
                    Shape(Dark, new(0.27f, 0.12f), new(0.73f, 0.12f), new(0.69f, 0.78f), new(0.31f, 0.78f));
                    Rect(Red, 0.3f, 0.6f, 0.7f, 0.72f);
                    break;
                case "party-hat":
                    Shape(Pink, new(0.5f, 0.14f), new(0.78f, 0.88f), new(0.22f, 0.88f));
                    Shape(Gold, new(0.33f, 0.62f), new(0.67f, 0.62f), new(0.72f, 0.75f), new(0.28f, 0.75f));
                    Circle(Gold, 0.5f, 0.14f, 0.08f);
                    break;
                case "cone":
                    Rect(Dark, 0.18f, 0.8f, 0.82f, 0.9f);
                    Shape(Orange, new(0.43f, 0.1f), new(0.57f, 0.1f), new(0.74f, 0.8f), new(0.26f, 0.8f));
                    Shape(White, new(0.37f, 0.38f), new(0.63f, 0.38f), new(0.67f, 0.54f), new(0.33f, 0.54f));
                    break;
                case "antenna":
                    Rect(Grey, 0.47f, 0.24f, 0.53f, 0.82f);
                    Rect(Dark, 0.36f, 0.8f, 0.64f, 0.9f);
                    Circle(Red, 0.5f, 0.2f, 0.11f);
                    break;
                case "flag":
                    Rect(White, 0.3f, 0.14f, 0.36f, 0.9f);
                    Shape(Red, new(0.36f, 0.18f), new(0.8f, 0.26f), new(0.76f, 0.44f), new(0.36f, 0.5f));
                    Circle(Gold, 0.33f, 0.12f, 0.06f);
                    break;
                case "horns":
                    Shape(Cream, new(0.2f, 0.86f), new(0.1f, 0.5f), new(0.12f, 0.12f), new(0.34f, 0.44f), new(0.44f, 0.86f));
                    Shape(Cream, new(0.8f, 0.86f), new(0.9f, 0.5f), new(0.88f, 0.12f), new(0.66f, 0.44f), new(0.56f, 0.86f));
                    break;
                case "propeller":
                    painter.BeginPath();
                    painter.Arc(P(0.5f, 0.86f), 0.3f * size, Angle.Degrees(180f), Angle.Degrees(360f));
                    painter.ClosePath();
                    FillAndStroke(Blue);
                    Rect(Gold, 0.46f, 0.36f, 0.54f, 0.56f);
                    Rect(Red, 0.1f, 0.28f, 0.9f, 0.38f);
                    Circle(Gold, 0.5f, 0.33f, 0.06f);
                    break;
                case "fin":
                    Shape(Sky, new(0.16f, 0.86f), new(0.4f, 0.5f), new(0.62f, 0.12f), new(0.66f, 0.46f), new(0.86f, 0.86f));
                    break;
                case "crown":
                    Shape(Gold, new(0.12f, 0.82f), new(0.12f, 0.3f), new(0.31f, 0.55f), new(0.5f, 0.18f),
                        new(0.69f, 0.55f), new(0.88f, 0.3f), new(0.88f, 0.82f));
                    Circle(Red, 0.5f, 0.68f, 0.07f);
                    Circle(Blue, 0.27f, 0.7f, 0.05f);
                    Circle(Green, 0.73f, 0.7f, 0.05f);
                    break;
                default:
                    painter.BeginPath();
                    painter.Arc(P(0.5f, 0.5f), 0.3f * size, Angle.Degrees(0f), Angle.Degrees(360f));
                    painter.strokeColor = Grey;
                    painter.lineWidth = line * 2f;
                    painter.Stroke();
                    painter.BeginPath();
                    painter.MoveTo(P(0.29f, 0.71f));
                    painter.LineTo(P(0.71f, 0.29f));
                    painter.Stroke();
                    break;
            }
        }
    }
}
