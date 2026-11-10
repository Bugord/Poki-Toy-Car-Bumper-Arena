using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Pickups;
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
    /// The picture on the menu's HOT POTATO card, made from the game's own models: the red monster truck in three
    /// quarters from the front and above, as the catalog's portraits show the cars (the QUICK MATCH card's), with the
    /// mode's bomb riding on its roof, its fuse lit, all in the
    /// arena's outline and the Game scene's sun, transparent around them. Written to
    /// <c>Assets/Art/Sprites/UI/sprite_mode_hot_potato.png</c> at half the size it is drawn, so its edges are smooth.
    /// Explicit: it is for making the picture, and needs graphics.
    /// </summary>
    [Explicit("Draws the HOT POTATO card's picture; run it by name.")]
    public sealed class ModeCardArt : ObjectsBFixture
    {
        private const string Output = "Assets/Art/Sprites/UI/sprite_mode_hot_potato.png";
        private static readonly Vector3 Stage = new(0f, 4000f, 0f);

        // Drawn at twice the size it is kept, then halved; as tall for its width as the board's picture.
        private const int Width = 928;
        private const int Height = 1024;
        // CarCatalogUpdater's portrait camera, seen from (4, 3, 5): 25 degrees down, 38.7 degrees round.
        private const float Pitch = 25f;
        private const float Yaw = -218.66f;
        private const float BombScale = 1f;
        private const float BombGap = 0.02f;
        private const float Margin = 1.06f;
        private const float Boost = 4f;
        // CarPaint's red, as the board's truck: apart from the slate bomb on it.
        private const int TruckColor = 1;

        [UnityTest]
        public IEnumerator DrawTheHotPotatoCard()
        {
            UseSpecial(SpecialRound.HotPotato);
            yield return Open("sandbox", "rake");
            var potato = Container.Resolve<HotPotato>();
            var timeout = Time.time + Tuning.SpecialRounds.HotPotato.FirstDelay + 2f;
            while (potato.Holder == null && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(potato.Holder, Is.Not.Null, "No bomb came to copy.");
            var sceneBomb = GameObject.Find("Bomb");
            Assert.That(sceneBomb, Is.Not.Null);

            var look = LoadCarPrefab().GetComponent<CarLook>();
            var entry = LoadCarCatalog().Find("monster-truck");
            var root = new GameObject("Mode Card Art");
            root.transform.position = Stage;
            var car = new GameObject("Car").transform;
            car.SetParent(root.transform, false);
            var meshes = new List<Mesh>();
            var model = CarLook.BuildModel(entry, car, look.ModelRotation, look.ModelMaterial, look.Reference);
            var body = CarModel.PaintBody(CarModel.FindBody(model, out _), entry.BodySwatch, look.SwatchOf(TruckColor));
            var bounds = CarModel.BoundsIn(car.gameObject, car.worldToLocalMatrix);
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            car.localRotation = Quaternion.Euler(0f, Yaw, 0f);

            // The bomb as it rides in the round: lit, its spark up, a little bigger so it reads at the card's size.
            var bomb = Object.Instantiate(sceneBomb, root.transform, false).transform;
            bomb.gameObject.SetActive(true);
            bomb.localScale = Vector3.one * BombScale;
            bomb.localRotation = Quaternion.Euler(0f, -10f, 8f);
            var radius = PickupArtRadius * BombScale;
            bomb.localPosition = Vector3.up * (bounds.size.y + BombGap + radius);
            var flash = bomb.Find("Flash");
            if (flash != null) {
                flash.gameObject.SetActive(false);
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
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), Output), cropped.EncodeToPNG());
            Debug.Log($"Mode card art: {cropped.width}x{cropped.height}");
            Import();

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
            foreach (var mesh in meshes) {
                Object.Destroy(mesh);
            }
        }

        // PickupArt.BombRadius, which the tests cannot see.
        private const float PickupArtRadius = 0.5f;

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

        private static void Import()
        {
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Output);
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
