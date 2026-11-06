using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Gives each league a sticker silhouette: plain rookie, bevelled bronze shield, winged silver, crowned gold
    /// laurels and a star crest with gemmed wings for rainbow legend. Ornaments grow outside the unchanged layout box,
    /// with a light rim on dark name pills and smaller growth on large badges. Round track stops keep their circular
    /// face. The frame follows the <see cref="PlayerRank.ClassOf"/> class; <see cref="SetRank"/> also repaints it and
    /// moves a rival's horns when a badge already on screen changes league.
    /// </summary>
    internal static class LevelBadge
    {
        private const string RankPrefix = "level-badge--";
        private const string BigClass = "level-badge--big";
        private const string FillClass = "level-badge__rainbow";
        private const string OrnamentClass = "level-badge__ornament";

        // Side ornaments stay within the existing gaps before names; height carries most of the league silhouette.
        // The rim traces exposed edges only, so it cannot draw a white seam through the sticker face.
        private const float OrnamentRim = 0.45f;
        private static readonly string[] DarkBackdrops = { "lobby__level", "car-name__level" };

        // Ornaments grow with the badge up to this height and then slower, so a big badge (the result card's, the
        // level-up popup's) keeps a crown that crowns it without reaching the stickers above.
        private const float FullGrowthHeight = 50f;
        private const float BigGrowth = 0.3f;

        // The legend rainbow, left to right as the design's gradient: pink, gold, green, blue, purple.
        private static readonly Color32[] Rainbow = {
            new(0xff, 0x7a, 0xa8, 0xff), new(0xff, 0xc9, 0x3c, 0xff), new(0x7e, 0xe0, 0x8a, 0xff),
            new(0x6f, 0xc3, 0xff, 0xff), new(0xb3, 0x8b, 0xff, 0xff),
        };

        private static readonly Color32 Navy = new(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color32 White = new(0xff, 0xff, 0xff, 0xff);
        private static readonly Color32 Bronze = new(0xe8, 0xa4, 0x6a, 0xff);
        private static readonly Color32 Silver = new(0xd7, 0xe0, 0xee, 0xff);
        private static readonly Color32 Rival = new(0xff, 0x4d, 0x4d, 0xff);
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

            // Above the horns, but clear of the number: only the frame and its outside silhouette are painted here.
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
            foreach (var child in badge.Children()) {
                if (child.ClassListContains("level-badge__horn")) {
                    child.EnableInClassList("level-badge__horn--crowned", IsCrowned(level));
                }
            }

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

        // All coordinates are relative to the ornament's inside origin. Neither painting nor rank changes move text.
        private static void DrawOrnament(MeshGenerationContext context)
        {
            var ornament = context.visualElement;
            var badge = ornament.parent;
            if (badge == null) {
                return;
            }

            var tier = badge.ClassListContains(RankPrefix + "legend") ? 4
                : badge.ClassListContains(RankPrefix + "gold") ? 3
                : badge.ClassListContains(RankPrefix + "silver") ? 2
                : badge.ClassListContains(RankPrefix + "bronze") ? 1 : 0;
            if (tier == 0) {
                return;
            }

            var badgeHeight = badge.layout.height;
            if (badgeHeight <= 0f) {
                return;
            }

            var height = Mathf.Min(badgeHeight, FullGrowthHeight + (badgeHeight - FullGrowthHeight) * BigGrowth);

            var style = badge.resolvedStyle;
            var border = style.borderTopWidth;
            var outline = Mathf.Max(1.5f, Mathf.Min(border, height * 0.095f));
            var middle = ornament.contentRect.width / 2f;
            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            var rim = 0f;
            foreach (var backdrop in DarkBackdrops) {
                if (badge.ClassListContains(backdrop)) {
                    rim = OrnamentRim * outline;
                }
            }

            // PillShape can resolve a round stop to a radius smaller than half its box because of its bottom shadow.
            var isRound = badge.ClassListContains("track__node")
                || style.borderTopLeftRadius >= Mathf.Min(badge.layout.width, badgeHeight) / 2f;
            var faceHeight = badgeHeight - Mathf.Max(0f, style.borderBottomWidth - border);
            var centreY = -border + faceHeight / 2f;
            var inset = isRound ? height * 0.1f : 0f;
            var isRival = badge.ClassListContains(RankPrefix + "rival");
            Color colour = isRival ? Rival : tier == 1 ? Bronze : tier == 2 ? Silver
                : tier == 3 ? style.backgroundColor : Rainbow[1];

            DrawSide(painter, new Vector2(-style.borderLeftWidth + inset, centreY), height, -1f,
                tier, colour, outline, rim);
            DrawSide(painter, new Vector2(ornament.contentRect.width + style.borderRightWidth - inset, centreY),
                height, 1f, tier, colour, outline, rim);

            if (tier == 1) {
                if (!isRound) {
                    // A short shield toe changes the bronze silhouette without taking space from the level number.
                    var bottom = -border + faceHeight - outline * 0.45f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(middle - height * 0.22f, bottom));
                    painter.LineTo(new Vector2(middle, bottom + height * 0.15f));
                    painter.LineTo(new Vector2(middle + height * 0.22f, bottom));
                    FinishShape(painter, colour, outline, rim);
                }
            } else {
                var crestWidth = height * (tier == 2 ? 0.38f : tier == 3 ? 0.72f : 0.78f);
                var sink = outline * 0.35f;
                if (isRound) {
                    var radius = faceHeight / 2f;
                    var halfBase = crestWidth / 2f;
                    sink += radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - halfBase * halfBase));
                }

                DrawCrest(painter, new Vector2(middle, -border + sink), height, crestWidth, tier,
                    tier == 2 ? (Color)Silver : tier == 3 ? style.backgroundColor : (Color)Rainbow[1], outline, rim);
            }
        }

        // Swept wings use broad, rounded feather lobes so their silhouette stays clear at phone size.
        // Their open roots sit on the badge edge, keeping the closing stroke in its outline and the light rim outside.
        private static void DrawSide(Painter2D painter, Vector2 root, float size, float direction, int tier,
            Color colour, float outline, float rim)
        {
            Vector2 At(float x, float y) => root + new Vector2(direction * x * size, y * size);
            painter.BeginPath();
            if (tier == 1) {
                painter.MoveTo(At(-0.025f, -0.29f));
                painter.LineTo(At(0.09f, -0.18f));
                painter.LineTo(At(0.09f, 0.18f));
                painter.LineTo(At(-0.025f, 0.29f));
            } else if (tier == 2) {
                painter.MoveTo(At(0f, -0.12f));
                painter.BezierCurveTo(At(0.04f, -0.17f), At(0.08f, -0.27f), At(0.13f, -0.34f));
                painter.BezierCurveTo(At(0.19f, -0.424f), At(0.21f, -0.22f), At(0.15f, -0.17f));
                painter.BezierCurveTo(At(0.12f, -0.145f), At(0.12f, -0.12f), At(0.15f, -0.10f));
                painter.BezierCurveTo(At(0.23f, -0.047f), At(0.17f, 0.10f), At(0.09f, 0.18f));
                painter.BezierCurveTo(At(0.05f, 0.22f), At(0.01f, 0.25f), At(0f, 0.28f));
            } else if (tier == 3) {
                painter.MoveTo(At(0f, -0.15f));
                painter.BezierCurveTo(At(0.04f, -0.22f), At(0.09f, -0.32f), At(0.14f, -0.39f));
                painter.BezierCurveTo(At(0.20f, -0.474f), At(0.22f, -0.29f), At(0.16f, -0.23f));
                painter.BezierCurveTo(At(0.13f, -0.20f), At(0.13f, -0.18f), At(0.16f, -0.16f));
                painter.BezierCurveTo(At(0.24f, -0.107f), At(0.20f, -0.025f), At(0.15f, 0.005f));
                painter.BezierCurveTo(At(0.12f, 0.023f), At(0.12f, 0.045f), At(0.15f, 0.065f));
                painter.BezierCurveTo(At(0.23f, 0.118f), At(0.16f, 0.22f), At(0.08f, 0.28f));
                painter.BezierCurveTo(At(0.04f, 0.31f), At(0.01f, 0.33f), At(0f, 0.36f));
            } else {
                painter.MoveTo(At(0f, -0.18f));
                painter.BezierCurveTo(At(0.05f, -0.25f), At(0.10f, -0.35f), At(0.15f, -0.42f));
                painter.BezierCurveTo(At(0.21f, -0.504f), At(0.24f, -0.31f), At(0.17f, -0.24f));
                painter.BezierCurveTo(At(0.14f, -0.21f), At(0.14f, -0.18f), At(0.17f, -0.16f));
                painter.BezierCurveTo(At(0.25f, -0.107f), At(0.22f, -0.015f), At(0.16f, 0.025f));
                painter.BezierCurveTo(At(0.13f, 0.045f), At(0.13f, 0.065f), At(0.16f, 0.085f));
                painter.BezierCurveTo(At(0.24f, 0.138f), At(0.17f, 0.25f), At(0.09f, 0.31f));
                painter.BezierCurveTo(At(0.05f, 0.34f), At(0.01f, 0.37f), At(0f, 0.40f));
            }

            FinishShape(painter, colour, outline, rim);

            if (tier == 4) {
                // A single gem distinguishes legend without another outline cutting across its wing.
                DrawGem(painter, At(0.09f, -0.10f), size * 0.055f, White);
            }
        }

        private static void DrawCrest(Painter2D painter, Vector2 root, float size, float width, int tier,
            Color colour, float outline, float rim)
        {
            Vector2 At(float x, float y) => root + new Vector2((x - 0.5f) * width, -y * size);
            painter.BeginPath();
            painter.MoveTo(At(0f, 0f));
            if (tier == 2) {
                painter.LineTo(At(0f, 0.12f));
                painter.LineTo(At(0.5f, 0.33f));
                painter.LineTo(At(1f, 0.12f));
            } else if (tier == 3) {
                painter.LineTo(At(0f, 0.31f));
                painter.LineTo(At(0.28f, 0.18f));
                painter.LineTo(At(0.5f, 0.43f));
                painter.LineTo(At(0.72f, 0.18f));
                painter.LineTo(At(1f, 0.31f));
            } else {
                painter.LineTo(At(0.07f, 0.20f));
                painter.LineTo(At(0.31f, 0.10f));
                painter.LineTo(At(0.69f, 0.10f));
                painter.LineTo(At(0.93f, 0.20f));
            }

            painter.LineTo(At(1f, 0f));
            FinishShape(painter, colour, outline, rim);

            if (tier == 2) {
                DrawGem(painter, root + new Vector2(0f, -size * 0.14f), size * 0.07f, White);
            } else if (tier == 3) {
                DrawGem(painter, root + new Vector2(0f, -size * 0.16f), size * 0.075f, White);
            } else {
                // The star replaces the flat crown silhouette; its lower points overlap the crown band.
                var centre = root + new Vector2(0f, -size * 0.27f);
                for (var i = 0; i < 10; i++) {
                    var angle = (-90f + i * 36f) * Mathf.Deg2Rad;
                    var radius = size * (i % 2 == 0 ? 0.29f : 0.14f);
                    var point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (i == 0) {
                        painter.BeginPath();
                        painter.MoveTo(point);
                    } else {
                        painter.LineTo(point);
                    }
                }

                painter.ClosePath();
                FinishShape(painter, colour, outline, rim);
                DrawGem(painter, centre, size * 0.085f, White);
            }
        }

        private static void DrawGem(Painter2D painter, Vector2 centre, float radius, Color colour)
        {
            painter.BeginPath();
            painter.MoveTo(centre + new Vector2(0f, -radius));
            painter.LineTo(centre + new Vector2(radius, 0f));
            painter.LineTo(centre + new Vector2(0f, radius));
            painter.LineTo(centre + new Vector2(-radius, 0f));
            painter.ClosePath();
            painter.fillColor = colour;
            painter.Fill();
        }

        // Call with an open root edge, except for a floating star, so the light rim stays outside the face.
        private static void FinishShape(Painter2D painter, Color colour, float outline, float rim)
        {
            if (rim > 0f) {
                painter.strokeColor = Rim;
                painter.lineWidth = outline + rim * 2f;
                painter.Stroke();
            }

            painter.ClosePath();
            painter.fillColor = colour;
            painter.Fill();
            painter.strokeColor = Navy;
            painter.lineWidth = outline;
            painter.Stroke();
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
