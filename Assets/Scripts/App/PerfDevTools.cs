#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Bugord.Core.Storage;
using ToyCarArena.Gameplay;
using ToyCarArena.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer.Unity;
#if BUGORD_SUPERCONSOLE
using Bugord.SuperConsole.Commands;
// Inside this namespace the bare name SuperConsole would be the Bugord.SuperConsole namespace.
using DevConsole = Bugord.SuperConsole.SuperConsole;
#endif

namespace ToyCarArena.App
{
    /// <summary>
    /// Performance tools of development builds and the editor, none of it in a release build: the <see cref="PerfOverlay"/>
    /// readout (off by default, since it covers the menu; <c>perf_overlay 1</c> or the <c>perf.overlay</c> watch turns it on
    /// and the choice is saved, so a tester keeps it across reloads) and, with Super Console, WATCHES rows (pin <c>perf.fps</c> to see it with the console closed) and
    /// <c>perf_*</c> commands that switch one rendering cost at a time, so a phone can show where its frame time goes.
    /// The settings these touch live in shared assets that outlive Play mode, so their original values are put back on
    /// dispose.
    /// </summary>
    public sealed class PerfDevTools : IStartable, ITickable, IDisposable
    {
        // The arena rebuilds its world and mechanic objects every round; hidden ones are hidden again this often.
        private const float ReapplyInterval = 0.5f;

        // How often the sampled watches read, and how much of the past the frame readouts average.
        private const float WatchInterval = 0.5f;

        // Saved, so a tester who turns the overlay on keeps it on the next time the page loads.
        private const string OverlayKey = "perf.overlay";

        private static readonly string[] WorldRoots = { "World", "Decor" };
        private static readonly string[] MechanicRoots = { "Candy Patches", "Carousel", "ArenaBall" };

        private readonly RenderScaler scaler;
        private readonly IKeyValueStorage storage;
        private readonly FrameStats stats = new();
        private readonly Dictionary<OutlineFeature, bool> originalOutline = new();
        private readonly List<GameObject> worldObjects = new();
        private readonly List<Renderer> mechanicRenderers = new();
        private readonly List<IDisposable> handles = new();
        private readonly UniversalRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        private readonly int originalMsaa;
#if !UNITY_EDITOR
        private readonly GameTuning tuning;
#endif

        private OutlineFeature[] outlineFeatures;
        private GameObject overlay;
        private PerfOverlay overlayView;
        private bool isWorldShown = true;
        private bool isMechanicShown = true;
        private float sinceReapply;

        public PerfDevTools(RenderScaler scaler, GameTuning tuning, IKeyValueStorage storage)
        {
            this.scaler = scaler;
            this.storage = storage;
            originalMsaa = asset != null ? asset.msaaSampleCount : 1;
#if !UNITY_EDITOR
            this.tuning = tuning;
#endif
        }

        public void Start()
        {
            overlay = new GameObject("PerfOverlay");
            UnityEngine.Object.DontDestroyOnLoad(overlay);
            overlayView = overlay.AddComponent<PerfOverlay>();
            overlayView.Bind(stats, scaler);
            // A disabled component gets neither Update nor OnGUI, so a hidden overlay costs nothing per frame.
            overlayView.enabled = storage.GetBool(OverlayKey);

#if BUGORD_SUPERCONSOLE
            RegisterWatches();
            RegisterCommands();
#endif
        }

        public void Tick()
        {
            stats.Add(Time.unscaledDeltaTime);
            if (isWorldShown && isMechanicShown) {
                return;
            }

            sinceReapply += Time.unscaledDeltaTime;
            if (sinceReapply < ReapplyInterval) {
                return;
            }

            sinceReapply = 0f;
            ReapplyWorld();
            ReapplyMechanic();
        }

