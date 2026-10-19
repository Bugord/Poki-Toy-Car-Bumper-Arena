using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Gives stickers the design's shape. Pills get round ends: CSS shrinks a radius too big for its box by one factor
    /// on both axes, so 999px makes a pill; UI Toolkit clamps each axis on its own (half the width across, half the
    /// height down), which draws an ellipse. An element styled with a radius of <see cref="PillRadius"/> or more gets
    /// half its height less its drop (or half its width, if that is shorter) instead, set again whenever its size
    /// changes. And the drop: the design's <c>box-shadow: 0 Npx 0 navy</c> is a bottom border thicker than the rest
    /// here, which keeps the outline but squashes the inside's bottom corners (their radius less the bottom border on
    /// the vertical axis, less the side border across), so the fill looked flattened and the drop seemed to cut into
    /// it. An element with a drop redraws its outline and drop and then its fill over UI Toolkit's own, with the bottom
    /// corners of the fill round, as the design's fill inside an even outline over its drop; its text and children still
    /// draw over that. An element with a background image or a translucent fill or border keeps UI Toolkit's shape.
    /// Pressed, a sticker sinks onto its drop without a change to its layout (UI Toolkit rounds every border and margin
    /// to whole pixels on its own, so trading the drop's border for as much margin moved what came after it by a pixel
    /// or two): its USS hides its border and fill (transparent, as UI Toolkit draws the fill under the border too) and
    /// translates it down by its drop, and this draws it without the drop, in the colours it had.
    /// Copied from SMUP, whose stickers these are; both games now need it, so it belongs in the kit.
    /// </summary>
    public static class PillShape
    {
        /// <summary>The USS radius that asks for a pill (border-radius: 999px).</summary>
        public const float PillRadius = 999f;

        // Marks an element whose radius this sets, since its resolved radius is then no longer the USS one.
        private const string RoundClass = "pill-shape";

        // Marks an element that redraws its drop.
        private const string DropClass = "pill-shape--drop";

        // The outline and fill colours of each sticker with a drop, for when it is pressed and they are hidden.
        private static readonly ConditionalWeakTable<VisualElement, Colors> Kept = new();

        /// <summary>
        /// Keeps the ends of every pill under <paramref name="root"/> (itself included) round and the fill of every
        /// sticker with a drop round over it, now and when one becomes a pill later (a class added), as long as its
        /// size changes with it.
        /// </summary>
        public static void Keep(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(element => element.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged));
        }

        // How much thicker the bottom border is than the top: the design's drop under the outline.
        private static float Drop(IResolvedStyle style)
        {
            return Mathf.Max(0f, style.borderBottomWidth - style.borderTopWidth);
        }

        private static void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var element = (VisualElement)evt.currentTarget;
            if (!element.ClassListContains(DropClass) && Drop(element.resolvedStyle) > 0f) {
                // Before its own content, so a label's text draws over the fill.
                element.AddToClassList(DropClass);
                element.generateVisualContent = (Action<MeshGenerationContext>)DrawDrop + element.generateVisualContent;
                element.MarkDirtyRepaint();
            }

            if (!element.ClassListContains(RoundClass)) {
                if (element.resolvedStyle.borderTopLeftRadius < PillRadius) {
                    return;
                }

                element.AddToClassList(RoundClass);
            }

            // The round ends belong to the pill above its drop, which hangs under it as a short stadium.
            var rect = evt.newRect;
            var radius = new Length(Mathf.Min(rect.width, rect.height - Drop(element.resolvedStyle)) / 2f);
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static void DrawDrop(MeshGenerationContext context)
        {
            var element = context.visualElement;
            var style = element.resolvedStyle;
            var image = style.backgroundImage;
            var drop = Drop(style);
            if (drop <= 0f || image.texture != null || image.sprite != null || image.renderTexture != null
                || image.vectorImage != null) {
                return;
            }

            // Pressed, a sticker hides its own border and fill and is drawn without its drop in the colours it had.
            var outline = style.borderBottomColor;
            var background = style.backgroundColor;
            var isSunk = outline.a <= 0f;
            var kept = Kept.GetOrCreateValue(element);
            if (isSunk) {
                if (!kept.IsSet) {
                    return;
                }

                outline = kept.Outline;
                background = kept.Background;
            } else if (outline.a < 1f || background.a < 1f) {
                return;
            } else {
                kept.Outline = outline;
                kept.Background = background;
                kept.IsSet = true;
            }

            var size = element.layout.size;
            var fill = new Rect(style.borderLeftWidth, style.borderTopWidth,
                size.x - style.borderLeftWidth - style.borderRightWidth,
                size.y - style.borderTopWidth - style.borderBottomWidth);
            if (fill.width <= 0f || fill.height <= 0f) {
                return;
            }

            var box = new Rect(0f, 0f, size.x, isSunk ? size.y - drop : size.y);

            // The outline's radii, clamped on each axis as UI Toolkit does.
            var most = Mathf.Min(box.width, box.height) / 2f;
            var topLeft = Mathf.Min(style.borderTopLeftRadius, most);
            var topRight = Mathf.Min(style.borderTopRightRadius, most);
            var bottomRight = Mathf.Min(style.borderBottomRightRadius, most);
            var bottomLeft = Mathf.Min(style.borderBottomLeftRadius, most);

            // The fill's: the outline's less the side border, on both axes.
            var innerTopLeft = Mathf.Max(0f, topLeft - style.borderLeftWidth);
            var innerTopRight = Mathf.Max(0f, topRight - style.borderRightWidth);
            var innerBottomRight = Mathf.Max(0f, bottomRight - style.borderRightWidth);
            var innerBottomLeft = Mathf.Max(0f, bottomLeft - style.borderLeftWidth);
            if (innerBottomLeft <= 0f && innerBottomRight <= 0f) {
                return;
            }

            var scale = Mathf.Min(1f,
                Fit(fill.width, innerTopLeft + innerTopRight), Fit(fill.width, innerBottomLeft + innerBottomRight),
                Fit(fill.height, innerTopLeft + innerBottomLeft), Fit(fill.height, innerTopRight + innerBottomRight));

            var painter = context.painter2D;
            painter.fillColor = outline;
            FillRoundedRect(painter, box, topLeft, topRight, bottomRight, bottomLeft);
            painter.fillColor = background;
            FillRoundedRect(painter, fill, innerTopLeft * scale, innerTopRight * scale, innerBottomRight * scale,
                innerBottomLeft * scale);
        }

        private sealed class Colors
        {
            public bool IsSet;
            public Color Outline;
            public Color Background;
        }

        private static float Fit(float length, float radii)
        {
            return radii > length ? length / radii : 1f;
        }

        private static void FillRoundedRect(Painter2D painter, Rect rect, float topLeft, float topRight,
            float bottomRight, float bottomLeft)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin + topLeft, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax - topRight, rect.yMin));
            Corner(painter, new Vector2(rect.xMax - topRight, rect.yMin + topRight), topRight, 270f);
            painter.LineTo(new Vector2(rect.xMax, rect.yMax - bottomRight));
            Corner(painter, new Vector2(rect.xMax - bottomRight, rect.yMax - bottomRight), bottomRight, 0f);
            painter.LineTo(new Vector2(rect.xMin + bottomLeft, rect.yMax));
            Corner(painter, new Vector2(rect.xMin + bottomLeft, rect.yMax - bottomLeft), bottomLeft, 90f);
            painter.LineTo(new Vector2(rect.xMin, rect.yMin + topLeft));
            Corner(painter, new Vector2(rect.xMin + topLeft, rect.yMin + topLeft), topLeft, 180f);
            painter.ClosePath();
            painter.Fill();
        }

        // A quarter circle from the given angle on (y points down, so it turns clockwise on screen).
        private static void Corner(Painter2D painter, Vector2 center, float radius, float from)
        {
            if (radius > 0f) {
                painter.Arc(center, radius, Angle.Degrees(from), Angle.Degrees(from + 90f));
            }
        }
    }
}
