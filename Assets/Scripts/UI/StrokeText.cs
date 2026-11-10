using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The outlined text of the stickers, built one way everywhere: three labels with the same text and classes in a
    /// <c>stroke</c> element. Under everything the drop (<c>stroke__drop</c>), the letters in navy without outline
    /// moved down by its text's rule (<c>.x-text.stroke__drop { translate: 0 10px; }</c>); over it the outline, the
    /// label that sizes the element; on top the face (<c>stroke__face</c>) without outline, so the letters keep their
    /// full width (.stroke in ui_screens). The drop draws what a text-shadow draws, but as a layer, because UI Toolkit
    /// draws no text-shadow under the fallback font's letters (Cyrillic, the Turkish ones): a Russian title had no
    /// drop. Its offset is in pixels, not in % of the label, because the fallback font's lines are taller: the drop is
    /// as thick in every language, and <see cref="FitText"/> shrinks it with the text. Without a rule it is hidden.
    /// </summary>
    internal static class StrokeText
    {
        public const string ClassName = "stroke";
        public const string FaceClass = "stroke__face";
        public const string DropClass = "stroke__drop";

        /// <summary>
        /// A new <c>stroke</c> element (with <paramref name="className"/>, if any) holding the three labels, each with
        /// <paramref name="textClasses"/>. The labels come outline, face, drop.
        /// </summary>
        public static VisualElement Create(string value, string className, out Label[] labels,
            params string[] textClasses)
        {
            var stroke = new VisualElement { pickingMode = PickingMode.Ignore };
            stroke.AddToClassList(ClassName);
            if (!string.IsNullOrEmpty(className)) {
                stroke.AddToClassList(className);
            }

            labels = Fill(stroke, value, textClasses);
            return stroke;
        }

        /// <summary>
        /// The usual one: the element gets <paramref name="className"/>, its labels <c>display</c> (the titles' white
        /// letters and navy outline) and <c>className-text</c>, which sets their size.
        /// </summary>
        public static VisualElement Outlined(string value, string className, out Label[] labels) =>
            Create(value, className, out labels, "display", className + "-text");

        /// <summary>Adds the three labels to <paramref name="stroke"/>, drop first: outline, face, drop.</summary>
        public static Label[] Fill(VisualElement stroke, string value, params string[] textClasses)
        {
            var outline = Text(value, textClasses);
            var face = Text(value, textClasses);
            face.AddToClassList(FaceClass);
            stroke.Add(outline);
            stroke.Add(face);
            var drop = AddDrop(outline);
            return new[] { outline, face, drop };
        }

        /// <summary>
        /// Gives every stroke of a screen's UXML its drop: a copy of its outline label (the first label in it that is
        /// not the face) under it. Its text follows the outline's, set from code or by a localized binding.
        /// </summary>
        public static void AddDrops(VisualElement root)
        {
            root.Query<VisualElement>(className: ClassName).ForEach(stroke => {
                Label outline = null;
                foreach (var child in stroke.Children()) {
                    if (child.ClassListContains(DropClass)) {
                        return;
                    }

                    if (outline == null && child is Label label && !label.ClassListContains(FaceClass)) {
                        outline = label;
                    }
                }

                if (outline != null) {
                    AddDrop(outline);
                }
            });
        }

        /// <summary>The three labels of <paramref name="stroke"/> (any order).</summary>
        public static Label[] LabelsOf(VisualElement stroke) => stroke.Query<Label>().ToList().ToArray();

        /// <summary>Sets the text of every label of the stroke.</summary>
        public static void SetText(Label[] labels, string value)
        {
            foreach (var label in labels) {
                label.text = value;
            }
        }

        /// <summary>
        /// Toggles a class on every label of the stroke, the drop too, so it keeps the size of the others.
        /// </summary>
        public static void EnableInClassList(Label[] labels, string className, bool enable)
        {
            foreach (var label in labels) {
                label.EnableInClassList(className, enable);
            }
        }

        // The drop: the outline's text and classes, before it in the stroke so it draws under it. Its text follows the
        // outline's: a label sends a change of its text while on a panel, and one set before is copied on attaching.
        private static Label AddDrop(Label outline)
        {
            var drop = new Label(outline.text) { pickingMode = PickingMode.Ignore };
            foreach (var className in outline.GetClasses()) {
                drop.AddToClassList(className);
            }

            drop.AddToClassList(DropClass);
            var stroke = outline.parent;
            stroke.Insert(stroke.IndexOf(outline), drop);
            outline.RegisterCallback<ChangeEvent<string>>(evt => {
                if (evt.target == outline) {
                    drop.text = evt.newValue;
                }
            });
            drop.RegisterCallback<AttachToPanelEvent>(_ => drop.text = outline.text);
            return drop;
        }

        private static Label Text(string value, string[] classes)
        {
            var label = new Label(value) { pickingMode = PickingMode.Ignore };
            foreach (var className in classes) {
                label.AddToClassList(className);
            }

            return label;
        }
    }
}
