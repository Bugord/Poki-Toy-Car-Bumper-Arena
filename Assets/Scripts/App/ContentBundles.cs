using System;
using System.Collections.Generic;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;
using UnityEngine.Networking;
using VContainer.Unity;

namespace ToyCarArena.App
{
    /// <summary>
    /// Downloads, after the game has started, the textures and music the build moved out of the player into bundles in
    /// StreamingAssets (<see cref="ContentNames"/>), and puts them back where the player's assets expect them: on the
    /// floor materials, the car catalog's portraits and the audio library's music. Poki counts the game loaded once the
    /// player is in, so every megabyte out of it is a shorter wait before the first round, which needs none of this.
    /// Nothing waits for long: a round whose floor is still downloading waits a few seconds under the curtain and then
    /// plays on a plain floor, which takes its texture the moment it comes. In the editor the assets were never taken
    /// out, and nothing is downloaded.
    /// </summary>
    public sealed class ContentBundles : IStartable, IDisposable
    {
        private const int Attempts = 3;
        private const float RetryDelay = 2f;
        private const int RequestTimeout = 30;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        private readonly GameTuning tuning;
        private readonly ArenaCatalog arenas;
        private readonly CarCatalog cars;
        private readonly AudioLibrary audio;
        private readonly IGamePlatform platform;
        private readonly CancellationTokenSource lifetime = new();
        private readonly Dictionary<string, UniTask<bool>> loads = new();
        private readonly List<AssetBundle> bundles = new();

        public ContentBundles(GameTuning tuning, ArenaCatalog arenas, CarCatalog cars, AudioLibrary audio, IGamePlatform platform)
        {
            this.tuning = tuning;
            this.arenas = arenas;
            this.cars = cars;
            this.audio = audio;
            this.platform = platform;
        }

        private static bool IsDeferred => !Application.isEditor;

        public void Start()
        {
            if (IsDeferred) {
                LoadAll(lifetime.Token).Forget();
            }
        }

        public void Dispose()
        {
            lifetime.Cancel();
            lifetime.Dispose();
            foreach (var bundle in bundles) {
                bundle.Unload(false);
            }
        }

        /// <summary>
        /// Waits, for no longer than the given seconds, until the floor of an arena's variant has its texture, and moves
        /// the arena's bundle to the front of the downloads if it has not come yet.
        /// </summary>
        public async UniTask PrepareArena(string arenaId, string variantId, float seconds, CancellationToken token)
        {
            var arena = arenas.Find(arenaId);
            if (arena == null || HasTexture(arena.FloorMaterialOf(variantId ?? FirstVariantOf(arenaId)))) {
                return;
            }

            await Wait(ContentNames.FloorBundleOf(arenaId), seconds, token);
        }

        /// <summary>Waits, for no longer than the given seconds, until the car portraits are in.</summary>
        public async UniTask PrepareCars(float seconds, CancellationToken token)
        {
            if (cars.Cars.Count == 0 || cars.Cars[0].Portrait != null) {
                return;
            }

            await Wait(ContentNames.CarsBundle, seconds, token);
        }

