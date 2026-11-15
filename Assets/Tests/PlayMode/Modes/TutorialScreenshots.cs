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
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Pickups;
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
    /// Pictures of the first round's tutorial for a person to look at: the HUD on each step (the sticker and the arrow)
    /// in English and Russian, 1080×1920 (touch) and 1920×1080 (mouse), in
    /// <c>Logs/tutorial/&lt;step&gt;_&lt;language&gt;_&lt;w&gt;x&lt;h&gt;.png</c>. Run by name, without -nographics; it asserts
    /// nothing about the layout.
    /// </summary>
    [Explicit("Writes screenshots of the tutorial for a visual check; run it by name.")]
    public sealed class TutorialScreenshots : SimulationSceneFixture
    {
        private const float SettleSeconds = 0.7f;
        private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;

        private static readonly (int Width, int Height, bool Touch)[] Sizes = { (1080, 1920, true), (1920, 1080, false) };
        private static readonly string[] Languages = { "en", "ru" };

        [UnityTest]
        public IEnumerator CaptureTheSteps()
        {
            var tuning = LoadTuning();
            UseSafety(null);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 1, IsNewPlayer = true }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var pickups = container.Resolve<ArenaPickups>();
            var tutorial = container.Resolve<TutorialMode>();
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            round.Place();
            yield return null;
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
            model.SetPayload(new HudRound(round, container.Resolve<IEnumerable<ITickable>>().OfType<PlayerDriver>().Single(),
                tuning.Controls, false, 1, true, playerRound: 1, mode: tutorial));
            var view = new HudView(controls, container.Resolve<IAudioService>());
            view.Bind(model);
            var hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Hud.uxml").Instantiate();
            hud.style.position = Position.Absolute;
            hud.style.left = 0;
            hud.style.top = 0;
            hud.style.right = 0;
            hud.style.bottom = 0;
            var camera = Camera.main;
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "tutorial");
            Directory.CreateDirectory(directory);
            var textures = new List<Object> { panelSettings, host };
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            var read = typeof(ItemScreenshots).GetMethod("Read", Hidden);
            var compose = typeof(ItemScreenshots).GetMethod("Compose", Hidden);
            var touchSeen = typeof(HudControls).GetField("isTouchSeen", BindingFlags.Instance | BindingFlags.NonPublic);
            try {
                document.rootVisualElement.Add(hud);
                view.Attach(hud.hierarchy[0]);
                view.ShowAsync(CancellationToken.None).Forget();
                for (var frame = 0; frame < 10; frame++) {
                    yield return null;
                }

                foreach (var (shot, step, praise) in new[] {
                             ("push", TutorialStep.Push, TutorialPraise.None),
                             ("nice-push", TutorialStep.Push, TutorialPraise.Pushed),
                             ("box", TutorialStep.Box, TutorialPraise.None),
                             ("use", TutorialStep.Use, TutorialPraise.None),
                             ("huge", TutorialStep.Use, TutorialPraise.Grown),
                             ("smash", TutorialStep.Smash, TutorialPraise.None),
                         }) {
                    // Bring the lesson to the shot by doing what the one before it asked.
                    if (shot == "nice-push") {
                        Assert.That(round.KnockOut(tutorial.Target, round.Player), Is.True);
                    }
                    else if (shot == "use") {
                        Assert.That(pickups.Give(round.Player, ItemKind.Mega), Is.True);
                    }
                    else if (shot == "huge") {
                        Assert.That(pickups.Use(round.Player), Is.True);
                    }

                    var waited = Time.realtimeSinceStartup + 4f;
                    while ((tutorial.Step != step || tutorial.Praise != praise) && Time.realtimeSinceStartup < waited) {
                        yield return null;
                    }

                    Assert.That(tutorial.Step, Is.EqualTo(step), shot);
                    Assert.That(tutorial.Praise, Is.EqualTo(praise), shot);
                    // Let the last two drop onto the floor.
                    var walked = Time.realtimeSinceStartup + (step == TutorialStep.Smash ? 1f : 0.3f);
                    while (Time.realtimeSinceStartup < walked) {
                        yield return null;
                    }

                    foreach (var (width, height, touch) in Sizes) {
                        touchSeen.SetValue(controls, false);
                        if (touch) {
                            controls.BeginTouchSteering(Vector2.zero);
                        }

                        foreach (var language in Languages) {
                            Strings.SetLanguage(language);
                            var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                            var interface_ = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                            textures.Add(scene);
                            textures.Add(interface_);
                            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Tutorial screenshot");
                            camera.targetTexture = scene;
                            panelSettings.targetTexture = interface_;
                            var settled = Time.realtimeSinceStartup + SettleSeconds;
                            while (Time.realtimeSinceStartup < settled) {
                                round.Player.Movement.Drive(Vector3.zero, 0f);
                                yield return null;
                            }

                            camera.Render();
                            var picture = (Texture2D)compose.Invoke(null, new object[] {
                                read.Invoke(null, new object[] { scene }), read.Invoke(null, new object[] { interface_ }),
                            });
                            var path = Path.Combine(directory,
                                $"{shot}_{language}_{width}x{height}.png");
                            File.WriteAllBytes(path, picture.EncodeToPNG());
                            Object.DestroyImmediate(picture);
                            Debug.Log($"Tutorial screenshot: {path}");
                        }
                    }
                }
            }
            finally {
                camera.targetTexture = null;
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Tutorial screenshot");
                view.Dispose();
                foreach (var texture in textures) {
                    if (texture != null) {
                        Object.DestroyImmediate(texture);
                    }
                }

                Strings.SetLanguage("en");
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }
    }
}
#endif
