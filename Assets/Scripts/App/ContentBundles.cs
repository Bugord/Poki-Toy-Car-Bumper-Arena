using System;
using System.Collections.Generic;
using System.IO;
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
        // Each attempt's way in, in order: the bundle streamed, the bundle as a file (or from memory), then twice the
        // content without a bundle.
        private static readonly Route[] Routes = { Route.Streamed, Route.Bytes, Route.Raw, Route.Raw };
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
        private readonly Dictionary<string, AsyncLazy<bool>> loads = new();
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

            // The timer goes before the source it cancels: a timer left running would cancel it after its disposal.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(seconds), DelayType.Realtime);
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

        /// <summary>
        /// The bundle's one download, started on the first ask; true once its assets are in place. The background queue
        /// and a screen waiting for the same bundle await it at once, which a preserved UniTask does not allow (it takes
        /// one continuation at a time), so it is a lazy task, which takes any number.
        /// </summary>
        private UniTask<bool> Load(string name)
        {
            if (!loads.TryGetValue(name, out var load)) {
                load = Download(name, lifetime.Token).ToAsyncLazy();
                loads[name] = load;
            }

            return load.Task;
        }

        private async UniTask<bool> Download(string name, CancellationToken token)
        {
            var url = $"{Application.streamingAssetsPath}/{ContentNames.Folder}/{ContentNames.FileOf(name)}";
            var rawUrl = $"{Application.streamingAssetsPath}/{ContentNames.Folder}/{ContentNames.RawFileOf(name)}";
            for (var attempt = FirstAttempt(); attempt < Routes.Length; attempt++) {
                var route = Routes[attempt];
                Content content = null;
                try {
                    switch (route) {
                        case Route.Streamed:
                            content = await Unpack(name, await DownloadStreamed(url, token));
                            break;
                        case Route.Bytes:
                            var (bundle, how) = await DownloadToFile(name, url, attempt, token);
                            content = await Unpack(name, bundle);
                            platform.MeasureContent(how, name);
                            break;
                        default:
                            content = await DownloadRaw(name, rawUrl, attempt, token);
                            platform.MeasureContent("raw", name);
                            break;
                    }
                } catch (OperationCanceledException) {
                    return false;
                } catch (Exception exception) {
                    Debug.LogWarning($"Content bundle '{name}', attempt {attempt + 1} ({route}): {exception.Message}");
                    platform.MeasureContent(ReasonOf(exception), name);
                    // A bundle that opened but would not give its assets is not opened again (it stays loaded, and a
                    // second copy of it would not load): its raw copy comes next.
                    if (exception is ContentException { Reason: ApplyFailed } && route != Route.Raw) {
                        attempt = Array.IndexOf(Routes, Route.Raw) - 1;
                        continue;
                    }
                }

                if (content != null) {
                    Put(name, content);
                    Debug.Log($"Content bundle '{name}' in, attempt {attempt + 1} ({route})");
                    return true;
                }

                if (attempt + 1 < Routes.Length
                    && await UniTask.Delay(TimeSpan.FromSeconds(RetryDelay), DelayType.Realtime, cancellationToken: token)
                        .SuppressCancellationThrow()) {
                    return false;
                }
            }

            platform.MeasureContent("failed", name);
            return false;
        }

        /// <summary>
        /// The first attempt: the streamed bundle, unless the page asks to start further along, for checking the
        /// fallbacks in a build: <c>?content=file</c> starts with the bundle as a file, <c>?content=raw</c> with the
        /// content without a bundle.
        /// </summary>
        private static int FirstAttempt()
        {
            var page = Application.absoluteURL ?? "";
            var route = page.Contains("content=raw") ? Route.Raw : page.Contains("content=file") ? Route.Bytes : Route.Streamed;
            return Array.IndexOf(Routes, route);
        }

        private static async UniTask<AssetBundle> DownloadStreamed(string url, CancellationToken token)
        {
            using var request = UnityWebRequestAssetBundle.GetAssetBundle(url);
            request.timeout = RequestTimeout;
            await request.SendWebRequest().ToUniTask(cancellationToken: token);
            var bundle = DownloadHandlerAssetBundle.GetContent(request);
            if (bundle == null) {
                throw new ContentException(OpenFailed, $"{request.downloadedBytes} bytes did not open as a bundle");
            }

            return bundle;
        }

        /// <summary>
        /// The bundle as bytes, past any cache (a query keeps the browser from handing back what failed before), opened
        /// from a file in the player's virtual file system, the way the player opens its own data archive, which the
        /// phones that fail the streamed bundle do open; from memory if the file will not do. The second item says which
        /// opened it, <c>file</c> or <c>memory</c>, for <c>content / &lt;how&gt;-&lt;bundle&gt; / load</c>.
        /// </summary>
        private static async UniTask<(AssetBundle, string)> DownloadToFile(string name, string url, int attempt,
            CancellationToken token)
        {
            var bytes = await DownloadBytes($"{url}?try={attempt}", token);
            string path;
            try {
                path = Path.Combine(Application.temporaryCachePath, ContentNames.FileOf(name));
                File.WriteAllBytes(path, bytes);
            } catch (Exception exception) {
                Debug.LogWarning($"Content bundle '{name}' could not be written to a file: {exception.Message}");
                path = null;
            }

            if (path != null) {
                var fromFile = await AssetBundle.LoadFromFileAsync(path).ToUniTask(cancellationToken: token);
                if (fromFile != null) {
                    return (fromFile, "file");
                }
            }

            var bundle = await AssetBundle.LoadFromMemoryAsync(bytes).ToUniTask(cancellationToken: token);
            if (bundle == null) {
                throw new ContentException(OpenFailed, $"{bytes.Length} bytes did not open as a bundle");
            }

            return (bundle, "memory");
        }

        private static async UniTask<byte[]> DownloadBytes(string url, CancellationToken token)
        {
            using var request = UnityWebRequest.Get(url);
            request.timeout = RequestTimeout;
            await request.SendWebRequest().ToUniTask(cancellationToken: token);
            var bytes = request.downloadHandler.data;
            if (bytes == null || bytes.Length == 0) {
                throw new ContentException(EmptyDownload, "empty download");
            }

            return bytes;
        }

        /// <summary>
        /// The bundle's content without the bundle (<see cref="ContentNames.RawFileOf"/>): the music decoded by the
        /// browser, the textures made from their raw data. No archive is opened, so whatever fails to unpack the bundles
        /// on some phones is not on this path.
        /// </summary>
        private static async UniTask<Content> DownloadRaw(string name, string url, int attempt, CancellationToken token)
        {
            url = $"{url}?try={attempt}";
            if (name == ContentNames.MusicBundle) {
                using var request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.ACC);
                request.timeout = RequestTimeout;
                await request.SendWebRequest().ToUniTask(cancellationToken: token);
                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null) {
                    throw new ContentException(RawFailed, $"{request.downloadedBytes} bytes did not decode as audio");
                }

                clip.name = name;
                return new Content { Music = clip };
            }

            var bytes = await DownloadBytes(url, token);
            try {
                return new Content { Textures = RawTextures.Read(bytes) };
            } catch (Exception exception) {
                throw new ContentException(RawFailed, $"{bytes.Length} raw bytes did not make textures: {exception.Message}");
            }
        }

        /// <summary>The assets of a bundle that opened; a bundle that opened but will not give them fails to apply.</summary>
        private async UniTask<Content> Unpack(string name, AssetBundle bundle)
        {
            bundles.Add(bundle);
            try {
                if (name == ContentNames.MusicBundle) {
                    var request = bundle.LoadAllAssetsAsync<AudioClip>();
                    await request;
                    return new Content { Music = request.allAssets.Length > 0 ? (AudioClip)request.allAssets[0] : null };
                }

                var textures = new Dictionary<string, Texture2D>();
                var load = bundle.LoadAllAssetsAsync<Texture2D>();
                await load;
                foreach (var asset in load.allAssets) {
                    textures[asset.name] = (Texture2D)asset;
                }

                return new Content { Textures = textures };
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                throw new ContentException(ApplyFailed, $"did not apply: {exception.Message}");
            }
        }

        /// <summary>Puts a bundle's assets where the player's assets expect them.</summary>
        private void Put(string name, Content content)
        {
            if (name == ContentNames.MusicBundle) {
                if (content.Music != null) {
                    content.Music.LoadAudioData();
                    audio.SetArenaMusic(content.Music);
                }

                return;
            }

            var textures = content.Textures;
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

        /// <summary>What a bundle brings, from the bundle or from its raw copy.</summary>
        private sealed class Content
        {
            public AudioClip Music;
            public Dictionary<string, Texture2D> Textures;
        }

        /// <summary>
        /// Why an attempt failed, for <c>content / why-&lt;reason&gt;-&lt;bundle&gt; / load</c>: the request timed out, the
        /// connection or the browser's fetch failed, the server answered with an error, the file came empty, the
        /// bytes did not open as a bundle, or something else threw. Sent on every failed attempt, so a visit's
        /// reasons are all counted, not only its last.
        /// </summary>
        private static string ReasonOf(Exception exception) => exception switch {
            ContentException content => content.Reason,
            UnityWebRequestException web when web.Error != null
                && web.Error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 => "why-timeout",
            UnityWebRequestException { Result: UnityWebRequest.Result.ConnectionError } => "why-network",
            UnityWebRequestException { Result: UnityWebRequest.Result.ProtocolError } => "why-http",
            UnityWebRequestException { Result: UnityWebRequest.Result.DataProcessingError } => "why-unpack",
            _ => "why-other",
        };

        private const string OpenFailed = "why-open";
        private const string EmptyDownload = "why-empty";
        private const string ApplyFailed = "why-apply";
        private const string RawFailed = "why-raw";

        private enum Route
        {
            Streamed,
            Bytes,
            Raw,
        }

        /// <summary>A failed attempt whose reason is already known.</summary>
        private sealed class ContentException : Exception
        {
            public ContentException(string reason, string message) : base(message) => Reason = reason;

            public string Reason { get; }
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
