using System.Collections.Generic;
using Bugord.App.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    public sealed class CurtainView : FadingView<CurtainViewModel>
    {
        private const float RowStep = 0.045f;
        private const float EnterDuration = 0.2f;
        private const float LeftHold = 0.12f;
        private const float LeftEnd = 0.24f;

        private readonly List<(VisualElement slot, VisualElement incoming, VisualElement outgoing)> rows = new();
        private VisualElement lobby;
        private Label title;
        private Label titleFace;
        private IVisualElementScheduledItem animation;

        protected override float FadeDuration => 0.25f;

        protected override void OnAttached()
        {
            lobby = Root.Q<VisualElement>("lobby");
            title = Root.Q<Label>("lobby-title");
            titleFace = Root.Q<Label>("lobby-title-face");
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

        private void UpdateLobby()
        {
            var time = (float)(Time.realtimeSinceStartupAsDouble - ViewModel.StartedAt);
            var text = ViewModel.IsReady ? "READY!" : "FINDING PLAYERS" + new string('.', 1 + (int)(time / 0.18f) % 3);
            title.text = text;
            titleFace.text = text;
            title.EnableInClassList("lobby__title--ready", ViewModel.IsReady);
            titleFace.EnableInClassList("lobby__title--ready", ViewModel.IsReady);

            for (var i = 0; i < rows.Count; i++) {
                var (slot, incoming, outgoing) = rows[i];
                if (outgoing != null) {
                    var scale = ViewModel.IsReady ? 0f : 1f - Mathf.Clamp01((time - LeftHold) / (LeftEnd - LeftHold));
                    outgoing.style.scale = new Scale(Vector3.one * scale);
                    outgoing.style.opacity = scale;
                    if (incoming == null) {
                        slot.style.height = 130f * scale;
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
            var chip = CarColor.CreateChip(row.ColorId, row.Index == 0);
            chip.EnableInClassList("lobby__you-chip", row.Index == 0);
            sticker.Add(chip);
            var name = OutlinedText(isLeft ? row.LeftName : row.Name, "lobby__name");
            name.EnableInClassList("lobby__name--revenge", row.IsNemesis && !isLeft);
            sticker.Add(name);
            if (row.IsNemesis && !isLeft) {
                var target = Element("revenge-target");
                target.Add(Element("revenge-target__center"));
                sticker.Add(target);
            }
            if (isLeft || row.IsNew) {
                var badge = OutlinedText(isLeft ? "LEFT" : "NEW", "lobby__badge");
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
