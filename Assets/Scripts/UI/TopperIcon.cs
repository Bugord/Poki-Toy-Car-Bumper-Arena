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
        private static readonly Color32 Purple = new(0x8f, 0x5c, 0xd6, 0xff);
        private static readonly Color32 Copper = new(0xc8, 0x77, 0x4a, 0xff);

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
                case "chef-hat":
                    Shape(White, new(0.3f, 0.88f), new(0.7f, 0.88f), new(0.72f, 0.6f), new(0.28f, 0.6f));
                    Circle(White, 0.32f, 0.46f, 0.18f);
                    Circle(White, 0.68f, 0.46f, 0.18f);
                    Circle(White, 0.5f, 0.32f, 0.22f);
                    break;
                case "cowboy-hat":
                    Shape(Cream, new(0.06f, 0.58f), new(0.24f, 0.72f), new(0.76f, 0.72f), new(0.94f, 0.58f),
                        new(0.86f, 0.82f), new(0.14f, 0.82f));
                    Shape(Cream, new(0.28f, 0.72f), new(0.32f, 0.26f), new(0.5f, 0.34f), new(0.68f, 0.26f),
                        new(0.72f, 0.72f));
                    Rect(Red, 0.29f, 0.6f, 0.71f, 0.7f);
                    break;
                case "viking-helmet":
                    Shape(Cream, new(0.24f, 0.62f), new(0.06f, 0.42f), new(0.08f, 0.12f), new(0.3f, 0.46f));
                    Shape(Cream, new(0.76f, 0.62f), new(0.94f, 0.42f), new(0.92f, 0.12f), new(0.7f, 0.46f));
                    Shape(Grey, new(0.22f, 0.78f), new(0.26f, 0.48f), new(0.5f, 0.3f), new(0.74f, 0.48f),
                        new(0.78f, 0.78f));
                    Rect(Gold, 0.2f, 0.74f, 0.8f, 0.86f);
                    Rect(Gold, 0.46f, 0.32f, 0.54f, 0.74f);
                    break;
                case "duck":
                    Shape(Gold, new(0.1f, 0.6f), new(0.18f, 0.44f), new(0.84f, 0.56f), new(0.8f, 0.82f),
                        new(0.2f, 0.84f));
                    Circle(Gold, 0.6f, 0.34f, 0.18f);
                    Shape(Orange, new(0.74f, 0.32f), new(0.94f, 0.36f), new(0.76f, 0.44f));
                    Circle(Navy, 0.64f, 0.29f, 0.04f);
                    break;
                case "halo":
                    Shape(White, new(0.46f, 0.82f), new(0.08f, 0.62f), new(0.12f, 0.46f), new(0.46f, 0.62f));
                    Shape(White, new(0.54f, 0.82f), new(0.92f, 0.62f), new(0.88f, 0.46f), new(0.54f, 0.62f));
                    Shape(Gold, new(0.18f, 0.28f), new(0.5f, 0.14f), new(0.82f, 0.28f), new(0.5f, 0.42f));
                    Shape(Sky, new(0.32f, 0.28f), new(0.5f, 0.21f), new(0.68f, 0.28f), new(0.5f, 0.35f));
                    break;
                case "rocket":
                    Shape(Orange, new(0.42f, 0.8f), new(0.58f, 0.8f), new(0.5f, 0.94f));
                    Shape(Red, new(0.34f, 0.8f), new(0.2f, 0.84f), new(0.22f, 0.64f), new(0.36f, 0.56f));
                    Shape(Red, new(0.66f, 0.8f), new(0.8f, 0.84f), new(0.78f, 0.64f), new(0.64f, 0.56f));
                    Rect(White, 0.36f, 0.36f, 0.64f, 0.8f);
                    Shape(Red, new(0.36f, 0.36f), new(0.5f, 0.08f), new(0.64f, 0.36f));
                    Circle(Sky, 0.5f, 0.52f, 0.07f);
                    break;
                case "wizard-hat":
                    Rect(Purple, 0.08f, 0.8f, 0.92f, 0.9f);
                    Shape(Purple, new(0.28f, 0.8f), new(0.46f, 0.3f), new(0.7f, 0.08f), new(0.6f, 0.36f), new(0.72f, 0.8f));
                    Rect(Gold, 0.28f, 0.68f, 0.72f, 0.8f);
                    Circle(Gold, 0.7f, 0.1f, 0.07f);
                    Circle(Gold, 0.52f, 0.52f, 0.05f);
                    break;
                case "ember":
                    // A Spartan's bronze helmet under its red crest.
                    Shape(Red, new(0.26f, 0.4f), new(0.32f, 0.16f), new(0.5f, 0.06f), new(0.7f, 0.12f),
                        new(0.8f, 0.36f), new(0.64f, 0.28f), new(0.44f, 0.28f));
                    Shape(Copper, new(0.2f, 0.84f), new(0.2f, 0.5f), new(0.28f, 0.36f), new(0.5f, 0.28f),
                        new(0.72f, 0.36f), new(0.8f, 0.5f), new(0.8f, 0.84f));
                    Rect(Dark, 0.32f, 0.5f, 0.68f, 0.58f);
                    Rect(Dark, 0.45f, 0.5f, 0.55f, 0.84f);
                    Rect(Gold, 0.18f, 0.8f, 0.82f, 0.9f);
                    break;
                case "silver-torch":
                    // A knight's steel great helm with an eye slit, a gold cross and a plume.
                    Shape(Blue, new(0.5f, 0.3f), new(0.22f, 0.16f), new(0.3f, 0.08f));
                    Shape(White, new(0.5f, 0.3f), new(0.38f, 0.04f), new(0.52f, 0.04f));
                    Shape(Sky, new(0.5f, 0.3f), new(0.64f, 0.06f), new(0.74f, 0.14f));
                    Shape(Grey, new(0.24f, 0.86f), new(0.24f, 0.42f), new(0.32f, 0.3f), new(0.5f, 0.26f),
                        new(0.68f, 0.3f), new(0.76f, 0.42f), new(0.76f, 0.86f));
                    Rect(Dark, 0.3f, 0.5f, 0.7f, 0.56f);
                    Rect(Gold, 0.47f, 0.32f, 0.53f, 0.82f);
                    Rect(Gold, 0.36f, 0.62f, 0.64f, 0.67f);
                    Rect(Gold, 0.22f, 0.8f, 0.78f, 0.88f);
                    break;
                case "fire-crown":
                    // A samurai's gold kabuto: a red neck guard, tall gold horns and a red sun.
                    Shape(Red, new(0.08f, 0.88f), new(0.2f, 0.6f), new(0.8f, 0.6f), new(0.92f, 0.88f));
                    Rect(Gold, 0.12f, 0.76f, 0.88f, 0.8f);
                    Shape(Gold, new(0.24f, 0.64f), new(0.3f, 0.44f), new(0.5f, 0.36f), new(0.7f, 0.44f), new(0.76f, 0.64f));
                    Shape(Gold, new(0.44f, 0.54f), new(0.18f, 0.12f), new(0.26f, 0.08f), new(0.5f, 0.48f));
                    Shape(Gold, new(0.56f, 0.54f), new(0.82f, 0.12f), new(0.74f, 0.08f), new(0.5f, 0.48f));
                    Circle(Red, 0.5f, 0.54f, 0.07f);
                    break;
                case "phoenix":
                    // An astronaut's helmet with a rainbow visor and a gold ring round it.
                    Rect(Gold, 0.3f, 0.8f, 0.7f, 0.9f);
                    Circle(White, 0.5f, 0.5f, 0.3f);
                    Circle(Purple, 0.53f, 0.55f, 0.17f);
                    Circle(Pink, 0.49f, 0.5f, 0.08f);
                    Shape(Gold, new(0.04f, 0.66f), new(0.96f, 0.44f), new(0.96f, 0.5f), new(0.04f, 0.72f));
                    Circle(Gold, 0.82f, 0.16f, 0.05f);
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
