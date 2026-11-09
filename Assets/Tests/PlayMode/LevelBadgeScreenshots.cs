using System.Collections;
using System.IO;
using System.Reflection;
using Bugord.App.UI;
using NUnit.Framework;
using ToyCarArena.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// One sheet of level badges of every rank, plain and as the REVENGE rival, in each place the game shows one: over a
    /// car, in a lobby row, in the match table, on the podium pill, the plain badge, the result card's, the level-up
    /// popup's and the track's rank stop: <c>Logs/level-badges/badges_&lt;scale&gt;.png</c>, at the reference size and at
    /// half of it (about a phone's). Explicit: it is for looking at, and needs graphics.
    /// </summary>
    [Explicit]
    public sealed class LevelBadgeScreenshots
    {
        // One level of each rank: rookie, bronze, silver, gold, legend (three digits, the widest badge).
        private static readonly int[] Levels = { 5, 18, 31, 57, 120 };

        private static readonly Color Sand = new(0.80f, 0.74f, 0.59f);
        private static readonly Color Sky = new(0.56f, 0.78f, 0.94f);
        private static readonly Color Navy = new(0.11f, 0.17f, 0.31f);

        [UnityTest]
        public IEnumerator CaptureEveryRank()
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "level-badges");
            Directory.CreateDirectory(directory);
            foreach (var scale in new[] { 1f, 0.5f }) {
                var width = Mathf.RoundToInt(2600 * scale);
                var height = Mathf.RoundToInt(2000 * scale);
                var panelSettings = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
                panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
                panelSettings.scale = scale;
                var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                panelSettings.targetTexture = target;
                var host = new GameObject("Level badge screenshot");
                var document = host.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                try {
                    var root = document.rootVisualElement;
                    root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/ui_screens.uss"));
                    root.style.backgroundColor = Sky;
                    root.style.paddingLeft = 40;
                    root.style.paddingTop = 30;
                    Build(root);
                    // As every screen keeps its stickers' round fill over their drop.
                    PillShape.Keep(root);
                    for (var frame = 0; frame < 6; frame++) {
                        yield return null;
                    }

                    Save(target, Path.Combine(directory, $"badges_{scale:0.0}.png"));
                }
                finally {
                    Object.Destroy(host);
                    Object.DestroyImmediate(target);
                    Object.Destroy(panelSettings);
                }
            }
        }

        private static void Build(VisualElement root)
        {
            // Over a car: the bot's handle on its dark pill, on the sand of the arena.
            var hud = Section(root, "HUD", Sand, 130);
            foreach (var isRival in new[] { false, true }) {
                foreach (var level in Levels) {
                    var sticker = Element("car-name__sticker");
                    sticker.style.marginRight = 30;
                    var line = Element("car-name__line");
                    line.Add(Badge(level, "car-name__level", isRival, true));
                    var name = new Label("RUSTY");
                    name.AddToClassList("display");
                    name.AddToClassList("car-name__text");
                    if (isRival) {
                        name.style.color = new Color(1f, 0.3f, 0.3f);
                    }
                    line.Add(name);
                    sticker.Add(line);
                    hud.Add(sticker);
                }
            }

            // A lobby row as the curtain builds it, on the curtain's navy.
            var lobby = Section(root, "LOBBY", Navy, 300);
            lobby.style.flexWrap = Wrap.Wrap;
            var createRow = typeof(CurtainView).GetMethod("CreateRow", BindingFlags.Static | BindingFlags.NonPublic);
            var index = 1;
            foreach (var isRival in new[] { false, true }) {
                foreach (var level in Levels) {
                    var slot = new VisualElement();
                    slot.style.width = 480;
                    slot.style.height = 122;
                    slot.style.marginRight = 24;
                    slot.style.marginBottom = 20;
                    var row = new LobbyRow(index, index % 8, "BLITZ", level, null, isRival);
                    slot.Add((VisualElement)createRow!.Invoke(null, new object[] { row, false, 0 }));
                    lobby.Add(slot);
                    index++;
                }
            }

            // The match table's row and the podium's pill, on the result's white card.
            var table = Section(root, "TABLE", Color.white, 110);
            foreach (var isRival in new[] { false, true }) {
                foreach (var level in Levels) {
                    var row = Element("results__row");
                    row.style.width = 220;
                    row.style.marginRight = 20;
                    row.Add(InSlot(Badge(level, "results__level", isRival, true), "results__level-slot"));
                    table.Add(row);
                }
            }

            var podium = Section(root, "PODIUM", Color.white, 110);
            foreach (var isRival in new[] { false, true }) {
                foreach (var level in Levels) {
                    var badge = Badge(level, "podium__level", isRival, true);
                    badge.style.marginRight = 60;
                    podium.Add(badge);
                }
            }

            var plain = Section(root, "PLAIN", Sky, 110);
            foreach (var isRival in new[] { false, true }) {
                foreach (var level in Levels) {
                    var badge = Badge(level, "sheet__plain", isRival);
                    badge.style.marginRight = 60;
                    plain.Add(badge);
                }
            }

            // The result card's badge, the level-up popup's and the track's rank stop.
            var big = Section(root, "BIG", Color.white, 260);
            foreach (var level in Levels) {
                var badge = Badge(level, "level-badge--big", false);
                badge.style.marginRight = 70;
                big.Add(badge);
            }

            foreach (var level in Levels) {
                var node = Element("track__node");
                node.AddToClassList("track__node--rank");
                node.AddToClassList("level-badge--" + RankOf(level));
                var value = new Label(level.ToString());
                value.AddToClassList("track__level");
                node.Add(value);
                typeof(HudView).Assembly.GetType("ToyCarArena.UI.LevelBadge")!
                    .GetMethod("Decorate", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, new object[] { node });
                node.style.marginRight = 70;
                big.Add(node);
            }

            var popup = Section(root, "POPUP", Color.white, 320);
            foreach (var level in Levels) {
                var badge = Badge(level, "level-badge--big", false);
                badge.AddToClassList("level-reward__rank");
                badge.style.marginRight = 70;
                popup.Add(badge);
            }
        }

        private static string RankOf(int level) =>
            level >= 100 ? "legend" : level >= 50 ? "gold" : level >= 25 ? "silver" : level >= 10 ? "bronze" : "rookie";

        private static VisualElement Badge(int level, string className, bool isRival, bool isLabelled = false)
        {
            var create = typeof(HudView).GetMethod("CreateLevelBadge", BindingFlags.Static | BindingFlags.NonPublic);
            return (VisualElement)create!.Invoke(null, new object[] { level, className, isRival, isLabelled });
        }

        private static VisualElement InSlot(VisualElement badge, string slotClass)
        {
            var wrap = typeof(HudView).GetMethod("InLevelSlot", BindingFlags.Static | BindingFlags.NonPublic);
            return (VisualElement)wrap!.Invoke(null, new object[] { badge, slotClass });
        }

        private static VisualElement Section(VisualElement root, string title, Color color, float height)
        {
            var section = new VisualElement();
            section.style.flexDirection = FlexDirection.Row;
            section.style.alignItems = Align.Center;
            section.style.minHeight = height;
            section.style.marginBottom = 16;
            section.style.paddingLeft = 30;
            section.style.paddingTop = 30;
            section.style.paddingBottom = 10;
            section.style.backgroundColor = color;
            var label = new Label(title);
            label.style.width = 150;
            label.style.fontSize = 28;
            label.style.color = color == Navy ? Color.white : Navy;
            section.Add(label);
            root.Add(section);
            return section;
        }

        private static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }

        private static void Save(RenderTexture source, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Debug.Log($"Level badge screenshot: {path}");
        }
    }
}
