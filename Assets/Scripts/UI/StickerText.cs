using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>Two sibling labels, so text keeps its measured size and its face never becomes its text container.</summary>
    public sealed class StickerText : VisualElement
    {
        private readonly Label outline;
        private readonly Label face;

        public StickerText(string text)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("stroke");
            AddToClassList("sticker-text");
            outline = new Label(text) { pickingMode = PickingMode.Ignore };
            face = new Label(text) { pickingMode = PickingMode.Ignore };
            outline.RemoveFromClassList(Label.ussClassName);
            face.RemoveFromClassList(Label.ussClassName);
            outline.AddToClassList("sticker-text__text");
            face.AddToClassList("sticker-text__text");
            face.AddToClassList("stroke__face");
            Add(outline);
            Add(face);
        }

        public void SetText(string text)
        {
            outline.text = text;
            face.text = text;
        }
    }
}
