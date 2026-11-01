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
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
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
    /// Pictures of the match HUD mid-match on phone and desktop screen shapes, for a person to look at: the arena and
    /// the HUD are drawn into textures of each size (a batch-mode editor has no screen to resize) and laid over each other
    /// into Logs/scoreboard. Run by name, without -nographics; it asserts nothing about the layout.
    /// </summary>
    [Explicit("Writes screenshots of the match HUD for a visual check; run it by name.")]
    public sealed class ScoreboardScreenshots : SimulationSceneFixture
    {
        private const int FramesPerSecond = 60;
        private const int SettleFrames = 12;
        private const float SettleSeconds = 0.6f;

        private static readonly PropertyInfo LastHitByProperty = typeof(Car).GetProperty(nameof(Car.LastHitBy));
        private static readonly PropertyInfo LastHitTimeProperty = typeof(Car).GetProperty(nameof(Car.LastHitTime));

        // Desktop shapes first: once the HUD has seen a finger it keeps its touch buttons.
        private static readonly (int Width, int Height, bool Touch)[] Sizes =
        {
            (1920, 1080, false), (1920, 1200, false), (2048, 1536, false), (1080, 1920, true), (1080, 2340, true), (2340, 1080, true),
        };

        [UnityTest]
        public IEnumerator CaptureTheMatchHud()
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 120f);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            FreezeBots(container);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            round.Place();
            round.Begin();

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
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "scoreboard");
            Directory.CreateDirectory(directory);
            var textures = new List<Object> { panelSettings, host };
            // The editor's rendering size is put back afterwards, so an open editor's Game view is left as it was.
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            try {
                document.rootVisualElement.Add(hud);
                view.Attach(hud);
                view.ShowAsync(CancellationToken.None).Forget();

                // Mid-match: one bot leads, the player has a knock-out, and three cars come back from falls.
                yield return KnockOff(round.Cars[1], round.Cars[2]);
                yield return KnockOff(round.Cars[4], round.Cars[2]);
                yield return KnockOff(round.Cars[5], round.Player);
                yield return Frames(tuning.Match.RespawnDelay + 0.5f);
                // Then a bot knocks the player off: it becomes the REVENGE target and the player waits to come back.
                yield return KnockOff(round.Cars[6], round.Cars[3]);
                yield return KnockOff(round.Player, round.Cars[3]);

                foreach (var (width, height, touch) in Sizes) {
                    if (touch) {
                        controls.BeginTouchSteering(Vector2.zero);
                    }

                    var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    var interface_ = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    textures.Add(scene);
                    textures.Add(interface_);
                    // The HUD projects world points through the screen's height, so the editor's rendering size is set
                    // to the picture's too where the editor allows it.
                    PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Scoreboard screenshot");
                    camera.targetTexture = scene;
                    panelSettings.targetTexture = interface_;
                    // The rows slide and fade on the panel's real-time clock: wait out the transitions in real time.
                    var settled = Time.realtimeSinceStartup + SettleSeconds;
                    for (var frame = 0; frame < SettleFrames || Time.realtimeSinceStartup < settled; frame++) {
                        yield return null;
                    }

                    Debug.Log($"Scoreboard screenshot {width}x{height}: screen {Screen.width}x{Screen.height}");

                    camera.Render();
                    var picture = Compose(Read(scene), Read(interface_));
                    var path = Path.Combine(directory, $"hud_{width}x{height}.png");
                    File.WriteAllBytes(path, picture.EncodeToPNG());
                    Object.DestroyImmediate(picture);
                    Debug.Log($"Scoreboard screenshot: {path}");
                }
            }
            finally {
                camera.targetTexture = null;
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Scoreboard screenshot");
                view.Dispose();
                foreach (var texture in textures) {
                    if (texture != null) {
                        Object.DestroyImmediate(texture);
                    }
                }

                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
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

        private static IEnumerator Frames(float seconds)
        {
            for (var frame = 0; frame < Mathf.CeilToInt(seconds * FramesPerSecond); frame++) {
                yield return null;
            }
        }

        private static void FreezeBots(IObjectResolver container)
        {
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
        }

        private static IEnumerator KnockOff(Car car, Car by)
        {
            car.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            LastHitByProperty.SetValue(car, by);
            LastHitTimeProperty.SetValue(car, Time.time);
            for (var frame = 0; frame < 5 * FramesPerSecond && !car.IsOut; frame++) {
                yield return null;
            }

            Assert.That(car.IsOut, Is.True, "The pushed car never fell.");
        }
    }
}
#endif
