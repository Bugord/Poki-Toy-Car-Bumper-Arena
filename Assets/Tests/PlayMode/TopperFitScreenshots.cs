using System.Collections;
using System.IO;
using NUnit.Framework;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Every car of the catalog wearing each topper, as the garage's stage draws it, a row of cars per topper:
    /// <c>Logs/topper-fit/&lt;id&gt;.png</c>, to check where each topper sits on each roof. Explicit: it is for
    /// looking at, and needs graphics.
    /// </summary>
    [Explicit("Writes pictures of every car wearing each topper for a visual check; run it by name.")]
    public sealed class TopperFitScreenshots : SimulationSceneFixture
    {
        private const int CellWidth = 480;
        private const int CellHeight = 300;

        [UnityTest]
        public IEnumerator CaptureEveryCarInEachTopper()
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "topper-fit");
            Directory.CreateDirectory(directory);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 1 }, "sandbox");
            yield return LoadGame();
            var stage = GameScope().Container.Resolve<GarageStage>();
            var cars = LoadCarCatalog().Cars;
            foreach (var topper in LoadTuning().Toppers.All) {
                var sheet = new Texture2D(CellWidth * cars.Count, CellHeight, TextureFormat.RGBA32, false);
                for (var i = 0; i < cars.Count; i++) {
                    var texture = stage.ShowCar(cars[i], 0, topper.Id);
                    yield return null;
                    yield return null;
                    var previous = RenderTexture.active;
                    var cell = RenderTexture.GetTemporary(CellWidth, CellHeight, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(texture, cell);
                    RenderTexture.active = cell;
                    sheet.ReadPixels(new Rect(0, 0, CellWidth, CellHeight), CellWidth * i, 0);
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(cell);
                }

                sheet.Apply();
                File.WriteAllBytes(Path.Combine(directory, $"{topper.Id}.png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
            }

            stage.Hide();
            yield return UnloadGame();
        }
    }
}
