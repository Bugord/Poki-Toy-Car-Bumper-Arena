using System.Collections.Generic;
using Bugord.App.Localization;
using Bugord.App.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The curtain between rounds. After NEXT MATCH it shows the coming match on it: MATCH FOUND!, the arena, the seats
    /// popping in, a loading bar and a tip, then READY! once the arena has loaded.
    /// </summary>
    public sealed class CurtainView : FadingView<CurtainViewModel>
    {
        private const float RowStep = 0.045f;
        private const float EnterDuration = 0.2f;
        private const float LeftHold = 0.12f;
        private const float LeftEnd = 0.24f;
        // The scene load reports no progress, so the bar eases towards most of the way and fills the rest on READY!.
        private const float LoadingShare = 0.9f;
        private const float LoadingTime = CurtainViewModel.LobbyDuration / 3f;
        private const float FillSpeed = 4f;

        private static readonly string[] Tips = {
            "lobby.tip.boost_hit",
            "lobby.tip.boost_edge",
            "lobby.tip.push_edge",
            "lobby.tip.crumble",
            "lobby.tip.middle",
        };

        private static readonly string[] Dots = { ".", "..", "..." };

        private readonly List<(VisualElement slot, VisualElement incoming, VisualElement outgoing)> rows = new();
        private VisualElement lobby;
        private Label title;
        private Label titleFace;
        private VisualElement arenaLine;
        private VisualElement arenaBadge;
        private VisualElement barFill;
        private Label loadingText;
        private float fill;
        private float fillTime;
        private IVisualElementScheduledItem animation;

        protected override float FadeDuration => 0.25f;

        protected override void OnAttached()
        {
            lobby = Root.Q<VisualElement>("lobby");
            title = Root.Q<Label>("lobby-title");
            titleFace = Root.Q<Label>("lobby-title-face");
            arenaLine = Root.Q<VisualElement>("lobby-arena");
            barFill = Root.Q<VisualElement>("lobby-bar-fill");
            loadingText = Root.Q<Label>("lobby-loading-text");
            ViewModel.LobbyShown += ShowLobby;
            if (ViewModel.Rows != null) {
                ShowLobby();
            }
        }

        public override void Dispose()
        {
            animation?.Pause();
            ViewModel.LobbyShown -= ShowLobby;
            base.Dispose();
        }

        private void ShowLobby()
        {
            var container = Root.Q<VisualElement>("lobby-rows");
            container.Clear();
            rows.Clear();
            lobby.style.display = DisplayStyle.Flex;
            BuildArenaLine();
            fill = 0f;
            fillTime = 0f;
            Root.Q<Label>("lobby-tip").text = Strings.Get(Tips[Random.Range(0, Tips.Length)]);
            foreach (var row in ViewModel.Rows) {
                var slot = Element("lobby__slot");
                var incoming = row.Name != null ? CreateRow(row, false) : null;
                var outgoing = row.LeftName != null ? CreateRow(row, true) : null;
                if (incoming != null) {
                    slot.Add(incoming);
                }
                if (outgoing != null) {
                    slot.Add(outgoing);
                }
                rows.Add((slot, incoming, outgoing));
                container.Add(slot);
            }
            PillShape.Keep(lobby);
            animation?.Pause();
            UpdateLobby();
            animation = Root.schedule.Execute(UpdateLobby).Every(16);
        }

        private void BuildArenaLine()
        {
            arenaLine.Clear();
            arenaBadge = null;
            var named = !string.IsNullOrEmpty(ViewModel.ArenaName);
            arenaLine.style.display = named ? DisplayStyle.Flex : DisplayStyle.None;
            if (!named) {
                return;
            }
            arenaLine.Add(OutlinedText(Strings.Get("lobby.arena"), "lobby__arena-label"));
            arenaLine.Add(OutlinedText(ViewModel.ArenaName, "lobby__arena-name"));
            if (ViewModel.ArenaIsNew) {
                arenaBadge = OutlinedText(Strings.Get("common.new"), "lobby__badge");
                arenaLine.Add(arenaBadge);
            }
        }

        private void UpdateLobby()
        {
            var time = (float)(Time.realtimeSinceStartupAsDouble - ViewModel.StartedAt);
            if (arenaBadge != null) {
                // The tag lands after the pill itself, with the same overshoot as the rows.
                var t = ViewModel.IsReady ? 1f : Mathf.Clamp01((time - 0.15f) / EnterDuration);
                var shifted = t - 1f;
                arenaBadge.style.scale = new Scale(Vector3.one * (1f + 2.70158f * shifted * shifted * shifted + 1.70158f * shifted * shifted));
            }
            var text = Strings.Get(ViewModel.IsReady ? "lobby.ready" : "lobby.match_found");
            title.text = text;
            titleFace.text = text;
            title.EnableInClassList("lobby__title--ready", ViewModel.IsReady);
            titleFace.EnableInClassList("lobby__title--ready", ViewModel.IsReady);
            var target = ViewModel.IsReady ? 1f : LoadingShare * (1f - Mathf.Exp(-time / LoadingTime));
            fill = Mathf.MoveTowards(fill, target, FillSpeed * Mathf.Max(0f, time - fillTime));
            fillTime = time;
            barFill.style.width = Length.Percent(100f * fill);
            loadingText.text = ViewModel.IsReady ? Strings.Get("lobby.ready_to_roll")
                : Strings.Get("lobby.loading") + Dots[(int)(time / 0.18f) % Dots.Length];

            for (var i = 0; i < rows.Count; i++) {
                var (slot, incoming, outgoing) = rows[i];
                if (outgoing != null) {
                    var scale = ViewModel.IsReady ? 0f : 1f - Mathf.Clamp01((time - LeftHold) / (LeftEnd - LeftHold));
                    outgoing.style.scale = new Scale(Vector3.one * scale);
                    outgoing.style.opacity = scale;
                    if (incoming == null) {
                        slot.style.height = 122f * scale;
                        slot.style.marginBottom = 12f * scale;
                    }
                }
                if (incoming == null) {
                    continue;
                }
                var start = Mathf.Max(i * RowStep, outgoing != null ? LeftEnd : 0f);
                var t = ViewModel.IsReady ? 1f : Mathf.Clamp01((time - start) / EnterDuration);
                var shifted = t - 1f;
                var pop = 1f + 2.70158f * shifted * shifted * shifted + 1.70158f * shifted * shifted;
                incoming.style.scale = new Scale(Vector3.one * pop);
                incoming.style.translate = new Translate(0f, 30f * (1f - t));
                incoming.style.opacity = Mathf.Clamp01(t * 4f);
            }
        }

        private static VisualElement CreateRow(LobbyRow row, bool isLeft)
        {
            var sticker = Element("lobby__row");
            sticker.EnableInClassList("lobby__row--left", isLeft);
            sticker.EnableInClassList("lobby__row--you", row.Index == 0);
            var chip = CarColor.CreateChip(row.ColorId, row.Index == 0);
            chip.EnableInClassList("lobby__you-chip", row.Index == 0);
            sticker.Add(chip);
            if (!isLeft && row.Level > 0) {
                sticker.Add(HudView.CreateLevelBadge(row.Level, "lobby__level"));
            }
            var name = OutlinedText(isLeft ? row.LeftName : row.Name, "lobby__name");
            name.EnableInClassList("lobby__name--revenge", row.IsNemesis && !isLeft);
            sticker.Add(name);
            if (row.IsNemesis && !isLeft) {
                var target = Element("revenge-target");
                target.Add(Element("revenge-target__center"));
                sticker.Add(target);
            }
            if (isLeft || row.IsNew) {
                var badge = OutlinedText(Strings.Get(isLeft ? "lobby.left" : "common.new"), "lobby__badge");
                badge.EnableInClassList("lobby__badge--left", isLeft);
                sticker.Add(badge);
            }
            return sticker;
        }

        private static VisualElement OutlinedText(string text, string className)
        {
            var label = new StickerText(text);
            label.AddToClassList(className);
            return label;
        }

        private static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }
    }
}