        public void Dispose()
        {
            foreach (var handle in handles) {
                handle.Dispose();
            }

            handles.Clear();
            if (asset != null) {
                asset.msaaSampleCount = originalMsaa;
            }

            foreach (var pair in originalOutline) {
                if (pair.Key != null) {
                    pair.Key.SetActive(pair.Value);
                }
            }

            isWorldShown = true;
            isMechanicShown = true;
            ReapplyWorld();
            ReapplyMechanic();
            if (overlay != null) {
                UnityEngine.Object.Destroy(overlay);
            }
        }

#if BUGORD_SUPERCONSOLE
        private void RegisterWatches()
        {
            // Read-only readouts. The frame ones average over the sampling interval, the worst frame over a second.
            handles.Add(DevConsole.Watch("perf.fps", FramesPerSecond, "fps", null, WatchInterval));
            handles.Add(DevConsole.Watch("perf.frame_ms", AverageFrameMs, "ms", null, WatchInterval));
            handles.Add(DevConsole.Watch("perf.frame_ms_max", WorstFrameMs, "ms", null, WatchInterval));
            handles.Add(DevConsole.Watch("perf.resolution", Resolution, null, null, WatchInterval));

            // Settings, edited in place (Pro).
            handles.Add(DevConsole.Watch("perf.overlay", IsOverlayOn, null, value => SetOverlay(value), WatchInterval));
            handles.Add(DevConsole.Watch("perf.render_scale", () => scaler.Override, "x (0 = auto)",
                value => scaler.Override = value, WatchInterval));
            handles.Add(DevConsole.Watch("perf.msaa", () => asset != null ? asset.msaaSampleCount : 1, null,
                value => SetMsaa(value), WatchInterval));
            handles.Add(DevConsole.Watch("perf.outline", IsOutlineOn, null, value => SetOutline(value), WatchInterval));
            handles.Add(DevConsole.Watch("perf.fog", () => RenderSettings.fog, null, value => RenderSettings.fog = value,
                WatchInterval));
            handles.Add(DevConsole.Watch("perf.world", () => isWorldShown, null, value => SetWorld(value), WatchInterval));
            handles.Add(DevConsole.Watch("perf.mechanic", () => isMechanicShown, null, value => SetMechanic(value),
                WatchInterval));

#if !UNITY_EDITOR
            // Every gameplay number, editable on the phone. Players only: in the editor an edit would change the
            // ScriptableObject asset in memory and stay there for later Play sessions.
            if (tuning != null) {
                handles.Add(DevConsole.Watch("tuning", () => tuning));
            }
#endif
        }

        private void RegisterCommands()
        {
            handles.Add(CommandRegistry.Register("perf_overlay", "Show or hide the frame readout in the corner (saved): 0 hide, 1 show",
                (Func<int, string>)(on => {
                    SetOverlay(on != 0);
                    return $"perf overlay {(IsOverlayOn() ? "shown" : "hidden")} (saved)";
                })));
            handles.Add(CommandRegistry.Register("perf_scale", "Override the 3D render scale (0 = back to automatic)",
                (Func<float, string>)(scale => {
                    scaler.Override = scale;
                    scaler.Tick();
                    return $"render scale {scaler.Scale:0.00}{(scale > 0f ? " (forced)" : " (automatic)")}, 3D {Resolution()}";
                })));
            handles.Add(CommandRegistry.Register("perf_msaa", "Set the MSAA sample count: 1, 2 or 4",
                (Func<int, string>)(samples => asset == null ? "no URP asset"
                    : samples is 1 or 2 or 4 ? $"msaa {SetMsaa(samples)}" : "use 1, 2 or 4")));
            handles.Add(CommandRegistry.Register("perf_outline", "Switch the outline pass: 0 off, 1 on",
                (Func<int, string>)(on => SetOutline(on != 0) ? $"outline {(on != 0 ? "on" : "off")}" : "no outline feature found")));
            handles.Add(CommandRegistry.Register("perf_fog", "Switch the fog (until the next round loads): 0 off, 1 on",
                (Func<int, string>)(on => {
                    RenderSettings.fog = on != 0;
                    return $"fog {(RenderSettings.fog ? "on" : "off")}";
                })));
            handles.Add(CommandRegistry.Register("perf_world", "Show or hide the world below the arena and its decor: 0 hide, 1 show",
                (Func<int, string>)(on => {
                    SetWorld(on != 0);
                    return $"world and decor {(isWorldShown ? "shown" : "hidden")}";
                })));
            handles.Add(CommandRegistry.Register("perf_mech", "Show or hide the variant mechanic's visuals (ball, candy, turntable): 0 hide, 1 show",
                (Func<int, string>)(on => {
                    SetMechanic(on != 0);
                    return $"mechanic visuals {(isMechanicShown ? "shown" : "hidden")}";
                })));
        }
#endif

