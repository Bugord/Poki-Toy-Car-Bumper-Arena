using ToyCarArena.Gameplay.Cars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The USS class that paints an element in a car's body colour (car-color--N in ui_screens), so the HUD's chips and
    /// the result screen's PUSHED OFF BY badge show the car the player sees on the arena.
    /// </summary>
    public static class CarColor
    {
        private static readonly string[] Classes =
        {
            "car-color--0", "car-color--1", "car-color--2", "car-color--3",
            "car-color--4", "car-color--5", "car-color--6", "car-color--7",
        };

        // Exact sRGB values of --car-* in ui_theme.tss, used for portrait tint interpolation too.
        private static readonly Color32[] Tints =
        {
            new(0x3f, 0xa8, 0x7a, 255), new(0xe6, 0x5f, 0x48, 255),
            new(0x5f, 0x74, 0xcb, 255), new(0xff, 0xb5, 0x4a, 255),
            new(0x7e, 0x56, 0xce, 255), new(0xff, 0x86, 0x44, 255),
            new(0xca, 0x77, 0xeb, 255), new(0x3f, 0x3f, 0x46, 255),
        };

        public static Color TintOf(int colorId) => Tints[CarPaint.OrGreen(colorId)];
        public static Color Silhouette => new Color32(0x1d, 0x2b, 0x4f, 255);

        /// <summary>
        /// The class of a colour ID, independent of slot, nickname or player marker.
        /// </summary>
        public static string ClassOf(int colorId) => Classes[CarPaint.OrGreen(colorId)];

        /// <summary>The same car sticker in the arena HUD and the curtain's lobby.</summary>
        public static VisualElement CreateChip(int colorId, bool isPlayer)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("hud__chip");
            chip.AddToClassList(ClassOf(colorId));
            MarkPlayer(chip, isPlayer);
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("hud__chip-ring");
            chip.Add(ring);
            // A toy car from the side: the top-down box it was read as a phone or a domino.
            var car = new VisualElement { pickingMode = PickingMode.Ignore };
            car.AddToClassList("hud__chip-car");
            car.generateVisualContent += DrawCar;
            chip.Add(car);
            return chip;
        }

        // White body and cabin with a navy rim, a navy window and two navy wheels with white hubs; facing right.
        private static void DrawCar(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            if (r.width <= 0f || r.height <= 0f) {
                return;
            }

            Vector2 At(float x, float y) => new(r.x + x * r.width, r.y + y * r.height);
            var p = context.painter2D;
            var navy = Silhouette;
            p.lineJoin = LineJoin.Round;
            p.lineWidth = r.width * 0.09f;
            p.strokeColor = navy;
            p.fillColor = Color.white;
            p.BeginPath();
            p.MoveTo(At(0.06f, 0.76f));
            p.LineTo(At(0.06f, 0.5f));
            p.LineTo(At(0.24f, 0.44f));
            p.LineTo(At(0.34f, 0.14f));
            p.LineTo(At(0.68f, 0.14f));
            p.LineTo(At(0.8f, 0.44f));
            p.LineTo(At(0.94f, 0.5f));
            p.LineTo(At(0.94f, 0.76f));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            p.fillColor = navy;
            p.BeginPath();
            p.MoveTo(At(0.4f, 0.27f));
            p.LineTo(At(0.62f, 0.27f));
            p.LineTo(At(0.68f, 0.44f));
            p.LineTo(At(0.34f, 0.44f));
            p.ClosePath();
            p.Fill();

            foreach (var x in new[] { 0.28f, 0.72f }) {
                p.fillColor = navy;
                p.BeginPath();
                p.Arc(At(x, 0.78f), r.width * 0.15f, 0f, 360f);
                p.Fill();
                p.fillColor = Color.white;
                p.BeginPath();
                p.Arc(At(x, 0.78f), r.width * 0.055f, 0f, 360f);
                p.Fill();
            }
        }

        public static void MarkPlayer(VisualElement chip, bool isPlayer) =>
            chip.EnableInClassList("hud__chip--player", isPlayer);
    }
}
