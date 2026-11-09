using System.Collections.Generic;
using Bugord.App.Localization;
using Bugord.App.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The curtain between rounds. After NEXT MATCH it shows the coming match on it as a search for players: FINDING
    /// PLAYERS..., the arena, the seats searching and filling one by one, a bar of players found and a tip, MATCH FOUND!
    /// once every seat is taken and READY! once the arena has loaded. The search runs inside the lobby's least time, so it
    /// adds nothing to the wait; the scene loads behind it the whole while.
    /// </summary>
    public sealed class CurtainView : FadingView<CurtainViewModel>
    {
        private const float RowStep = 0.045f;
        private const float EnterDuration = 0.2f;
        private const float LeftHold = 0.12f;
        private const float LeftEnd = 0.24f;
        // When the first and the last seat are found, in seconds from the lobby: all of them before its least time ends,
        // so MATCH FOUND! shows a moment before READY!. Each find is shifted a little, so they do not come like a clock.
        private const float FirstFind = 0.3f;
        private const float LastFind = CurtainViewModel.LobbyDuration - 0.4f;
        private const float FindJitter = 0.06f;
        // A found seat's searching strip shrinks away under the player popping in.
        private const float SearchOut = 0.12f;
        // The bar fills with the seats found and the rest on READY!.
        private const float FoundShare = 0.9f;
        private const float FillSpeed = 4f;
        private const float SpinSpeed = 540f;

        private static readonly string[] Tips = {
            "lobby.tip.boost_hit",
            "lobby.tip.boost_edge",
            "lobby.tip.push_edge",
            "lobby.tip.crumble",
            "lobby.tip.middle",
        };

        private static readonly string[] Dots = { ".", "..", "..." };

        private readonly List<Seat> seats = new();
        private VisualElement lobby;
        private Label title;
        private Label titleFace;
        private VisualElement arenaLine;
        private VisualElement specialCard;
        private VisualElement arenaBadge;
        private VisualElement barFill;
        private Label loadingText;
        private float fill;
        private float fillTime;
        private IVisualElementScheduledItem animation;

        protected override float FadeDuration => 0.25f;

        private sealed class Seat
        {
            public VisualElement Slot;
            public VisualElement Incoming;
            public VisualElement Outgoing;
            public VisualElement Searching;
            public VisualElement Spinner;
            public Label SearchingText;
            // When the seat's player is found, in seconds from the lobby; 0 for the player's own, there from the start.
            public float FoundAt;
        }

        protected override void OnAttached()
        {
            lobby = Root.Q<VisualElement>("lobby");
            DriftingChecker.AddTo(Root.Q<VisualElement>("curtain"));
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
            seats.Clear();
            lobby.style.display = DisplayStyle.Flex;
            BuildArenaLine();
            fill = 0f;
            fillTime = 0f;
            // The mission closest to done, while there is one; a tip otherwise. A special round's rule has its own card
            // under the arena, and the line gives way to it: one thing to read before the round.
            var tip = Root.Q<Label>("lobby-tip");
            tip.text = ViewModel.Mission ?? Strings.Get(Tips[Random.Range(0, Tips.Length)]);
            tip.style.display = GameText.Special(ViewModel.Special) == null ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var row in ViewModel.Rows) {
                var seat = new Seat { Slot = Element("lobby__slot") };
                if (row.Name != null && row.Index != 0) {
                    seat.Searching = CreateSearching(out seat.Spinner, out seat.SearchingText);
                    seat.Slot.Add(seat.Searching);
                }
                if (row.Name != null) {
                    seat.Incoming = CreateRow(row, false, ViewModel.Streak);
                    seat.Slot.Add(seat.Incoming);
                }
                if (row.LeftName != null) {
                    seat.Outgoing = CreateRow(row, true, 0);
                    seat.Slot.Add(seat.Outgoing);
                }
                seats.Add(seat);
                container.Add(seat.Slot);
            }
            ScheduleFinds();
            PillShape.Keep(lobby);
            animation?.Pause();
            UpdateLobby();
            animation = Root.schedule.Execute(UpdateLobby).Every(16);
        }

        /// <summary>
        /// Spreads the finds of the searched seats over the search, in a shuffled order: a real queue fills seats in no
        /// order of their own.
        /// </summary>
        private void ScheduleFinds()
        {
            var searched = new List<Seat>();
            foreach (var seat in seats) {
                if (seat.Searching != null) {
                    searched.Add(seat);
                }
            }
            for (var i = searched.Count - 1; i > 0; i--) {
                var j = Random.Range(0, i + 1);
                (searched[i], searched[j]) = (searched[j], searched[i]);
            }
            for (var i = 0; i < searched.Count; i++) {
                var share = searched.Count > 1 ? i / (float)(searched.Count - 1) : 1f;
                var at = Mathf.Lerp(FirstFind, LastFind, share) + Random.Range(-FindJitter, FindJitter);
                // A seat a player is leaving only searches once the old row has gone.
                searched[i].FoundAt = Mathf.Max(at, (searched[i].Outgoing != null ? LeftEnd : 0f) + EnterDuration);
            }
        }

        private void BuildArenaLine()
        {
            arenaLine.Clear();
            arenaBadge = null;
            BuildSpecialCard();
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

        /// <summary>
        /// A special round's card under the arena: its name on a tilted orange tag over the card's top edge and its
        /// rule big on the card. It was a small tag in the arena pill and the rule on the tip line at the bottom, where
        /// few read it before the round changed under them.
        /// </summary>
        private void BuildSpecialCard()
        {
            specialCard?.RemoveFromHierarchy();
            specialCard = null;
            var special = GameText.Special(ViewModel.Special);
            if (special == null) {
                return;
            }

            specialCard = Element("lobby__special");
            var tag = OutlinedText(special, "lobby__badge");
            tag.AddToClassList("lobby__badge--special");
            specialCard.Add(tag);
            var rule = new Label(GameText.SpecialHint(ViewModel.Special)) { pickingMode = PickingMode.Ignore };
            rule.AddToClassList("lobby__special-rule");
            specialCard.Add(rule);
            arenaLine.parent.Insert(arenaLine.parent.IndexOf(arenaLine) + 1, specialCard);
            arenaBadge = tag;
        }

        private void UpdateLobby()
        {
            var time = (float)(Time.realtimeSinceStartupAsDouble - ViewModel.StartedAt);
            var isReady = ViewModel.IsReady;
            if (arenaBadge != null) {
                // The tag lands after the pill itself, with the same overshoot as the rows.
                arenaBadge.style.scale = new Scale(Vector3.one * Pop(isReady ? 1f : Mathf.Clamp01((time - 0.15f) / EnterDuration)));
            }

            var dots = Dots[(int)(time / 0.18f) % Dots.Length];
            var found = 0;
            var total = 0;
            for (var i = 0; i < seats.Count; i++) {
                var seat = seats[i];
                if (seat.Outgoing != null) {
                    var scale = isReady ? 0f : 1f - Mathf.Clamp01((time - LeftHold) / (LeftEnd - LeftHold));
                    seat.Outgoing.style.scale = new Scale(Vector3.one * scale);
                    seat.Outgoing.style.opacity = scale;
                    if (seat.Incoming == null) {
                        seat.Slot.style.height = 122f * scale;
                        seat.Slot.style.marginBottom = 12f * scale;
                    }
                }
                if (seat.Incoming == null) {
                    continue;
                }

                total++;
                var isFound = isReady || time >= seat.FoundAt;
                if (isFound) {
                    found++;
                }

                var enter = Mathf.Max(i * RowStep, seat.Outgoing != null ? LeftEnd : 0f);
                if (seat.Searching != null) {
                    // The searching strip pops in with the seat and gives way to the player found for it.
                    var t = isReady ? 1f : Mathf.Clamp01((time - enter) / EnterDuration);
                    var away = isFound ? Mathf.Clamp01((time - seat.FoundAt) / SearchOut) : 0f;
                    var scale = isReady ? 0f : Pop(t) * (1f - away);
                    seat.Searching.style.scale = new Scale(Vector3.one * scale);
                    seat.Searching.style.opacity = Mathf.Clamp01(t * 4f) * (1f - away);
                    if (!isFound) {
                        seat.Spinner.style.rotate = new Rotate(time * SpinSpeed);
                        seat.SearchingText.text = Strings.Get("lobby.searching") + dots;
                    }
                    enter = seat.FoundAt;
                }

                var shown = isReady ? 1f : Mathf.Clamp01((time - enter) / EnterDuration);
                seat.Incoming.style.scale = new Scale(Vector3.one * Pop(shown));
                seat.Incoming.style.translate = new Translate(0f, 30f * (1f - shown));
                seat.Incoming.style.opacity = Mathf.Clamp01(shown * 4f);
            }

            var isComplete = found >= total;
            var text = isReady ? Strings.Get("lobby.ready")
                : isComplete ? Strings.Get("lobby.match_found") : Strings.Get("lobby.finding_players");
            title.text = text;
            titleFace.text = text;
            title.EnableInClassList("lobby__title--ready", isReady);
            titleFace.EnableInClassList("lobby__title--ready", isReady);
            title.EnableInClassList("lobby__title--search", !isComplete);
            titleFace.EnableInClassList("lobby__title--search", !isComplete);

            var target = isReady ? 1f : FoundShare * (total > 0 ? found / (float)total : 1f);
            fill = Mathf.MoveTowards(fill, target, FillSpeed * Mathf.Max(0f, time - fillTime));
            fillTime = time;
            barFill.style.width = Length.Percent(100f * fill);
            loadingText.text = isReady ? Strings.Get("lobby.ready_to_roll")
                : isComplete ? Strings.Get("lobby.starting") + dots : Strings.Format("lobby.players", found, total);
        }

        // An ease out with an overshoot, 0 to 1: the pop of a sticker landing.
        private static float Pop(float t)
        {
            var shifted = t - 1f;
            return 1f + 2.70158f * shifted * shifted * shifted + 1.70158f * shifted * shifted;
        }

        private static VisualElement CreateSearching(out VisualElement spinner, out Label text)
        {
            var sticker = Element("lobby__row");
            sticker.AddToClassList("lobby__row--searching");
            spinner = Element("lobby__spinner");
            sticker.Add(spinner);
            text = new Label(Strings.Get("lobby.searching")) { pickingMode = PickingMode.Ignore };
            text.AddToClassList("lobby__searching-text");
            sticker.Add(text);
            return sticker;
        }

        private static VisualElement CreateRow(LobbyRow row, bool isLeft, int streak)
        {
            var sticker = Element("lobby__row");
            sticker.EnableInClassList("lobby__row--left", isLeft);
            sticker.EnableInClassList("lobby__row--you", row.Index == 0);
            var isRival = row.IsNemesis && !isLeft;
            sticker.EnableInClassList("lobby__row--rival", isRival);
            var chip = CarColor.CreateChip(row.ColorId, row.Index == 0);
            chip.EnableInClassList("lobby__you-chip", row.Index == 0);
            sticker.Add(chip);
            // The slot stays even without a badge (a LEFT seat), so every nick starts on the same line.
            var level = !isLeft && row.Level > 0
                ? HudView.CreateLevelBadge(row.Level, "lobby__level", row.IsNemesis, true)
                : null;
            sticker.Add(HudView.InLevelSlot(level, "lobby__level-slot"));
            var name = OutlinedText(isLeft ? row.LeftName : row.Name, "lobby__name");
            name.EnableInClassList("lobby__name--revenge", row.IsNemesis && !isLeft);
            sticker.Add(name);
            // The player's own win streak, the round it leads to paying more, or the one a bot joined on.
            var rowStreak = row.Index == 0 ? streak : isLeft ? 0 : row.Streak;
            if (rowStreak > 0) {
                var badge = OutlinedText(Strings.Format("result.streak", rowStreak), "lobby__badge");
                badge.AddToClassList("lobby__badge--streak");
                sticker.Add(badge);
            }
            else if (isRival) {
                // The rival's row says why its name is red: the plaque reads before the horns on the small badge do.
                var badge = OutlinedText(Strings.Get("hud.revenge"), "lobby__badge");
                badge.AddToClassList("lobby__badge--revenge");
                sticker.Add(badge);
            }

            if (isLeft) {
                var badge = OutlinedText(Strings.Get("lobby.left"), "lobby__badge");
                badge.AddToClassList("lobby__badge--left");
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
