using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The rank's ornaments on a level badge, as the 2.0 plates have them: silver wears a small white diamond on its top
    /// edge, gold and legend a navy crown sitting on it, and legend's fill is a rainbow instead of one colour. Rookie and
    /// bronze have none. Everything is sized from the badge's own height, so one badge reads the same over a car and in a
    /// popup. The rank itself is the <see cref="PlayerRank.ClassOf"/> class: set it with <see cref="SetRank"/>, which
    /// also repaints the ornament, when a badge already on screen moves up a rank.
    /// </summary>
    internal static class LevelBadge
    {
        private const string RankPrefix = "level-badge--";
        private const string BigClass = "level-badge--big";
        private const string FillClass = "level-badge__rainbow";
        private const string OrnamentClass = "level-badge__ornament";

        // The crown, against the badge's height: as wide as the plate is tall less a little, and a bit over half its
        // height, its base just over the top of the fill (the 2.0 plates' 16 × 10 crown on a 19 px plate, 41 × 26 on 40).
        private const float CrownWidth = 0.9f;
        private const float CrownHeight = 0.58f;
        private const float CrownSink = 0.04f;

        // On a round badge (the track's rank stop) a narrower, lower crown, its base corners on the circle.
        private const float RoundCrownWidth = 0.62f;
        private const float RoundCrownHeight = 0.42f;

        // The diamond: a square turned on its point, its middle a little over the plate's top edge, so its lower point
        // only just reaches the number. Its outline is the badge's own.
        private const float DiamondSide = 0.44f;
        private const float DiamondLift = 0.08f;

        // A thin light rim round the navy crown, so it still reads on the navy lobby rows and the HUD's dark name pill;
        // on a white card it disappears. A share of the badge's outline.
        private const float CrownRim = 0.6f;

        // The legend rainbow, left to right as the design's gradient: pink, gold, green, blue, purple.
        private static readonly Color32[] Rainbow = {
            new(0xff, 0x7a, 0xa8, 0xff), new(0xff, 0xc9, 0x3c, 0xff), new(0x7e, 0xe0, 0x8a, 0xff),
            new(0x6f, 0xc3, 0xff, 0xff), new(0xb3, 0x8b, 0xff, 0xff),
        };

        private static readonly Color32 Navy = new(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color32 White = new(0xff, 0xff, 0xff, 0xff);
        private static readonly Color32 Rim = new(0xff, 0xff, 0xff, 0xd9);

        private static Texture2D rainbowTexture;

        /// <summary>
        /// Gives <paramref name="badge"/> (a <c>level-badge</c>, or another element wearing a rank class) the legend fill
        /// and the ornament, once; what shows follows its rank class.
        /// </summary>
        public static void Decorate(VisualElement badge)
        {
            if (badge.Q(className: OrnamentClass) != null) {
                return;
            }

            // Under the number: the rainbow fills the badge inside its outline, and only legend shows it (ui_screens).
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList(FillClass);
            fill.style.backgroundImage = new StyleBackground(RainbowTexture());
            fill.RegisterCallback<GeometryChangedEvent>(OnFillGeometryChanged);
            badge.Insert(0, fill);

            // Over everything, though it draws above the badge: the crown or the diamond.
            var ornament = new VisualElement { pickingMode = PickingMode.Ignore };
            ornament.AddToClassList(OrnamentClass);
            ornament.generateVisualContent += DrawOrnament;
            badge.Add(ornament);
        }

        /// <summary>Puts <paramref name="badge"/> on the rank of <paramref name="level"/>, its ornament with it.</summary>
        public static void SetRank(VisualElement badge, int level)
        {
            var rank = PlayerRank.ClassOf(level);
            foreach (var className in new List<string>(badge.GetClasses())) {
                if (className.StartsWith(RankPrefix) && className != BigClass && className != rank
                    && className != "level-badge--rival") {
                    badge.RemoveFromClassList(className);
                }
            }

            badge.AddToClassList(rank);
            badge.Q(className: OrnamentClass)?.MarkDirtyRepaint();
        }

        /// <summary>Whether a badge of <paramref name="level"/> wears a crown (gold and legend).</summary>
        public static bool IsCrowned(int level) => PlayerRank.TierOf(level) >= 3;

        // The fill's corners follow the badge's inside, as PillShape draws it: the outline's radius less the side border.
        private static void OnFillGeometryChanged(GeometryChangedEvent evt)
        {
            var fill = (VisualElement)evt.currentTarget;
            var badge = fill.parent;
            if (badge == null) {
                return;
            }

            var style = badge.resolvedStyle;
            var size = badge.layout.size;
            var most = Mathf.Min(size.x, size.y) / 2f;
            fill.style.borderTopLeftRadius = Mathf.Max(0f, Mathf.Min(style.borderTopLeftRadius, most) - style.borderLeftWidth);
            fill.style.borderTopRightRadius = Mathf.Max(0f, Mathf.Min(style.borderTopRightRadius, most) - style.borderRightWidth);
            fill.style.borderBottomLeftRadius = Mathf.Max(0f, Mathf.Min(style.borderBottomLeftRadius, most) - style.borderLeftWidth);
            fill.style.borderBottomRightRadius = Mathf.Max(0f, Mathf.Min(style.borderBottomRightRadius, most) - style.borderRightWidth);
        }

        // The ornament element covers the badge's inside; the crown and the diamond are drawn above it, from the badge's
        // outer top edge, at sizes taken from the badge's whole height.
        private static void DrawOrnament(MeshGenerationContext context)
        {
            var ornament = context.visualElement;
            var badge = ornament.parent;
            if (badge == null) {
                return;
            }

            var isDiamond = badge.ClassListContains(RankPrefix + "silver");
            var isCrown = badge.ClassListContains(RankPrefix + "gold") || badge.ClassListContains(RankPrefix + "legend");
            if (!isDiamond && !isCrown) {
                return;
            }

            var height = badge.layout.height;
            if (height <= 0f) {
                return;
            }

            // The ornament's own origin is the inside's top left corner, under the top border.
            var style = badge.resolvedStyle;
            var border = style.borderTopWidth;
            var outline = Mathf.Max(1.5f, border);
            var middle = ornament.contentRect.width / 2f;
            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            if (isDiamond) {
                var half = DiamondSide * height * 0.7071f;
                var centre = new Vector2(middle, -border - DiamondLift * height);
                painter.BeginPath();
                painter.MoveTo(centre + new Vector2(0f, -half));
                painter.LineTo(centre + new Vector2(half, 0f));
                painter.LineTo(centre + new Vector2(0f, half));
                painter.LineTo(centre + new Vector2(-half, 0f));
                painter.ClosePath();
                painter.fillColor = White;
                painter.Fill();
                painter.strokeColor = Navy;
                painter.lineWidth = outline;
                painter.Stroke();
                return;
            }

            var isRound = style.borderTopLeftRadius >= Mathf.Min(badge.layout.width, height) / 2f;
            if (isRound) {
                // Sunk until the base's corners meet the circle, so they do not hang in the air.
                var radius = height / 2f;
                var halfBase = RoundCrownWidth * height / 2f;
                var sink = radius - Mathf.Sqrt(radius * radius - halfBase * halfBase) - border;
                DrawCrown(painter, middle, sink + CrownSink * height, RoundCrownWidth * height,
                    RoundCrownHeight * height + sink, -border + sink, CrownRim * outline);
                return;
            }

            DrawCrown(painter, middle, CrownSink * height, CrownWidth * height, CrownHeight * height, -border,
                CrownRim * outline);
        }

        // The 2.0 crown: a navy silhouette with three points, its base at baseY. The light rim runs round its sides and
        // points only, from the badge's outer top edge (topEdgeY) up, so it never lines the badge's own outline.
        private static void DrawCrown(Painter2D painter, float middle, float baseY, float width, float height,
            float topEdgeY, float rim)
        {
            var left = middle - width / 2f;
            Vector2 At(float x, float y) => new(left + x * width, baseY - (1f - y) * height);

            painter.BeginPath();
            painter.MoveTo(new Vector2(left, topEdgeY));
            painter.LineTo(At(0f, 0.15f));
            painter.LineTo(At(0.28f, 0.55f));
            painter.LineTo(At(0.5f, 0f));
            painter.LineTo(At(0.72f, 0.55f));
            painter.LineTo(At(1f, 0.15f));
            painter.LineTo(new Vector2(left + width, topEdgeY));
            painter.strokeColor = Rim;
            painter.lineWidth = rim * 2f;
            painter.lineCap = LineCap.Butt;
            painter.Stroke();

            painter.BeginPath();
            painter.MoveTo(At(0f, 1f));
            painter.LineTo(At(0f, 0.15f));
            painter.LineTo(At(0.28f, 0.55f));
            painter.LineTo(At(0.5f, 0f));
            painter.LineTo(At(0.72f, 0.55f));
            painter.LineTo(At(1f, 0.15f));
            painter.LineTo(At(1f, 1f));
            painter.ClosePath();
            painter.fillColor = Navy;
            painter.Fill();
        }

        // A small gradient made once at run time (a texture asset would add to the first load): 128 texels across,
        // mixed in sRGB as CSS mixes a linear-gradient, and read as sRGB so the project's linear colour space keeps it.
        private static Texture2D RainbowTexture()
        {
            if (rainbowTexture != null) {
                return rainbowTexture;
            }

            const int width = 128;
            rainbowTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false, false) {
                name = "Level badge rainbow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[width];
            for (var x = 0; x < width; x++) {
                var at = x / (width - 1f) * (Rainbow.Length - 1);
                var from = Mathf.Min(Mathf.FloorToInt(at), Rainbow.Length - 2);
                pixels[x] = Color32.Lerp(Rainbow[from], Rainbow[from + 1], at - from);
            }

            rainbowTexture.SetPixels32(pixels);
            rainbowTexture.Apply(false, true);
            return rainbowTexture;
        }
    }
}
