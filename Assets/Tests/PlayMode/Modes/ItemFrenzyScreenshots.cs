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
    /// Pictures of ITEM FRENZY for a person to look at, in English and Russian, 1080×1920 (touch) and 1920×1080 (mouse):
    /// the HUD of a round of it with the boxes on the floor (<c>Logs/item-frenzy/hud_&lt;state&gt;_&lt;language&gt;_&lt;w&gt;x&lt;h&gt;.png</c>:
    /// the empty slot as the round starts, an item in it with the hint), the lobby before it
    /// (<c>Logs/lobby/frenzy_&lt;language&gt;_ready_*.png</c>) and the menu with it as the event of the day
    /// (<c>Logs/menu/[ru-]event-item-frenzy*.png</c>). The result of a round of it is a plain round's. Run by name,
    /// without -nographics; it asserts nothing about the layout.
    /// </summary>
    [Explicit("Writes screenshots of ITEM FRENZY for a visual check; run it by name.")]
    public sealed class ItemFrenzyScreenshots : SimulationSceneFixture
    {
        private const int SettleFrames = 12;
        private const float SettleSeconds = 0.6f;
        private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;

        private static readonly (int Width, int Height, bool Touch)[] Sizes = {
            (1920, 1080, false), (1080, 1920, true), (2340, 1080, true),
        };
        private static readonly string[] Languages = { "en", "ru" };
        // The HUD's hint also in the languages with the longest words for it.
        private static readonly string[] HudLanguages = { "en", "ru", "fr", "de", "tr" };

        [UnityTest]
        public IEnumerator CaptureTheHud()
        {
            var tuning = LoadTuning();
            UseSpecial(SpecialRound.ItemFrenzy);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 3 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var pickups = container.Resolve<ArenaPickups>();
            var mode = container.Resolve<ItemFrenzyMode>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            // A visit in which the player has not used an item in the mode yet.
            typeof(HudView).Assembly.GetType("ToyCarArena.UI.ItemFrenzyHud").GetMethod("ForgetVisit", Hidden)
                .Invoke(null, null);
            // The floor holds for the pictures: five languages on three screens outlast the first wave, and by the last
            // screen the cars stood over nothing. In memory only, back in the finally.
            var arena = container.Resolve<GameTuning.ArenaSettings>();
            var crumbleDelay = typeof(GameTuning.ArenaSettings).GetField("crumbleDelay", BindingFlags.Instance | BindingFlags.NonPublic);
            var keptDelay = crumbleDelay.GetValue(arena);
            crumbleDelay.SetValue(arena, 1e6f);
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
            // A player who saw the stock hint in another visit: in the mode it shows all the same.
            var controls = new HudControls { HasUsedItem = true };
            var model = new HudViewModel(null, controls);
            model.SetPayload(new HudRound(round, container.Resolve<IEnumerable<ITickable>>().OfType<PlayerDriver>().Single(),
                tuning.Controls, false, 3, false, special: SpecialRound.ItemFrenzy, playerRound: 9, mode: mode));
            var view = new HudView(controls, container.Resolve<IAudioService>());
            view.Bind(model);
            var hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Hud.uxml").Instantiate();
            hud.style.position = Position.Absolute;
            hud.style.left = 0;
            hud.style.top = 0;
            hud.style.right = 0;
            hud.style.bottom = 0;
            var camera = Camera.main;
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "item-frenzy");
            Directory.CreateDirectory(directory);
            var textures = new List<Object> { panelSettings, host };
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            var read = typeof(ItemScreenshots).GetMethod("Read", Hidden);
            var compose = typeof(ItemScreenshots).GetMethod("Compose", Hidden);
            try {
                document.rootVisualElement.Add(hud);
                view.Attach(hud);
                view.ShowAsync(CancellationToken.None).Forget();
                for (var frame = 0; frame < 10; frame++) {
                    yield return null;
                }

                var starts = round.Cars.ToDictionary(car => car, car => (car.Position, car.transform.rotation));
                var hint = hud.Q<VisualElement>("item-hint");
                var touchSeen = typeof(HudControls).GetField("isTouchSeen", BindingFlags.Instance | BindingFlags.NonPublic);
                var clearItem = typeof(Car).GetMethod("ClearItem", BindingFlags.Instance | BindingFlags.NonPublic);
                var removeAll = typeof(ArenaPickups).GetMethod("RemoveAll", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var (width, height, touch) in Sizes) {
                    // A desktop is a desktop until a finger touches it; the touch frames come after.
                    touchSeen.SetValue(controls, false);
                    if (touch) {
                        controls.BeginTouchSteering(Vector2.zero);
                    }

                    // The boxes where the mode laid them as the round started, one in front of each car: as laid for the
                    // first screen, laid again for the next where a slot is free (a box takes its slot with it until it has
                    // shrunk away, and the round lays its own meanwhile).
                    Assert.That(mode.Opening.Count, Is.GreaterThanOrEqualTo(round.Cars.Count - 1));
                    if (width != Sizes[0].Width) {
                        removeAll.Invoke(pickups, null);
                        var freed = Time.realtimeSinceStartup + 1.5f;
                        while (Time.realtimeSinceStartup < freed) {
                            // Every car back where it started the round, at rest.
                            foreach (var car in round.Cars) {
                                car.Movement.Place(starts[car].Position, starts[car].rotation);
                                car.Movement.Drive(Vector3.zero, 0f);
                            }

                            yield return null;
                        }

                        foreach (var spot in mode.Opening) {
                            pickups.Spawn(spot);
                        }
                    }

                    foreach (var state in new[] { "empty", "item" }) {
                        clearItem.Invoke(round.Player, null);
                        if (state == "item") {
                            Assert.That(pickups.Give(round.Player, ItemKind.Bomb), Is.True);
                        }

                        foreach (var language in HudLanguages) {
                            Strings.SetLanguage(language);
                            // Put the hint up afresh, in this language and for this screen.
                            hint.RemoveFromClassList("hud__item-hint--shown");
                            var scene = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                            var interface_ = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                            textures.Add(scene);
                            textures.Add(interface_);
                            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Frenzy screenshot");
                            camera.targetTexture = scene;
                            panelSettings.targetTexture = interface_;
                            var settled = Time.realtimeSinceStartup + SettleSeconds;
                            for (var frame = 0; frame < SettleFrames || Time.realtimeSinceStartup < settled; frame++) {
                                // The cars stand still for the picture.
                                foreach (var car in round.Cars) {
                                    car.Movement.Drive(Vector3.zero, 0f);
                                }

                                yield return null;
                            }

                            camera.Render();
                            var picture = (Texture2D)compose.Invoke(null, new object[] {
                                read.Invoke(null, new object[] { scene }), read.Invoke(null, new object[] { interface_ }),
                            });
                            var path = Path.Combine(directory, $"hud_{state}_{language}_{width}x{height}.png");
                            File.WriteAllBytes(path, picture.EncodeToPNG());
                            Object.DestroyImmediate(picture);
                            Debug.Log($"Frenzy screenshot: {path}");
                        }
                    }
                }
            }
            finally {
                crumbleDelay.SetValue(arena, keptDelay);
                camera.targetTexture = null;
                PlayModeWindow.SetCustomRenderingResolution((uint)screenWidth, (uint)screenHeight, "Frenzy screenshot");
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

        /// <summary>The lobby before a round of the mode, its card under the arena's pill.</summary>
        [UnityTest]
        public IEnumerator CaptureTheLobby()
        {
            var capture = typeof(LobbyScreenshots).GetMethod("Capture", Hidden);
            foreach (var language in Languages) {
                yield return (IEnumerator)capture.Invoke(null,
                    new object[] { SpecialRound.ItemFrenzy, language, $"frenzy_{language}", new float[0], 1 });
            }
        }

        /// <summary>The menu with the mode as the event of the day: its card in CHOOSE MODE, the tile and the MODE plate.</summary>
        [UnityTest]
        public IEnumerator CaptureTheMenu()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "menu");
            Directory.CreateDirectory(directory);
            var capture = typeof(MenuScreenshots).GetMethod("Capture", Hidden);
            EventSchedule.Forced = EventMode.ItemFrenzy;
            try {
                foreach (var language in Languages) {
                    Strings.SetLanguage(language);
                    var prefix = language == "en" ? "" : language + "-";
                    foreach (var (width, height, touch) in new[] { (1080, 1920, true), (1920, 1080, false) }) {
                        yield return (IEnumerator)capture.Invoke(null, new object[] {
                            directory, $"{prefix}event-item-frenzy", (width, height, touch), false, 540, 0, false, 0, 0,
                            true, 0,
                        });
                    }
                }
            }
            finally {
                EventSchedule.Forced = null;
                Strings.SetLanguage("en");
                typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            }
        }
    }
}
#endif
