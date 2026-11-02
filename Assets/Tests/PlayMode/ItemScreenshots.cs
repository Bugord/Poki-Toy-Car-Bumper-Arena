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
            model.SetPayload(new HudRound(
                round, container.Resolve<IEnumerable<ITickable>>().OfType<PlayerDriver>().Single(), tuning.Controls, false, 7,
                false));
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

        /// <summary>
        /// The first round's boost nudge on a desktop, SPACE OR CLICK! over the gauge, with a bot beside the player:
        /// Logs/items/boost_hint_1920x1080.png.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureTheDesktopBoostHint()
        {
            var tuning = LoadTuning();
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 1 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            round.Place();
            round.Begin();
            var player = round.Player.transform;
            round.Cars[1].Movement.Place(player.position + player.right * 4f, player.rotation);

            var panelSettings = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
            panelSettings.clearColor = true;
            panelSettings.colorClearValue = Color.clear;
            var host = new GameObject("Screenshot HUD");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var controls = new HudControls();
            var model = new HudViewModel(null, controls);
            model.SetPayload(new HudRound(
                round, container.Resolve<IEnumerable<ITickable>>().OfType<PlayerDriver>().Single(), tuning.Controls, true, 1,
                false));
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
            var scene = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var interface_ = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            try {
                document.rootVisualElement.Add(hud);
                view.Attach(hud);
                view.ShowAsync(CancellationToken.None).Forget();
                PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "Boost hint screenshot");
                camera.targetTexture = scene;
                panelSettings.targetTexture = interface_;
                var settled = Time.realtimeSinceStartup + 1f;
                while (Time.realtimeSinceStartup < settled) {
                    yield return null;
                }

                Assert.That(model.CanHintBoost, Is.True, "the bot beside the player should bring the nudge up");
                camera.Render();
                var picture = Compose(Read(scene), Read(interface_));
                File.WriteAllBytes(Path.Combine(directory, "boost_hint_1920x1080.png"), picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
            }
            finally {
                camera.targetTexture = null;
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Boost hint screenshot");
                view.Dispose();
                Object.DestroyImmediate(scene);
                Object.DestroyImmediate(interface_);
                Object.Destroy(panelSettings);
                Object.Destroy(host);
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }

        /// <summary>
        /// The items of v0.9 at work on the arena, in Logs/items/items_*.png at 1920×1080 and 1080×1920: the player's car
        /// grown big by a mega, a bowling ball rolling at a bot, a balloon over a bot, an oil slick with a bot's oily tracks
        /// across it, and the slot holding each new item in turn.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureTheNewItemsAtWork()
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
            model.SetPayload(new HudRound(
                round, container.Resolve<IEnumerable<ITickable>>().OfType<PlayerDriver>().Single(), tuning.Controls, false, 7,
                false));
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
            var cars = round.Cars;
            try {
                document.rootVisualElement.Add(hud);
                view.Attach(hud);
                view.ShowAsync(CancellationToken.None).Forget();

                // The player's car grows; a bot with a balloon; the player spills oil and a bot drives through it.
                var player = round.Player;
                player.Movement.Place(new Vector3(0f, 0.2f, -6f), Quaternion.Euler(0f, 20f, 0f));
                cars[1].Movement.Place(new Vector3(-7f, 0.2f, 3f), Quaternion.Euler(0f, 120f, 0f));
                cars[2].Movement.Place(new Vector3(-3f, 0.2f, -10f), Quaternion.Euler(0f, 90f, 0f));
                cars[3].Movement.Place(new Vector3(4.5f, 0.2f, 6f), Quaternion.Euler(0f, 200f, 0f));
                for (var i = 4; i < cars.Count; i++) {
                    cars[i].Movement.Place(new Vector3(8f, 0.2f, -2f + (i - 4) * 3.2f), Quaternion.Euler(0f, 270f, 0f));
                }

                for (var frame = 0; frame < 20; frame++) {
                    yield return null;
                }

                pickups.Give(cars[1], ItemKind.Balloon);
                pickups.Use(cars[1]);
                pickups.Give(player, ItemKind.Oil);
                pickups.Use(player);
                cars[2].Movement.Drive(Vector3.right, 1f);
                for (var frame = 0; frame < 60; frame++) {
                    yield return null;
                }

                cars[2].Movement.Drive(Vector3.zero, 0f);
                pickups.Give(player, ItemKind.Mega);
                pickups.Use(player);
                for (var frame = 0; frame < 25; frame++) {
                    yield return null;
                }

                // A bot rolls a ball across the floor towards the far cars.
                cars[3].transform.rotation = Quaternion.Euler(0f, 250f, 0f);
                pickups.Give(cars[3], ItemKind.Bowling);
                pickups.Use(cars[3]);
                Time.timeScale = 0.15f;
                for (var frame = 0; frame < 6; frame++) {
                    yield return null;
                }

                var held = new[] { ItemKind.Bowling, ItemKind.Balloon, ItemKind.Oil, ItemKind.Mega };
                var shots = new[] { (1920, 1080), (1080, 1920) };
                for (var shot = 0; shot < held.Length; shot++) {
                    pickups.Give(player, held[shot]);
                    var (width, height) = shots[shot % shots.Length];
                    var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    var interface_ = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    textures.Add(scene);
                    textures.Add(interface_);
                    PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Item screenshot");
                    camera.targetTexture = scene;
                    panelSettings.targetTexture = interface_;
                    for (var frame = 0; frame < SettleFrames; frame++) {
                        yield return null;
                    }

                    camera.Render();
                    var picture = Compose(Read(scene), Read(interface_));
                    var path = Path.Combine(directory, $"items_{held[shot].ToString().ToLowerInvariant()}_{width}x{height}.png");
                    File.WriteAllBytes(path, picture.EncodeToPNG());
                    Object.DestroyImmediate(picture);
                    Debug.Log($"Item screenshot: {path}");
                    // Empties the slot for the next one.
                    typeof(Gameplay.Cars.Car).GetMethod("ClearItem", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(player, null);
                }
            }
            finally {
                Time.timeScale = 1f;
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
