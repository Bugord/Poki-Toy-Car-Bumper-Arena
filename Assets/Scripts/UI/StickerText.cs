using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Outlined text (<see cref="StrokeText"/>) as one element, so it keeps its measured size and its face never
    /// becomes its text container. Its labels drop the label class: the style sheet sets them whole.
    /// </summary>
    public sealed class StickerText : VisualElement
    {
        private readonly Label[] labels;

        public StickerText(string text)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList(StrokeText.ClassName);
            AddToClassList("sticker-text");
            labels = StrokeText.Fill(this, text, "sticker-text__text");
            foreach (var label in labels) {
                label.RemoveFromClassList(Label.ussClassName);
            }
        }

        public void SetText(string text) => StrokeText.SetText(labels, text);
    }
}
