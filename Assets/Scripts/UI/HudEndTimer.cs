using System;
using Bugord.App.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The end of a match on the HUD, in stages, so the clock running out is felt and not only read: at 0:30 a tilted gold
    /// sticker under the bar and a gold flash of the time; from 0:10 a bigger bar, a red frame round the screen beating
    /// with the clock and a FINAL 10! sticker; in the last three seconds a bar bigger still, beating, with red rings
    /// spreading from it and the frame lit; at 0:00 a TIME UP! stamp slamming into the middle with a ring and stars.
    /// <para>
    /// Only a match ends by its clock; a round of last-car-standing keeps its plain red pill, so this is made only for a
    /// match. Everything runs on real time (the buzzer's hit-stop slows nothing here), nothing catches input, and the
    /// elements are built once: a frame only sets styles.
    /// </para>
    /// </summary>
    internal sealed class HudEndTimer : IDisposable
    {
        // The stages: seconds left on the clock as it shows them, rounded up.
        private const int NoteSeconds = 30;
        private const int UrgentSeconds = 10;
        private const int FinalSeconds = 3;

        private const float UrgentScale = 1.15f;
        private const float FinalScale = 1.4f;
        // The bar's beat in the last seconds: up to this scale on the second, easing back.
        private const float FinalBeatScale = 1.55f;
        private const float BeatSeconds = 0.3f;
        // How fast the bar grows into a new stage, in scale per second.
        private const float GrowSpeed = 2f;

        private const float FramePeak = 0.55f;
        // In the last seconds the frame stays lit and only breathes with the beat.
        private const float FrameFinalLow = 0.4f;
        private const float FrameFadeSeconds = 0.4f;

        private const float RingSeconds = 0.6f;
        private const float RingDelay = 0.15f;
        private const float RingScale = 0.6f;
        private const float RingOpacity = 0.9f;

        // The note: springs in, holds and flies up.
        private const float NoteInSeconds = 0.35f;
        private const float NoteHoldSeconds = 1.4f;
        private const float NoteOutSeconds = 0.3f;
        private const float NoteTilt = -4f;
        private const float NoteFly = 180f;
        // Under the bar at its biggest, so a bigger bar never reaches it.
        private const float NoteGap = 24f;
        // A scoreboard starting left of this share of the screen's width reaches under the note's words.
        private const float NoteClearShare = 0.75f;

        private const string FlashClass = "hud__timer--flash";
        private const long FlashMilliseconds = 250;

        // The stamp: slams from big to its size, tilting, then the ring and the stars burst behind it.
        private const float StampSlamSeconds = 0.2f;
        private const float StampFromScale = 2f;
        private const float StampTilt = -6f;
        private const float StampSettleSeconds = 0.25f;
        private const float StampSquash = 0.08f;
        private const float StampRingSeconds = 0.5f;
        private const float StampRingFrom = 0.6f;
        private const float StampRingTo = 2.4f;
        private const int StampStars = 8;
        private const float StarSeconds = 0.6f;
        private const float StarReach = 440f;
        private const float StarTurn = 220f;

        private readonly HudViewModel model;
        private readonly VisualElement bar;
        private readonly VisualElement timePill;
        private readonly VisualElement scoreboard;
        private readonly VisualElement frame;
        private readonly VisualElement[] rings;
        private readonly VisualElement note;
        private readonly VisualElement noteSticker;
        private readonly Label noteText;
        private readonly Label noteFace;
        private readonly VisualElement stamp;
        private readonly VisualElement stampSticker;
        private readonly VisualElement stampRing;
        private readonly VisualElement[] stars = new VisualElement[StampStars];
        private readonly VisualElement[] hiddenAtStamp;
        private readonly IVisualElementScheduledItem tick;
        private int shownSeconds = -1;
        private float beatAt = float.NegativeInfinity;
        private float noteAt = float.NegativeInfinity;
        private float stampAt = float.NegativeInfinity;
        private float barScale = 1f;
        private float frameAtStamp;
        private bool isStamped;

        private HudEndTimer(VisualElement root, HudViewModel model, VisualElement timePill, VisualElement scoreboard,
            VisualElement[] hiddenAtStamp)
        {
            this.model = model;
            this.timePill = timePill;
            this.scoreboard = scoreboard;
            this.hiddenAtStamp = hiddenAtStamp;
            bar = root.Q<VisualElement>("hud-bar");
            frame = root.Q<VisualElement>("end-frame");
            rings = new[] { root.Q<VisualElement>("end-ring-a"), root.Q<VisualElement>("end-ring-b") };
            note = root.Q<VisualElement>("end-note");
            noteSticker = root.Q<VisualElement>("end-note-sticker");
            noteText = root.Q<Label>("end-note-text");
            noteFace = root.Q<Label>("end-note-face");
            stamp = root.Q<VisualElement>("end-stamp");
            stampSticker = root.Q<VisualElement>("end-stamp-sticker");
            stampRing = root.Q<VisualElement>("end-stamp-ring");

            var burst = root.Q<VisualElement>("end-stamp-stars");
            for (var i = 0; i < StampStars; i++) {
                stars[i] = CreateStar();
                burst.Add(stars[i]);
            }

            noteSticker.style.rotate = new Rotate(NoteTilt);
            tick = root.schedule.Execute(Tick).Every(16);
        }

        /// <summary>The end of a match on the HUD, or null after a round of last-car-standing.</summary>
        public static HudEndTimer Create(VisualElement root, HudViewModel model, VisualElement timePill,
            VisualElement scoreboard, params VisualElement[] hiddenAtStamp) =>
            model.IsTimed ? new HudEndTimer(root, model, timePill, scoreboard, hiddenAtStamp) : null;

        public void Dispose()
        {
            tick.Pause();
        }

        private void Tick()
        {
            var now = Time.unscaledTime;
            if (!isStamped && model.IsFinished) {
                Stamp(now);
            }
            else if (!isStamped && model.HasBegun) {
                var seconds = Mathf.CeilToInt(model.TimeLeft);
                if (seconds != shownSeconds) {
                    OnSecond(seconds, now);
                }
            }

            AnimateBar(now);
            AnimateFrame(now);
            AnimateRings(now);
            AnimateNote(now);
            AnimateStamp(now);
        }

        private void OnSecond(int seconds, float now)
        {
            var previous = shownSeconds;
            shownSeconds = seconds;
            // The first reading only sets where the clock is: a stage starts as the clock crosses into it.
            if (previous < 0) {
                return;
            }

            if (seconds <= UrgentSeconds && seconds > 0) {
                beatAt = now;
            }

            if (seconds == NoteSeconds && previous > NoteSeconds) {
                ShowNote(Strings.Format("hud.seconds_left", NoteSeconds), now);
                timePill.AddToClassList(FlashClass);
                timePill.schedule.Execute(() => timePill.RemoveFromClassList(FlashClass)).StartingIn(FlashMilliseconds);
            }
            else if (seconds == UrgentSeconds && previous > UrgentSeconds) {
                ShowNote(Strings.Format("hud.final_seconds", UrgentSeconds), now);
            }
        }

        private void ShowNote(string text, float now)
        {
            noteText.text = text;
            noteFace.text = text;
            noteAt = now;
        }

        private void Stamp(float now)
        {
            isStamped = true;
            stampAt = now;
            frameAtStamp = frame.resolvedStyle.opacity;
            noteAt = float.NegativeInfinity;
            // The stamp takes the middle of the screen alone: no BACK IN or knock-out sticker under it.
            foreach (var element in hiddenAtStamp) {
                element.style.visibility = Visibility.Hidden;
            }
        }

        // The bar grows into each stage from the top centre (ui_screens), so it never slides off its place.
        private void AnimateBar(float now)
        {
            var seconds = shownSeconds;
            var target = isStamped || seconds > UrgentSeconds || seconds < 0 ? 1f :
                seconds <= FinalSeconds ? FinalScale : UrgentScale;
            barScale = Mathf.MoveTowards(barScale, target, GrowSpeed * Time.unscaledDeltaTime);
            var scale = barScale;
            if (!isStamped && seconds <= FinalSeconds && seconds > 0) {
                var beat = 1f - Mathf.Clamp01((now - beatAt) / BeatSeconds);
                scale += (FinalBeatScale - FinalScale) * beat * beat;
            }

            bar.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        private void AnimateFrame(float now)
        {
            float opacity;
            if (isStamped) {
                opacity = frameAtStamp * (1f - Mathf.Clamp01((now - stampAt) / FrameFadeSeconds));
            }
            else if (shownSeconds > UrgentSeconds || shownSeconds <= 0) {
                opacity = 0f;
            }
            else {
                // Up and back down over the second, from the beat.
                var pulse = Mathf.Sin(Mathf.Clamp01(now - beatAt) * Mathf.PI);
                opacity = shownSeconds <= FinalSeconds
                    ? Mathf.Lerp(FrameFinalLow, FramePeak, pulse)
                    : FramePeak * pulse;
            }

            frame.style.display = opacity > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
            frame.style.opacity = opacity;
        }

        private void AnimateRings(float now)
        {
            var isFinal = !isStamped && shownSeconds <= FinalSeconds && shownSeconds > 0;
            for (var i = 0; i < rings.Length; i++) {
                var age = (now - beatAt - i * RingDelay) / RingSeconds;
                var ring = rings[i];
                if (!isFinal || age < 0f || age >= 1f) {
                    ring.style.display = DisplayStyle.None;
                    continue;
                }

                var spread = 1f - (1f - age) * (1f - age);
                var scale = 1f + RingScale * spread;
                ring.style.display = DisplayStyle.Flex;
                ring.style.scale = new Scale(new Vector3(scale, scale, 1f));
                ring.style.opacity = RingOpacity * (1f - age);
            }
        }

        private void AnimateNote(float now)
        {
            var age = now - noteAt;
            var total = NoteInSeconds + NoteHoldSeconds + NoteOutSeconds;
            if (age < 0f || age >= total) {
                note.style.display = DisplayStyle.None;
                return;
            }

            note.style.display = DisplayStyle.Flex;
            note.style.top = NoteTop();
            float scale;
            float opacity;
            float fly;
            if (age < NoteInSeconds) {
                scale = EaseOutBack(age / NoteInSeconds);
                opacity = Mathf.Clamp01(age / NoteInSeconds * 3f);
                fly = 0f;
            }
            else {
                var leave = Mathf.Clamp01((age - NoteInSeconds - NoteHoldSeconds) / NoteOutSeconds);
                scale = 1f;
                opacity = 1f - leave;
                fly = -NoteFly * leave * leave;
            }

            noteSticker.style.scale = new Scale(new Vector3(scale, scale, 1f));
            noteSticker.style.opacity = opacity;
            noteSticker.style.translate = new Translate(0f, fly);
        }

        // Under the bar at the biggest it gets, wherever the safe area put the bar; on a narrow screen, where the compact
        // scoreboard reaches in under the bar, under the scoreboard too, so the words never cover the standings.
        private float NoteTop()
        {
            var top = bar.layout.y + bar.layout.height * FinalBeatScale;
            var board = scoreboard?.layout ?? default;
            var width = note.parent.layout.width;
            if (scoreboard != null && scoreboard.resolvedStyle.display == DisplayStyle.Flex &&
                board.xMin < width * NoteClearShare) {
                top = Mathf.Max(top, board.yMax);
            }

            return top + NoteGap;
        }

        private void AnimateStamp(float now)
        {
            if (!isStamped) {
                stamp.style.display = DisplayStyle.None;
                return;
            }

            stamp.style.display = DisplayStyle.Flex;
            var age = now - stampAt;
            var slam = Mathf.Clamp01(age / StampSlamSeconds);
            // Falls in faster and faster, as a stamp does, then squashes once on landing.
            var scale = Mathf.Lerp(StampFromScale, 1f, slam * slam);
            if (slam >= 1f) {
                var settle = Mathf.Clamp01((age - StampSlamSeconds) / StampSettleSeconds);
                scale -= StampSquash * Mathf.Sin(settle * Mathf.PI) * (1f - settle);
            }

            stampSticker.style.scale = new Scale(new Vector3(scale, scale, 1f));
            stampSticker.style.rotate = new Rotate(StampTilt * slam);
            stampSticker.style.opacity = Mathf.Clamp01(age / StampSlamSeconds * 2.5f);

            var burst = age - StampSlamSeconds;
            var ring = Mathf.Clamp01(burst / StampRingSeconds);
            var ringScale = Mathf.Lerp(StampRingFrom, StampRingTo, 1f - (1f - ring) * (1f - ring));
            stampRing.style.display = burst >= 0f && ring < 1f ? DisplayStyle.Flex : DisplayStyle.None;
            stampRing.style.scale = new Scale(new Vector3(ringScale, ringScale, 1f));
            stampRing.style.opacity = 1f - ring;

            var fly = Mathf.Clamp01(burst / StarSeconds);
            var reach = StarReach * (1f - (1f - fly) * (1f - fly));
            for (var i = 0; i < stars.Length; i++) {
                var star = stars[i];
                if (burst < 0f || fly >= 1f) {
                    star.style.display = DisplayStyle.None;
                    continue;
                }

                var angle = (i + 0.5f) * Mathf.PI * 2f / stars.Length;
                // Wider than tall, as the stamp is.
                var at = new Vector2(Mathf.Cos(angle) * 1.4f, Mathf.Sin(angle)) * reach;
                var size = Mathf.Lerp(1f, 0.45f, fly);
                star.style.display = DisplayStyle.Flex;
                star.style.translate = new Translate(at.x, at.y);
                star.style.scale = new Scale(new Vector3(size, size, 1f));
                star.style.rotate = new Rotate(StarTurn * fly * (i % 2 == 0 ? 1f : -1f));
                star.style.opacity = 1f - Mathf.Clamp01((fly - 0.6f) / 0.4f);
            }
        }

        // The gold burst of the HUD's knock-out badge, big.
        private static VisualElement CreateStar()
        {
            var star = new VisualElement { pickingMode = PickingMode.Ignore };
            star.AddToClassList("ko-star");
            star.AddToClassList("ko-star--big");
            star.AddToClassList("end-stamp__star");
            HudView.DrawAsBurst(star);

            star.style.display = DisplayStyle.None;
            return star;
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            t -= 1f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }
    }
}
