using System.Collections;
using System.IO;
using NUnit.Framework;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Rendering;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The two cars of the web template's loading screen, drawn side on as the arena draws them (outline, the Game
    /// scene's sun, transparent around them): the crowned monster truck that pushes and the car it pushes off the bar,
    /// <c>Logs/loading-art/&lt;name&gt;.png</c>. Tools/loading/art.py trims and shrinks them into the template.
    /// Explicit: it is for making pictures, and needs graphics.
    /// </summary>
    [Explicit("Draws the loading screen's cars; run it by name.")]
    public sealed class LoadingScreenArt : SimulationSceneFixture
    {
        private static readonly Vector3 Stage = new(0f, 4000f, 0f);

        private const int Width = 1600;
        private const int Height = 1200;
        private const float FieldOfView = 20f;
        private const float Pitch = 14f;
        private const float Boost = 5f;
        // One camera for both cars, so they keep the size the arena gives them next to each other.
        private const float Distance = 16f;
        private const float LookHeight = 1.2f;
        // Colour ids of CarPaint: red for the king, blue for the one going off.
        private const int TruckColor = 1;
        private const int CarColor = 2;

        [UnityTest]
        public IEnumerator DrawTheLoadingScreenCars()
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "loading-art");
            Directory.CreateDirectory(directory);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 1 }, "sandbox");
            yield return LoadGame();

            var catalog = LoadCarCatalog();
            // Nose right and a little towards the camera; the pushed car faces the truck, going backwards.
            yield return Draw(directory, "truck", catalog.Find("monster-truck"), TruckColor, "crown", 100f);
            yield return Draw(directory, "car", catalog.Find("racer"), CarColor, "", -110f);
            yield return UnloadGame();
        }

        private static IEnumerator Draw(string directory, string name, CarCatalog.Entry entry, int colorId, string topper,
            float yaw)
        {
            var look = LoadCarPrefab().GetComponent<CarLook>();
            var root = new GameObject("Loading Art");
            root.transform.position = Stage;
            var car = new GameObject("Car").transform;
            car.SetParent(root.transform, false);
            var model = CarLook.BuildModel(entry, car, look.ModelRotation, look.ModelMaterial, look.Reference);
            var body = CarModel.PaintBody(CarModel.FindBody(model, out _), entry.BodySwatch, look.SwatchOf(colorId));
            CarLook.WearTopper(topper, entry.Id, car, model, look.ModelMaterial, look.Toppers, out _);
            var bounds = CarModel.BoundsIn(car.gameObject, car.worldToLocalMatrix);
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            car.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            texture.Create();
            var camera = new GameObject("Loading Art Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 60f;
            camera.targetTexture = texture;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var rotation = Quaternion.Euler(Pitch, 0f, 0f);
            camera.transform.localRotation = rotation;
            camera.transform.localPosition = new Vector3(0f, LookHeight, 0f) - rotation * Vector3.forward * Distance;
            OutlineBoost.Set(camera, Boost);

            yield return null;
            yield return null;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var picture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            picture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            picture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(directory, $"{name}.png"), picture.EncodeToPNG());

            OutlineBoost.Clear(camera);
            Object.Destroy(picture);
            camera.targetTexture = null;
            texture.Release();
            Object.Destroy(texture);
            Object.Destroy(root);
            Object.Destroy(body);
        }

        private static Car LoadCarPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Car>("Assets/Prefabs/Cars/Car.prefab");
#else
            throw new System.NotSupportedException("Draw the loading screen's cars in the editor.");
#endif
        }
    }
}
