using System.Collections;
using System.IO;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Close pictures of the player's car on the arena wearing each topper of the garage:
    /// <c>Logs/toppers/&lt;id&gt;.png</c>. Explicit: it is for looking at, and needs graphics.
    /// </summary>
    [Explicit("Writes pictures of the toppers on a car for a visual check; run it by name.")]
    public sealed class TopperScreenshots : SimulationSceneFixture
    {
        [UnityTest]
        public IEnumerator CaptureEachTopper()
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "toppers");
            Directory.CreateDirectory(directory);
            foreach (var topper in LoadTuning().Toppers.All) {
                PlayerTopper = topper.Id;
                CreateParent(new SimulationInput(), new SimulationProgress { Round = 1 }, "sandbox");
                yield return LoadGame();
                var round = GameScope().Container.Resolve<ArenaRound>();
                round.Place();
                for (var i = 0; i < 30; i++) {
                    yield return null;
                }

                var car = round.Player.transform;
                var camera = Camera.main;
                var target = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32);
                var position = camera.transform.position;
                var rotation = camera.transform.rotation;
                // From in front and above, as the arena camera looks, but close.
                camera.transform.position = car.position + car.forward * 3.4f + Vector3.up * 3.2f + car.right * 1.6f;
                camera.transform.LookAt(car.position + Vector3.up * 0.8f);
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
                camera.transform.SetPositionAndRotation(position, rotation);
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var picture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(directory, $"{topper.Id}.png"), picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
                Object.DestroyImmediate(target);
                yield return UnloadGame();
                Object.DestroyImmediate(parentScope.gameObject);
                parentScope = null;
            }
        }
    }
}
