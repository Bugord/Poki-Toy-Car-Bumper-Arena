using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Rendering;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Pictures of the arena objects that toss cars, in each of their moments, for a look at their art: the jack-in-the-boxes
    /// of playroom/blocks, the jelly pads of candy/chocolate and the sand mounds of sandbox/bricks. Each moment is written
    /// twice to <c>Logs/mechanics/&lt;object&gt;_&lt;moment&gt;_&lt;view&gt;.png</c>: as the game camera frames the arena in
    /// portrait, and close up from the same angle. Explicit: it needs graphics and is for a person to look at.
    /// </summary>
    [Explicit("Writes pictures of the arena objects for a visual check; run it by name.")]
    public sealed class MechanicLookScreenshots : ObjectsBFixture
    {
        private string directory;

        [UnityTest]
        public IEnumerator Jack()
        {
            yield return Open("playroom", "blocks");
            var jack = Container.Resolve<JackBoxes>();
            var spot = jack.CentreOf(0);
            yield return Shoot("jack", "idle", spot);
            jack.Announce(0);
            yield return Wait(0.6f);
            yield return Shoot("jack", "warning", spot);
            while (jack.StateOf(0) != JackBoxes.SpotState.Open) {
                yield return Step();
            }

            yield return Wait(0.08f);
            yield return Shoot("jack", "pop", spot);
            yield return Wait(0.6f);
            yield return Shoot("jack", "up", spot);
        }

        [UnityTest]
        public IEnumerator Jelly()
        {
            yield return Open("candy", "chocolate");
            var jelly = Container.Resolve<JellyPads>();
            var spot = jelly.CentreOf(0);
            yield return Wait(0.5f);
            yield return Shoot("jelly", "idle", spot);
            // The squash after a toss, as a car that drove onto the pad would start it.
            var pads = (IList)typeof(JellyPads).GetField("pads", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(jelly);
            var clock = (float)typeof(JellyPads).GetField("clock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(jelly);
            pads[0].GetType().GetField("WobbleStart").SetValue(pads[0], clock);
            yield return Wait(0.04f);
            yield return Shoot("jelly", "squash", spot);
            yield return Wait(0.1f);
            yield return Shoot("jelly", "spring", spot);
        }

        [UnityTest]
        public IEnumerator Mounds()
        {
            yield return Open("sandbox", "bricks");
            var mounds = Container.Resolve<ArenaMounds>();
            var spot = new Vector3(2f, 0f, 3f);
            typeof(ArenaMounds).GetMethod("Announce", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(mounds, new object[] { spot });
            yield return Wait(0.3f);
            yield return Shoot("mounds", "warning-early", spot);
            yield return Wait(0.7f);
            yield return Shoot("mounds", "warning-late", spot);
            while (!mounds.TryGetMound(0, out _, out var isRisen) || !isRisen) {
                yield return Step();
            }

            yield return Wait(0.1f);
            yield return Shoot("mounds", "pop", spot);
            yield return Wait(0.6f);
            yield return Shoot("mounds", "risen", spot);
        }

        private IEnumerator Shoot(string thing, string moment, Vector3 spot)
        {
            directory ??= Path.Combine(Directory.GetCurrentDirectory(), "Logs", "mechanics");
            Directory.CreateDirectory(directory);
            var camera = Camera.main;
            var game = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = game;
            // The arena camera refits to the texture's size in its LateUpdate.
            yield return null;
            yield return null;
            Write(camera, game, Path.Combine(directory, $"{thing}_{moment}_game.png"));
            camera.targetTexture = null;
            Object.DestroyImmediate(game);

            var close = new GameObject("Close Camera").AddComponent<Camera>();
            close.CopyFrom(camera);
            close.fieldOfView = 36f;
            close.transform.SetPositionAndRotation(spot - camera.transform.forward * 12f, camera.transform.rotation);
            var picture = new RenderTexture(1200, 900, 24, RenderTextureFormat.ARGB32);
            close.targetTexture = picture;
            OutlineBoost.Set(close, 1f);
            Write(close, picture, Path.Combine(directory, $"{thing}_{moment}_close.png"));
            OutlineBoost.Clear(close);
            close.targetTexture = null;
            Object.DestroyImmediate(picture);
            Object.DestroyImmediate(close.gameObject);
        }

        private static void Write(Camera camera, RenderTexture target, string path)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Debug.Log($"Mechanic picture: {path}");
        }
    }
}
