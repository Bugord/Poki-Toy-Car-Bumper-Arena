using System.Collections;
using System.IO;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Rendering;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The picture on an event mode's menu card (CHOOSE MODE's event card, the tile under XP BOOST), made from the game's
    /// own models as the mode describes it (<see cref="ModeCard"/>): its car in three quarters from the front and above,
    /// as the catalog's portraits show the cars (the QUICK MATCH card's), with the mode's prop riding on its roof (the
    /// hot potato's lit bomb), all in the arena's outline and the Game scene's sun, transparent around them. Written to
    /// <c>Assets/Art/Sprites/UI/sprite_mode_&lt;id with underscores&gt;.png</c> at half the size it is drawn, so its
    /// edges are smooth. Explicit: it is for making the pictures, and needs graphics. A new mode adds its id to the
    /// cases and draws again after its card changes.
    /// </summary>
    [Explicit("Draws the event cards' pictures; run it by name.")]
    public sealed class ModeCardArt : ObjectsBFixture
    {
        private static readonly Vector3 Stage = new(0f, 4000f, 0f);

        // Drawn at twice the size it is kept, then halved; as tall for its width as the board's picture.
        private const int Width = 928;
        private const int Height = 1024;
        // CarCatalogUpdater's portrait camera, seen from (4, 3, 5): 25 degrees down, 38.7 degrees round.
        private const float Pitch = 25f;
        private const float Yaw = -218.66f;
        private const float PropGap = 0.02f;
        private const float Margin = 1.06f;
        private const float Boost = 4f;
        // How long the mode's round may take to show its prop (the hot potato's first bomb comes after its delay).
        private const float PropWait = 12f;

        [UnityTest]
        public IEnumerator DrawTheCard([Values("hot-potato", "crown", "gold-rush", "item-frenzy", "overheat",
            "team-push", "football")] string id)
        {
            var kind = EventModes.KindOf(EventModes.Parse(id));
            Assert.That(kind, Is.Not.EqualTo(SpecialRound.None), $"No mode {id}.");
            UseSpecial(kind);
            yield return Open("sandbox", "rake");
            var mode = Container.Resolve<RoundModes>().Active;
            Assert.That(mode, Is.Not.Null, $"The round of {id} has no active mode.");
            var card = mode.Card;

            GameObject sceneProp = null;
            if (card.Prop != null) {
                var timeout = Time.time + PropWait;
                while ((sceneProp = GameObject.Find(card.Prop)) == null && Time.time < timeout) {
                    yield return Step();
                }

                Assert.That(sceneProp, Is.Not.Null, $"No {card.Prop} came to copy.");
            }

            var look = LoadCarPrefab().GetComponent<CarLook>();
            var entry = LoadCarCatalog().Find(card.CarId);
            Assert.That(entry, Is.Not.Null, $"No car {card.CarId}.");
            var root = new GameObject("Mode Card Art");
            root.transform.position = Stage;
            var car = new GameObject("Car").transform;
            car.SetParent(root.transform, false);
            var model = CarLook.BuildModel(entry, car, look.ModelRotation, look.ModelMaterial, look.Reference);
            var body = CarModel.PaintBody(CarModel.FindBody(model, out _), entry.BodySwatch, look.SwatchOf(card.Color));
            var bounds = CarModel.BoundsIn(car.gameObject, car.worldToLocalMatrix);
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            car.localRotation = Quaternion.Euler(0f, Yaw, 0f);

            if (sceneProp != null) {
                // The prop as it rides in the round (the bomb lit, its spark up), its flash off.
                var prop = Object.Instantiate(sceneProp, root.transform, false).transform;
                prop.gameObject.SetActive(true);
                prop.localScale = Vector3.one;
                prop.localRotation = Quaternion.Euler(0f, -10f, 8f);
                prop.localPosition = Vector3.up * (bounds.size.y + PropGap + card.PropRadius);
                var flash = prop.Find("Flash");
                if (flash != null) {
                    flash.gameObject.SetActive(false);
                }
            }

            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            texture.Create();
            var camera = new GameObject("Mode Card Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 60f;
            camera.targetTexture = texture;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            Frame(camera, root.transform);
            OutlineBoost.Set(camera, Boost);

            yield return null;
            yield return null;
            camera.Render();
            var half = new RenderTexture(Width / 2, Height / 2, 0, RenderTextureFormat.ARGB32);
            half.Create();
            Graphics.Blit(texture, half);
            var previous = RenderTexture.active;
            RenderTexture.active = half;
            var picture = new Texture2D(half.width, half.height, TextureFormat.RGBA32, false);
            picture.ReadPixels(new Rect(0, 0, half.width, half.height), 0, 0);
            picture.Apply();
            RenderTexture.active = previous;
            // Cut to what was drawn, as the board's picture is: the card places the toy, not empty margins.
            var cropped = Crop(picture);
            var output = $"Assets/Art/Sprites/UI/sprite_mode_{id.Replace('-', '_')}.png";
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), output), cropped.EncodeToPNG());
            Debug.Log($"Mode card art {id}: {cropped.width}x{cropped.height}");
            Import(output);

            OutlineBoost.Clear(camera);
            Object.Destroy(picture);
            Object.Destroy(cropped);
            camera.targetTexture = null;
            texture.Release();
            half.Release();
            Object.Destroy(texture);
            Object.Destroy(half);
            Object.Destroy(root);
            Object.Destroy(body);
        }

        // An orthographic camera looking down at Pitch, sized so every renderer under the root fits with a margin.
        private static void Frame(Camera camera, Transform root)
        {
            var rotation = Quaternion.Euler(Pitch, 0f, 0f);
            camera.transform.rotation = rotation;
            var view = Matrix4x4.Rotate(Quaternion.Inverse(rotation));
            var min = Vector3.one * float.MaxValue;
            var max = Vector3.one * float.MinValue;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) {
                var b = renderer.bounds;
                for (var i = 0; i < 8; i++) {
                    var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i / 2 % 2 == 0 ? b.min.y : b.max.y,
                        i / 4 == 0 ? b.min.z : b.max.z);
                    var local = view.MultiplyPoint3x4(corner - root.position);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }

            var middle = (min + max) * 0.5f;
            var aspect = (float)Width / Height;
            camera.orthographicSize = Mathf.Max((max.y - min.y) * 0.5f, (max.x - min.x) * 0.5f / aspect) * Margin;
            camera.transform.position = root.position + rotation * new Vector3(middle.x, middle.y, min.z - 20f);
        }

        // The picture cut to its visible pixels, with a couple of clear pixels round them so the outline's soft edge stays.
        private static Texture2D Crop(Texture2D picture)
        {
            const int Pad = 2;
            var pixels = picture.GetPixels32();
            int minX = picture.width, minY = picture.height, maxX = -1, maxY = -1;
            for (var y = 0; y < picture.height; y++) {
                for (var x = 0; x < picture.width; x++) {
                    if (pixels[y * picture.width + x].a < 8) {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            minX = Mathf.Max(0, minX - Pad);
            minY = Mathf.Max(0, minY - Pad);
            maxX = Mathf.Min(picture.width - 1, maxX + Pad);
            maxY = Mathf.Min(picture.height - 1, maxY + Pad);
            // Sides in fours, as the compressed blocks need.
            while ((maxX - minX + 1) % 4 != 0) {
                if (maxX < picture.width - 1) maxX++; else minX--;
            }

            while ((maxY - minY + 1) % 4 != 0) {
                if (maxY < picture.height - 1) maxY++; else minY--;
            }
            var cropped = new Texture2D(maxX - minX + 1, maxY - minY + 1, TextureFormat.RGBA32, false);
            cropped.SetPixels(picture.GetPixels(minX, minY, cropped.width, cropped.height));
            cropped.Apply();
            return cropped;
        }

        private static void Import(string output)
        {
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(output);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            // Kept at its own size: scaled to a power of two, the picture would be squeezed out of shape.
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = 70;
            importer.SaveAndReimport();
#endif
        }

        private static Car LoadCarPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Car>("Assets/Prefabs/Cars/Car.prefab");
#else
            throw new System.NotSupportedException("Draw the card's picture in the editor.");
#endif
        }
    }
}
