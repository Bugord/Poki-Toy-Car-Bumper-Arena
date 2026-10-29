using ToyCarArena.Gameplay.Cars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>One neutral car with a separately tinted body, using the catalog's two aligned images.</summary>
    internal sealed class CarPortrait
    {
        private readonly VisualElement main;
        private readonly VisualElement body;
        private int colorId;
        private float reveal;

        public CarPortrait(VisualElement root)
        {
            root.RemoveFromClassList("car-portrait--locked");
            this.main = Layer(root);
            this.body = Layer(root);
        }

        public void Set(CarCatalog.Entry car, int colorId, bool locked = false)
        {
            main.style.backgroundImage = new StyleBackground(car?.Portrait);
            body.style.backgroundImage = new StyleBackground(car?.BodyPortrait);
            body.style.display = car?.BodyPortrait != null ? DisplayStyle.Flex : DisplayStyle.None;
            this.colorId = colorId;
            SetReveal(locked ? 0f : 1f);
        }

        public void SetColor(int colorId)
        {
            this.colorId = colorId;
            SetReveal(reveal);
        }

        public void SetReveal(float amount)
        {
            this.reveal = Mathf.Clamp01(amount);
            main.style.unityBackgroundImageTintColor = Color.Lerp(CarColor.Silhouette, Color.white, reveal);
            body.style.unityBackgroundImageTintColor = Color.Lerp(CarColor.Silhouette, CarColor.TintOf(colorId), reveal);
        }

        private static VisualElement Layer(VisualElement root)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("car-portrait__layer");
            root.Add(layer);
            return layer;
        }
    }
}
