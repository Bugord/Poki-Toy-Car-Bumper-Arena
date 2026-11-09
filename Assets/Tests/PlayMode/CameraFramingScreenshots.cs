#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Pictures of how the arena camera frames each arena, for a person to look at: the player's car stands at its start
    /// and then near the right, near and far rims, on phone and desktop screen shapes, and the arena with the HUD over it
    /// goes to Logs/camera/&lt;arena&gt;_&lt;place&gt;_&lt;w&gt;x&lt;h&gt;.png. Run by name, without -nographics; it asserts
    /// nothing about the framing.
    /// </summary>
    [Explicit("Writes screenshots of the camera's framing for a visual check; run it by name.")]
    public sealed class CameraFramingScreenshots : SimulationSceneFixture
    {
        // Frames of 1/60 s the camera is given to ease onto a new place: its zoom and pan take under a second.
        private const int SettleFrames = 100;
        // How far inside the rim the car stands for the rim pictures, in metres.
        private const float RimInset = 2.5f;

        // Desktop shapes first: once the HUD has seen a finger it keeps its touch buttons.
        private static readonly (int Width, int Height, bool Touch)[] Sizes =
        {
            (1920, 1080, false), (1080, 1920, true), (2340, 1080, true),
        };

        private static readonly string[] Arenas = { "sandbox", "tabletop", "candy", "playroom" };

        [UnityTest]
        public IEnumerator CaptureTheFraming([ValueSource(nameof(Arenas))] string arena)
        {
            var tuning = UseMode(GameTuning.GameMode.LastStanding);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, arena);
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var map = container.Resolve<ArenaMap>();
            FreezeBots(container);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            // Placed but not begun: the floor does not crumble, so every picture shows the whole arena.
            round.Place();
            var start = round.Player.Position;
            var places = new (string Name, Vector3 Position)[]
            {
                ("start", start),
                ("right", AtRim(map, Vector3.right)),
                ("near", AtRim(map, Vector3.back)),
                ("far", AtRim(map, Vector3.forward)),
            };

            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            panelSettings.clearColor = true;
            panelSettings.colorClearValue = Color.clear;
            var host = new GameObject("Screenshot HUD");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            var controls = new HudControls();
            var model = new HudViewModel(null, controls);
            model.SetPayload(new HudRound(round, null, tuning.Controls, false, 7, false));
            var view = new HudView(controls, container.Resolve<IAudioService>());
            view.Bind(model);
            var hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Hud.uxml").Instantiate();
            hud.style.position = Position.Absolute;
            hud.style.left = 0;
            hud.style.top = 0;
            hud.style.right = 0;
            hud.style.bottom = 0;
            var camera = Camera.main;
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "camera");
            Directory.CreateDirectory(directory);
            var textures = new List<Object> { panelSettings, host };
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            var captureDeltaTime = Time.captureDeltaTime;
            try {
                Time.captureDeltaTime = 1f / 60f;
                document.rootVisualElement.Add(hud);
                view.Attach(hud);
                view.ShowAsync(CancellationToken.None).Forget();

                foreach (var (width, height, touch) in Sizes) {
                    if (touch) {
                        controls.BeginTouchSteering(Vector2.zero);
                    }

                    var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    var interface_ = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    textures.Add(scene);
                    textures.Add(interface_);
                    PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Camera screenshot");
                    camera.targetTexture = scene;
                    panelSettings.targetTexture = interface_;

                    foreach (var (name, position) in places) {
                        round.Player.Movement.Place(position, Quaternion.LookRotation(-position.normalized));
                        for (var frame = 0; frame < SettleFrames; frame++) {
                            yield return null;
                        }

                        camera.Render();
                        var picture = Compose(Read(scene), Read(interface_));
                        var path = Path.Combine(directory, $"{arena}_{name}_{width}x{height}.png");
                        File.WriteAllBytes(path, picture.EncodeToPNG());
                        Object.DestroyImmediate(picture);
                        Debug.Log($"Camera screenshot: {path} (camera {camera.transform.position})");
                    }
                }
            }
            finally {
                Time.captureDeltaTime = captureDeltaTime;
                camera.targetTexture = null;
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Camera screenshot");
                view.Dispose();
                foreach (var texture in textures) {
                    if (texture != null) {
                        Object.DestroyImmediate(texture);
                    }
                }

                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }

        private static Vector3 AtRim(ArenaMap map, Vector3 direction)
        {
            return direction * (map.RimRadius(direction) - RimInset) + Vector3.up * 0.05f;
        }

        private static Texture2D Read(RenderTexture source)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        // The HUD over the arena by its alpha; the arena's own alpha does not matter.
        private static Texture2D Compose(Texture2D scene, Texture2D overlay)
        {
            var below = scene.GetPixels32();
            var above = overlay.GetPixels32();
            for (var i = 0; i < below.Length; i++) {
                var alpha = above[i].a / 255f;
                below[i] = new Color32(
                    (byte)Mathf.RoundToInt(above[i].r * alpha + below[i].r * (1f - alpha)),
                    (byte)Mathf.RoundToInt(above[i].g * alpha + below[i].g * (1f - alpha)),
                    (byte)Mathf.RoundToInt(above[i].b * alpha + below[i].b * (1f - alpha)),
                    255);
            }

            scene.SetPixels32(below);
            scene.Apply();
            Object.DestroyImmediate(overlay);
            return scene;
        }

        private static void FreezeBots(IObjectResolver container)
        {
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
        }
    }
}
#endif
