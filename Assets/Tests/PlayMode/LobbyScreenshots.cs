using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Pictures of the lobby on the curtain between rounds as it searches for players, at moments of the search and
    /// once ready, in portrait and landscape: <c>Logs/lobby/lobby_&lt;moment&gt;_&lt;w&gt;x&lt;h&gt;.png</c>. Explicit:
    /// it is for looking at, and needs graphics.
    /// </summary>
    [Explicit]
    public sealed class LobbyScreenshots
    {
        [UnityTest]
        public IEnumerator CaptureTheSearchForPlayers()
        {
            Strings.Load(new StringTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Localization/strings.csv").text));
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var roster = new SessionRoster(catalog, new FirstPlayerCar(catalog));
            roster.ForRound(6);
            // A round played, so a couple of seats change hands, as after NEXT MATCH.
            roster.NextRound(6, 6, RoundResult.Nobody, tuning.Players);

            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "lobby");
            Directory.CreateDirectory(directory);
            var shots = new[] { (1080, 1920), (1920, 1080) };
            var moments = new[] { 0.15f, 0.6f, 1.0f, 1.4f };
            foreach (var (width, height) in shots) {
                var panelSettings = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ui_panel_settings.asset"));
                var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                panelSettings.targetTexture = target;
                var host = new GameObject("Lobby screenshot");
                var document = host.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                var model = new CurtainViewModel();
                var view = new CurtainView();
                try {
                    var curtain = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/Curtain.uxml").Instantiate();
                    curtain.style.position = Position.Absolute;
                    curtain.style.left = 0;
                    curtain.style.top = 0;
                    curtain.style.right = 0;
                    curtain.style.bottom = 0;
                    document.rootVisualElement.Add(curtain);
                    view.Bind(model);
                    view.Attach(curtain);
                    view.ShowAsync(CancellationToken.None).Forget();
                    model.ShowLobby(roster, 6, 3, "SANDBOX", true);
                    foreach (var moment in moments) {
                        while (Time.realtimeSinceStartupAsDouble - model.StartedAt < moment) {
                            yield return null;
                        }

                        Save(target, Path.Combine(directory, $"lobby_{moment:0.00}_{width}x{height}.png"));
                    }

                    yield return model.Ready(CancellationToken.None).ToCoroutine();
                    yield return null;
                    yield return null;
                    Save(target, Path.Combine(directory, $"lobby_ready_{width}x{height}.png"));
                }
                finally {
                    view.Dispose();
                    Object.Destroy(host);
                    Object.DestroyImmediate(target);
                    Object.Destroy(panelSettings);
                }
            }

            roster.Dispose();
            typeof(Strings).GetField("table", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
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
            Debug.Log($"Lobby screenshot: {path}");
        }
    }
}
