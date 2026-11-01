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
    /// Pictures of the match HUD with an item held, on desktop and phone screen shapes, for a person to look at: the
    /// arena and the HUD are drawn into textures of each size and laid over each other into Logs/items, as in
    /// <see cref="ScoreboardScreenshots"/>. Run by name, without -nographics; it asserts nothing about the layout.
    /// </summary>
    [Explicit("Writes screenshots of the match HUD with an item held for a visual check; run it by name.")]
    public sealed class ItemScreenshots : SimulationSceneFixture
    {
        private const int SettleFrames = 12;
        private const float SettleSeconds = 0.6f;

        private static readonly MethodInfo HideItemHint =
            typeof(HudView).GetMethod("HideItemHint", BindingFlags.Instance | BindingFlags.NonPublic);

        // Desktop first: once the HUD has seen a finger it keeps its touch buttons. A finger steering on the right half
        // moves BOOST, and the item button with it, to the left.
        private static readonly (int Width, int Height, bool Touch, bool Left)[] Sizes =
        {
            (1920, 1080, false, false), (1080, 1920, true, false), (2340, 1080, true, false), (1080, 1920, true, true),
        };

        [UnityTest]
        public IEnumerator CaptureTheHudWithAnItem()
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 120f);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var pickups = container.Resolve<ArenaPickups>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
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
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "items");
            Directory.CreateDirectory(directory);
            var textures = new List<Object> { panelSettings, host };
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            try {
                document.rootVisualElement.Add(hud);
                view.Attach(hud);
                view.ShowAsync(CancellationToken.None).Forget();

                // Mid-match: a shield is running (its badge is the timer) and a bomb waits in the slot.
                Assert.That(pickups.Give(round.Player, ItemKind.Shield), Is.True);
                for (var frame = 0; frame < 3; frame++) {
                    yield return null;
                }

                Assert.That(pickups.Use(round.Player), Is.True);
                Assert.That(pickups.Give(round.Player, ItemKind.Bomb), Is.True);
                for (var frame = 0; frame < 30; frame++) {
                    yield return null;
                }

                foreach (var (width, height, touch, left) in Sizes) {
                    if (touch) {
                        controls.BeginTouchSteering(left ? new Vector2(float.MaxValue, 0f) : Vector2.zero);
                    }

                    // The first-use hint shows once a visit and for a moment: put it up afresh for each picture.
                    HideItemHint.Invoke(view, null);
                    controls.HasShownItemHint = false;

                    var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    var interface_ = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    textures.Add(scene);
                    textures.Add(interface_);
                    PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Item screenshot");
                    camera.targetTexture = scene;
                    panelSettings.targetTexture = interface_;
                    var settled = Time.realtimeSinceStartup + SettleSeconds;
                    for (var frame = 0; frame < SettleFrames || Time.realtimeSinceStartup < settled; frame++) {
                        yield return null;
                    }

                    camera.Render();
                    var picture = Compose(Read(scene), Read(interface_));
                    var path = Path.Combine(directory, $"hud_{width}x{height}{(left ? "_left" : "")}.png");
                    File.WriteAllBytes(path, picture.EncodeToPNG());
                    Object.DestroyImmediate(picture);
                    Debug.Log($"Item screenshot: {path}");
                }
            }
            finally {
                camera.targetTexture = null;
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Item screenshot");
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
