#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Pictures of the kick-off before a match, at 3 and at GO!, on a phone held upright and on its side, for a person to
    /// look at: the arena and the HUD are drawn into textures of each size and laid over each other into Logs/countdown,
    /// as in <see cref="ScoreboardScreenshots"/>. Run by name, without -nographics; it asserts nothing about the layout.
    /// </summary>
    [Explicit("Writes screenshots of the kick-off countdown for a visual check; run it by name.")]
    public sealed class KickoffScreenshots : SimulationSceneFixture
    {
        private const int SettleFrames = 12;
        private const float SettleSeconds = 0.6f;

        // Into each step: the number has sprung in and holds; GO! has sprung in with its burst still flying out.
        private const float NumberAge = 0.32f;
        private const float GoAge = 0.2f;

        private const int ShotFrames = 4;

        private static readonly PropertyInfo StepTime = typeof(HudViewModel).GetProperty(nameof(HudViewModel.KickoffStepTime));

        private static readonly (int Width, int Height)[] Sizes = { (1080, 1920), (2340, 1080) };

        [UnityTest]
        public IEnumerator CaptureTheKickoff()
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 120f);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            // The cars stand on the arena, the round not begun: as under the kick-off.
            round.Place();

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
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "countdown");
            Directory.CreateDirectory(directory);
            var textures = new List<Object> { panelSettings, host };
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            try {
                document.rootVisualElement.Add(hud);
                view.Attach(hud);
                view.ShowAsync(CancellationToken.None).Forget();

                foreach (var (width, height) in Sizes) {
                    var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    var interface_ = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    textures.Add(scene);
                    textures.Add(interface_);
                    PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Kickoff screenshot");
                    camera.targetTexture = scene;
                    panelSettings.targetTexture = interface_;
                    var settled = Time.realtimeSinceStartup + SettleSeconds;
                    for (var frame = 0; frame < SettleFrames || Time.realtimeSinceStartup < settled; frame++) {
                        yield return null;
                    }

                    foreach (var (step, age, name) in new[] { (3, NumberAge, "3"), (0, GoAge, "go") }) {
                        model.ShowKickoff(step, step == 0 ? tuning.Round.KickoffGo : tuning.Round.KickoffStep);
                        // Frames take long here and the panel draws after the test resumes: hold the step at its age
                        // over a few frames, so the picture is of that moment however long they take.
                        for (var frame = 0; frame < ShotFrames; frame++) {
                            StepTime.SetValue(model, Time.unscaledTime - age);
                            yield return null;
                        }

                        camera.Render();
                        var picture = Compose(Read(scene), Read(interface_));
                        var path = Path.Combine(directory, $"kickoff_{name}_{width}x{height}.png");
                        File.WriteAllBytes(path, picture.EncodeToPNG());
                        Object.DestroyImmediate(picture);
                        Debug.Log($"Kickoff screenshot: {path}");
                    }

                    model.ShowKickoff(HudViewModel.NoKickoff, 0f);
                }
            }
            finally {
                camera.targetTexture = null;
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Kickoff screenshot");
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

        // The HUD over the arena by its alpha.
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
    }
}
#endif