        private async UniTask Wait(string name, float seconds, CancellationToken token)
        {
            if (!IsDeferred) {
                return;
            }

            var load = Load(name);
            if (load.Status != UniTaskStatus.Pending) {
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfterSlim(TimeSpan.FromSeconds(seconds), DelayType.Realtime);
            var (isTimeout, _) = await load.AttachExternalCancellation(timeout.Token).SuppressCancellationThrow();
            token.ThrowIfCancellationRequested();
            if (isTimeout) {
                platform.MeasureContent("late", name);
            }
        }

        /// <summary>
        /// The bundles one after another, the ones the next screens need first: the music under the first round, the
        /// portraits on its result, then the floors in the order the rotation reaches them.
        /// </summary>
        private async UniTaskVoid LoadAll(CancellationToken token)
        {
            await Load(ContentNames.MusicBundle);
            await Load(ContentNames.CarsBundle);
            for (var i = 1; i <= arenas.Arenas.Count; i++) {
                var arena = arenas.Arenas[i % arenas.Arenas.Count];
                if (token.IsCancellationRequested) {
                    return;
                }

                await Load(ContentNames.FloorBundleOf(arena.Id));
            }
        }

        /// <summary>The bundle's one download, started on the first ask; true once its assets are in place.</summary>
        private UniTask<bool> Load(string name)
        {
            if (!loads.TryGetValue(name, out var load)) {
                load = Download(name, lifetime.Token).Preserve();
                loads[name] = load;
            }

            return load;
        }

        private async UniTask<bool> Download(string name, CancellationToken token)
        {
            var url = $"{Application.streamingAssetsPath}/{ContentNames.Folder}/{ContentNames.FileOf(name)}";
            for (var attempt = 1; ; attempt++) {
                AssetBundle bundle = null;
                using (var request = UnityWebRequestAssetBundle.GetAssetBundle(url)) {
                    request.timeout = RequestTimeout;
                    try {
                        await request.SendWebRequest().ToUniTask(cancellationToken: token);
                        bundle = DownloadHandlerAssetBundle.GetContent(request);
                    } catch (OperationCanceledException) {
                        return false;
                    } catch (Exception exception) {
                        Debug.LogWarning($"Content bundle '{name}', attempt {attempt}: {exception.Message}");
                    }
                }

                if (bundle != null) {
                    bundles.Add(bundle);
                    // A bundle that came but would not unpack is not asked for again: the game goes on without it.
                    try {
                        await Apply(name, bundle);
                        return true;
                    } catch (Exception exception) when (exception is not OperationCanceledException) {
                        Debug.LogWarning($"Content bundle '{name}' did not apply: {exception.Message}");
                        platform.MeasureContent("failed", name);
                        return false;
                    }
                }

                if (attempt >= Attempts) {
                    platform.MeasureContent("failed", name);
                    return false;
                }

                if (await UniTask.Delay(TimeSpan.FromSeconds(RetryDelay), DelayType.Realtime, cancellationToken: token)
                        .SuppressCancellationThrow()) {
                    return false;
                }
            }
        }

        private async UniTask Apply(string name, AssetBundle bundle)
        {
            if (name == ContentNames.MusicBundle) {
                var request = bundle.LoadAllAssetsAsync<AudioClip>();
                await request;
                if (request.allAssets.Length > 0) {
                    var clip = (AudioClip)request.allAssets[0];
                    clip.LoadAudioData();
                    audio.SetArenaMusic(clip);
                }

                return;
            }

            var textures = new Dictionary<string, Texture2D>();
            var load = bundle.LoadAllAssetsAsync<Texture2D>();
            await load;
            foreach (var asset in load.allAssets) {
                textures[asset.name] = (Texture2D)asset;
            }

            if (name == ContentNames.CarsBundle) {
                foreach (var car in cars.Cars) {
                    textures.TryGetValue(ContentNames.PortraitOf(car), out var portrait);
                    textures.TryGetValue(ContentNames.BodyPortraitOf(car), out var body);
                    car.SetPortraits(portrait, body);
                }

                return;
            }

            foreach (var arena in arenas.Arenas) {
                foreach (var look in arena.Looks) {
                    var texture = ContentNames.FloorTextureOf(look.FloorMaterial);
                    if (texture != null && !HasTexture(look.FloorMaterial) && textures.TryGetValue(texture, out var map)) {
                        look.FloorMaterial.SetTexture(BaseMap, map);
                        look.FloorMaterial.SetTexture(MainTex, map);
                    }
                }
            }
        }

        private static bool HasTexture(Material material) =>
            material == null || ContentNames.FloorTextureOf(material) == null || material.GetTexture(BaseMap) != null;

        private string FirstVariantOf(string arenaId)
        {
            foreach (var map in tuning.Maps) {
                if (map.Id == arenaId) {
                    return map.Variants.Count > 0 ? map.Variants[0].Id : null;
                }
            }

            return null;
        }
    }
}