        private float FramesPerSecond()
        {
            return stats.Measure(WatchInterval, out var average, out _) ? Mathf.Round(1f / average) : 0f;
        }

        private float AverageFrameMs()
        {
            return stats.Measure(WatchInterval, out var average, out _) ? Mathf.Round(average * 10000f) / 10f : 0f;
        }

        private float WorstFrameMs()
        {
            return stats.Measure(1f, out _, out var worst) ? Mathf.Round(worst * 10000f) / 10f : 0f;
        }

        private string Resolution()
        {
            var size = scaler.RenderSize;
            return $"{size.x}x{size.y}";
        }

        private bool IsOverlayOn()
        {
            return overlayView != null && overlayView.enabled;
        }

        private void SetOverlay(bool shown)
        {
            if (overlayView != null) {
                overlayView.enabled = shown;
            }

            storage.SetBool(OverlayKey, shown);
        }

        private int SetMsaa(int samples)
        {
            if (asset == null) {
                return 1;
            }

            // Only the counts URP takes on a phone: anything else rounds down to one of them.
            asset.msaaSampleCount = samples >= 4 ? 4 : samples >= 2 ? 2 : 1;
            return asset.msaaSampleCount;
        }

        private bool IsOutlineOn()
        {
            outlineFeatures ??= Resources.FindObjectsOfTypeAll<OutlineFeature>();
            return outlineFeatures.Length > 0 && outlineFeatures[0].isActive;
        }

        private bool SetOutline(bool on)
        {
            // Found in memory, not through the renderer: the feature objects are loaded with the renderer asset in a player
            // too, and the renderer does not publish its feature list.
            outlineFeatures ??= Resources.FindObjectsOfTypeAll<OutlineFeature>();
            foreach (var feature in outlineFeatures) {
                originalOutline.TryAdd(feature, feature.isActive);
                feature.SetActive(on);
            }

            return outlineFeatures.Length > 0;
        }

        private void SetWorld(bool shown)
        {
            isWorldShown = shown;
            ReapplyWorld();
        }

        private void SetMechanic(bool shown)
        {
            isMechanicShown = shown;
            ReapplyMechanic();
        }

        private void ReapplyWorld()
        {
            if (!isWorldShown) {
                // Only active objects are found, so a root already hidden is kept in the list; a new round brings new ones.
                foreach (var name in WorldRoots) {
                    var found = GameObject.Find(name);
                    if (found != null && !worldObjects.Contains(found)) {
                        worldObjects.Add(found);
                    }
                }
            }

            for (var i = worldObjects.Count - 1; i >= 0; i--) {
                if (worldObjects[i] == null) {
                    worldObjects.RemoveAt(i);
                    continue;
                }

                worldObjects[i].SetActive(isWorldShown);
            }

            if (isWorldShown) {
                worldObjects.Clear();
            }
        }

        private void ReapplyMechanic()
        {
            if (!isMechanicShown) {
                foreach (var name in MechanicRoots) {
                    var root = GameObject.Find(name);
                    if (root == null) {
                        continue;
                    }

                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
                        if (!mechanicRenderers.Contains(renderer)) {
                            mechanicRenderers.Add(renderer);
                        }
                    }
                }
            }

            for (var i = mechanicRenderers.Count - 1; i >= 0; i--) {
                if (mechanicRenderers[i] == null) {
                    mechanicRenderers.RemoveAt(i);
                    continue;
                }

                mechanicRenderers[i].enabled = isMechanicShown;
            }

            if (isMechanicShown) {
                mechanicRenderers.Clear();
            }
        }
    }
}
#endif
