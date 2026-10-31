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
            var car = new VisualElement { pickingMode = PickingMode.Ignore };
            car.AddToClassList("hud__chip-car");
            var glass = new VisualElement { pickingMode = PickingMode.Ignore };
            glass.AddToClassList("hud__chip-glass");
            car.Add(glass);
            chip.Add(car);
            return chip;
        }

        public static void MarkPlayer(VisualElement chip, bool isPlayer) =>
            chip.EnableInClassList("hud__chip--player", isPlayer);
    }
}
