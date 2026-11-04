using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToyCarArena.App;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyCarArena.Editor
{
    /// <summary>
    /// Moves the heavy leaf assets the first round does not need out of the player, for <see cref="WebGLBuild"/>: the
    /// floor textures of every arena variant but the first round's, the car portraits and the music go into bundles,
    /// and the references to them are cut from the player's assets while it builds, then the asset files are written
    /// back byte for byte. The player downloads the bundles after it starts (<see cref="ContentBundles"/>). Only
    /// textures and an audio clip move: they depend on nothing, so no bundle holds a copy of a shader or material.
    /// </summary>
    internal sealed class DeferredContent : IDisposable
    {
        private const string BundleFolder = "Library/ContentBundles";
        private static readonly string[] FloorMaps = { "_BaseMap", "_MainTex" };

        private readonly Dictionary<string, List<string>> bundles = new();
        private readonly Dictionary<string, byte[]> originals = new();

        private DeferredContent()
        {
        }

        /// <summary>The project paths of every asset moved into a bundle.</summary>
        public IReadOnlyCollection<string> Paths => bundles.Values.SelectMany(paths => paths).ToList();

        public IEnumerable<string> BundleNames => bundles.Keys;

        /// <summary>
        /// Collects the assets, checks they follow the names the player looks them up by, and builds their bundles.
        /// </summary>
        public static DeferredContent Build()
        {
            var content = new DeferredContent();
            var tuning = Load<GameTuning>();
            var arenas = Load<ArenaCatalog>();
            var cars = Load<CarCatalog>();
            var audio = Load<AudioLibrary>();

            content.Add(ContentNames.MusicBundle, audio.ArenaMusic, audio.ArenaMusic != null ? audio.ArenaMusic.name : null);
            foreach (var car in cars.Cars) {
                content.Add(ContentNames.CarsBundle, car.Portrait, ContentNames.PortraitOf(car));
                content.Add(ContentNames.CarsBundle, car.BodyPortrait, ContentNames.BodyPortraitOf(car));
            }

            var first = ContentNames.FirstFloor(tuning, arenas);
            foreach (var arena in arenas.Arenas) {
                foreach (var look in arena.Looks) {
                    var material = look.FloorMaterial;
                    if (material == null || material == first) {
                        continue;
                    }

                    var name = ContentNames.FloorTextureOf(material);
                    var texture = material.GetTexture(FloorMaps[0]);
                    if (name == null || texture == null) {
                        throw new BuildFailedException($"Floor material {material.name} does not draw a tex_floor_ texture.");
                    }

                    content.Add(ContentNames.FloorBundleOf(arena.Id), texture, name);
                }

                // The player downloads one for every arena.
                if (!content.bundles.ContainsKey(ContentNames.FloorBundleOf(arena.Id))) {
                    throw new BuildFailedException($"Arena {arena.Id} has no floor to move into a bundle.");
                }
            }

            Directory.CreateDirectory(BundleFolder);
            var builds = content.bundles
                .Select(bundle => new AssetBundleBuild { assetBundleName = bundle.Key, assetNames = bundle.Value.ToArray() })
                .ToArray();
            var manifest = BuildPipeline.BuildAssetBundles(BundleFolder, builds,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.WebGL);
            if (manifest == null) {
                throw new BuildFailedException("The content bundles did not build.");
            }

            return content;
        }

        /// <summary>
        /// Cuts the references to the moved assets from the player's assets, until <see cref="Dispose"/> writes the
        /// files back.
        /// </summary>
        public void Strip()
        {
            var tuning = Load<GameTuning>();
            var arenas = Load<ArenaCatalog>();
            var cars = Load<CarCatalog>();
            var audio = Load<AudioLibrary>();

            Keep(audio);
            audio.SetArenaMusic(null);
            Keep(cars);
            foreach (var car in cars.Cars) {
                car.SetPortraits(null, null);
            }

            var first = ContentNames.FirstFloor(tuning, arenas);
            foreach (var look in arenas.Arenas.SelectMany(arena => arena.Looks)) {
                if (look.FloorMaterial == null || look.FloorMaterial == first) {
                    continue;
                }

                Keep(look.FloorMaterial);
                foreach (var map in FloorMaps) {
                    look.FloorMaterial.SetTexture(map, null);
                }
            }

            foreach (var path in originals.Keys) {
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.GUIDFromAssetPath(path));
            }
        }

        /// <summary>Copies the bundles where the player looks for them, under the built game's StreamingAssets.</summary>
        public void CopyTo(string outputPath)
        {
            var folder = Path.Combine(outputPath, "StreamingAssets", ContentNames.Folder);
            Directory.CreateDirectory(folder);
            foreach (var name in bundles.Keys) {
                File.Copy(Path.Combine(BundleFolder, name), Path.Combine(folder, ContentNames.FileOf(name)), true);
            }
        }

        public void Dispose()
        {
            foreach (var (path, bytes) in originals) {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }

            originals.Clear();
        }

        private void Add(string bundle, Object asset, string name)
        {
            if (asset == null) {
                throw new BuildFailedException($"Bundle {bundle}: {name ?? "an asset"} is missing.");
            }

            if (asset.name != name) {
                throw new BuildFailedException($"Bundle {bundle}: {asset.name} should be named {name}, the name the game looks it up by.");
            }

            if (!bundles.TryGetValue(bundle, out var paths)) {
                paths = new List<string>();
                bundles[bundle] = paths;
            }

            paths.Add(AssetDatabase.GetAssetPath(asset));
        }

        private void Keep(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (!originals.ContainsKey(path)) {
                originals[path] = File.ReadAllBytes(path);
            }

            EditorUtility.SetDirty(asset);
        }

        private static T Load<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length != 1) {
                throw new BuildFailedException($"Expected one {typeof(T).Name}, found {guids.Length}.");
            }

            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
