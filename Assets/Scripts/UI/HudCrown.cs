using System;
using Bugord.App.Localization;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The CROWN mode on the HUD, so the player always knows what to do. A banner under the bar says it: GRAB THE
    /// CROWN! (orange) while it is loose, STEAL THE CROWN! (red) while a bot has it, RUN! (gold, pulsing) while the
    /// player does; it pops on each change. Beside the crown, wherever it rides, a sticker with the holder's seconds
    /// (gold for the player's own), popping on each new second. When the crown or its holder is off the screen, a gold
    /// pointer with a crown on the screen's edge shows where. GOT IT! pops over the player's car when it takes the
    /// crown, STOLEN! when a bump takes it away. The crown itself is in the arena (<see cref="CrownMode"/>).
    /// <para>
    /// Made only in the mode (<see cref="Create"/> gives null otherwise). Built once; a frame only sets styles and text
    /// when they change, on real time, and nothing here catches input. The styles of the shared fuse and banner
    /// stickers are reused; the mode's own colours are set from code.
    /// </para>
    /// </summary>
    internal sealed class HudCrown : IModeHud
    {
        // Reference pixels under the top of the safe area the banner sits at: under the HUD bar and the kill feed's
        // three rows at its left in portrait, as the hot potato's.
        private const float BannerTop = 480f;
        private const float BannerPulse = 0.06f;
        private const float BannerRate = 6f;
        private const float ChangePop = 0.25f;
        private const float ChangeScale = 1.25f;

        // The seconds sticker: reference pixels right of the crown's middle, and its beat on each new second.
        private const float SecondsSide = 130f;
        private const float SecondsMargin = 24f;
        private const float BeatScale = 1.3f;
        private const float BeatSeconds = 0.25f;
        // Metres over the crown's bottom the sticker's middle is level with.
        private const float SecondsLift = 0.45f;

        // The pointer: its size, how far inside the screen's edge, and the arrow's reach out of it.
        private const float PointerSize = 118f;
        private const float PointerInset = 92f;
        private const float ArrowReach = 82f;
        private const float ArrowSize = 46f;
        // Reference pixels inside the edge a point still counts as on the screen.
        private const float OnScreenMargin = 40f;

        // The pops over the player's car: how long they show, and how far over the car they start.
        private const float PopIn = 0.22f;
        private const float PopHold = 0.8f;
        private const float PopOut = 0.35f;
        private const float PopLift = 220f;
        private const float PopRise = 150f;

        private static readonly Color Navy = new Color32(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color Gold = new Color32(0xff, 0xc9, 0x3c, 0xff);
        private static readonly Color Orange = new Color32(0xff, 0x9f, 0x1c, 0xff);
        private static readonly Color Red = new Color32(0xff, 0x4d, 0x4d, 0xff);
        private static readonly Color White = Color.white;

        private enum Say
        {
            None,
            Grab,
            Steal,
            Run,
        }

        private readonly HudViewModel model;
        private readonly CrownMode crown;
        private readonly VisualElement layer;
        private readonly VisualElement root;
        private readonly VisualElement banner;
        private readonly VisualElement bannerSticker;
        private readonly Label[] bannerLabels;
        private readonly VisualElement seconds;
        private readonly VisualElement secondsSticker;
        private readonly Label[] secondsLabels;
        private readonly VisualElement pointer;
        private readonly VisualElement arrow;
        private readonly Pop gotPop;
        private readonly Pop stolenPop;
        private readonly Action onLanguage;
        private readonly Action<Car, Car> onTaken;
        private Say said = Say.None;
        private float saidAt = float.NegativeInfinity;
        private int shownSeconds = -1;
        private Car shownHolder;
        private float beatAt = float.NegativeInfinity;

        /// <summary>The mode's HUD, made by its look (<see cref="CrownLook"/>) in a crown round only.</summary>
        public static HudCrown Create(VisualElement layer, VisualElement root, HudViewModel model)
        {
            return model.Mode is CrownMode { IsActive: true } crown && layer != null
                ? new HudCrown(layer, root, model, crown)
                : null;
        }

        private HudCrown(VisualElement layer, VisualElement root, HudViewModel model, CrownMode crown)
        {
            this.model = model;
            this.crown = crown;
            this.layer = layer;
            this.root = root;

            seconds = new VisualElement { name = "crown-seconds", pickingMode = PickingMode.Ignore };
            seconds.AddToClassList("hud__fuse");
            secondsSticker = new VisualElement { pickingMode = PickingMode.Ignore };
            secondsSticker.AddToClassList("hud__fuse-sticker");
            secondsSticker.style.flexDirection = FlexDirection.Row;
            secondsSticker.style.paddingLeft = 14f;
            secondsSticker.Add(Icon(46f, 6f));
            var secondsText = StrokeText.Create("", null, out secondsLabels, "display", "hud__fuse-text");
            secondsSticker.Add(secondsText);
            seconds.Add(secondsSticker);
            layer.Add(seconds);

            banner = new VisualElement { name = "crown-banner", pickingMode = PickingMode.Ignore };
            banner.AddToClassList("hud__pass");
            bannerSticker = new VisualElement { pickingMode = PickingMode.Ignore };
            bannerSticker.AddToClassList("hud__pass-sticker");
            bannerSticker.style.flexDirection = FlexDirection.Row;
            bannerSticker.style.alignItems = Align.Center;
            bannerSticker.style.paddingLeft = 30f;
            bannerSticker.Add(Icon(70f, 10f));
            var bannerText = StrokeText.Create("", null, out bannerLabels, "display", "hud__pass-text");
            bannerSticker.Add(bannerText);
            banner.Add(bannerSticker);
            root.Add(banner);

            pointer = new VisualElement { name = "crown-pointer", pickingMode = PickingMode.Ignore };
            pointer.style.position = Position.Absolute;
            pointer.style.width = PointerSize;
            pointer.style.height = PointerSize;
            pointer.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
            pointer.style.display = DisplayStyle.None;
            arrow = new VisualElement { pickingMode = PickingMode.Ignore };
            arrow.style.position = Position.Absolute;
            arrow.style.left = 0f;
            arrow.style.top = 0f;
            arrow.style.width = PointerSize;
            arrow.style.height = PointerSize;
            arrow.generateVisualContent += DrawArrow;
            pointer.Add(arrow);
            var disc = new VisualElement { pickingMode = PickingMode.Ignore };
            disc.style.position = Position.Absolute;
            disc.style.left = 0f;
            disc.style.top = 0f;
            disc.style.width = PointerSize;
            disc.style.height = PointerSize;
            disc.style.alignItems = Align.Center;
            disc.style.justifyContent = Justify.Center;
            SetBorder(disc, 7f, 13f, PointerSize * 0.5f);
            disc.style.backgroundColor = Gold;
            disc.Add(Icon(62f, 0f));
            pointer.Add(disc);
            layer.Add(pointer);

            gotPop = new Pop(root, Gold);
            stolenPop = new Pop(root, Red);

            onLanguage = () => {
                said = Say.None;
                gotPop.SetText(Strings.Get("hud.crown_got"));
                stolenPop.SetText(Strings.Get("hud.crown_stolen"));
            };
            onLanguage();
            Strings.Changed += onLanguage;
            onTaken = OnTaken;
            crown.Taken += onTaken;
        }

        public void Tick(Camera camera, float top, float left, float right)
        {
            var time = Time.unscaledTime;
            var isLive = crown.IsShown && !model.IsFinished && camera != null && layer.panel != null;
            var holder = crown.Holder;
            var player = model.PlayerCar;
            var isPlayerHolding = holder != null && holder == player;

            var say = !isLive ? Say.None
                : isPlayerHolding ? Say.Run
                : holder != null ? Say.Steal
                : Say.Grab;
            ShowBanner(say, time, top);

            var size = layer.layout.size;
            ShowSeconds(isLive && holder != null, holder, camera, top, left, right, size, time);
            ShowPointer(isLive && !isPlayerHolding, camera, top, left, right, size);
            gotPop.Tick(time, camera, layer, player, top);
            stolenPop.Tick(time, camera, layer, player, top);
        }

        public void Dispose()
        {
            Strings.Changed -= onLanguage;
            crown.Taken -= onTaken;
            seconds.RemoveFromHierarchy();
            banner.RemoveFromHierarchy();
            pointer.RemoveFromHierarchy();
            gotPop.Dispose();
            stolenPop.Dispose();
        }

        // The player took the crown, or a bump took it from the player: a pop over the player's car.
        private void OnTaken(Car holder, Car from)
        {
            var player = model.PlayerCar;
            if (player == null) {
                return;
            }

            if (holder == player) {
                stolenPop.Stop();
                gotPop.Start(Time.unscaledTime);
            }
            else if (from == player) {
                gotPop.Stop();
                stolenPop.Start(Time.unscaledTime);
            }
        }

        private void ShowBanner(Say say, float time, float top)
        {
            if (say != said) {
                said = say;
                saidAt = time;
                if (say != Say.None) {
                    var key = say switch {
                        Say.Grab => "hud.crown_grab",
                        Say.Steal => "hud.crown_steal",
                        _ => "hud.crown_run",
                    };
                    StrokeText.SetText(bannerLabels, Strings.Get(key));
                    bannerSticker.style.backgroundColor = say switch {
                        Say.Grab => Orange,
                        Say.Steal => Red,
                        _ => Gold,
                    };
                }
            }

            banner.EnableInClassList("hud__pass--shown", say != Say.None);
            banner.style.top = top + BannerTop;
            if (say == Say.None) {
                return;
            }

            // A pop on each change, then the player's RUN! keeps pulsing; the others stay still.
            var since = time - saidAt;
            var pop = since < ChangePop ? Mathf.Sin(since / ChangePop * Mathf.PI) * (ChangeScale - 1f) : 0f;
            var pulse = say == Say.Run ? BannerPulse * (0.5f + 0.5f * Mathf.Sin(time * BannerRate)) : 0f;
            var scale = 1f + pop + pulse;
            bannerSticker.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        private void ShowSeconds(bool isShown, Car holder, Camera camera, float top, float left, float right,
            Vector2 size, float time)
        {
            seconds.EnableInClassList("hud__fuse--shown", isShown);
            if (!isShown) {
                shownSeconds = -1;
                shownHolder = null;
                return;
            }

            var value = model.ScoreOf(crown.HolderIndex);
            if (value != shownSeconds || holder != shownHolder) {
                if (holder == shownHolder && value > shownSeconds) {
                    beatAt = time;
                }

                if (holder != shownHolder) {
                    var isPlayer = holder == model.PlayerCar;
                    secondsSticker.style.backgroundColor = isPlayer ? Gold : White;
                }

                shownSeconds = value;
                shownHolder = holder;
                StrokeText.SetText(secondsLabels, value.ToString());
            }

            var beat = Mathf.Clamp01(1f - (time - beatAt) / BeatSeconds);
            var scale = 1f + (BeatScale - 1f) * beat * beat;
            secondsSticker.style.scale = new Scale(new Vector3(scale, scale, 1f));

            var world = crown.CrownPosition + Vector3.up * SecondsLift;
            var point = layer.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, world, camera));
            point.x += SecondsSide;
            var halfWidth = secondsSticker.layout.width * 0.5f + SecondsMargin;
            var halfHeight = secondsSticker.layout.height * 0.5f + SecondsMargin;
            var minX = halfWidth + left;
            point.x = Mathf.Clamp(point.x, minX, Mathf.Max(minX, size.x - halfWidth - right));
            point.y = Mathf.Clamp(point.y, halfHeight + top, Mathf.Max(halfHeight + top, size.y - halfHeight));
            seconds.style.left = point.x;
            seconds.style.top = point.y;
        }

        /// <summary>
        /// While the crown (or its holder) is off the screen, a gold disc with a crown on the screen's edge, on the line
        /// from the middle of the screen to it, its arrow pointing the way.
        /// </summary>
        private void ShowPointer(bool isWanted, Camera camera, float top, float left, float right, Vector2 size)
        {
            if (!isWanted) {
                pointer.style.display = DisplayStyle.None;
                return;
            }

            var target = crown.Holder != null ? crown.Holder.Position : crown.CrownPosition;
            var point = layer.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, target, camera));
            var minX = left + OnScreenMargin;
            var maxX = size.x - right - OnScreenMargin;
            var minY = top + OnScreenMargin;
            var maxY = size.y - OnScreenMargin;
            var isOnScreen = point.x >= minX && point.x <= maxX && point.y >= minY && point.y <= maxY;
            pointer.style.display = isOnScreen ? DisplayStyle.None : DisplayStyle.Flex;
            if (isOnScreen) {
                return;
            }

            // Onto the edge of the inset frame along the line from its middle.
            var frameMin = new Vector2(left + PointerInset, top + PointerInset);
            var frameMax = new Vector2(size.x - right - PointerInset, size.y - PointerInset);
            var middle = (frameMin + frameMax) * 0.5f;
            var toward = point - middle;
            if (toward.sqrMagnitude < 1f) {
                toward = Vector2.up;
            }

            var half = (frameMax - frameMin) * 0.5f;
            var reach = Mathf.Min(
                Mathf.Abs(toward.x) > 0.001f ? half.x / Mathf.Abs(toward.x) : float.PositiveInfinity,
                Mathf.Abs(toward.y) > 0.001f ? half.y / Mathf.Abs(toward.y) : float.PositiveInfinity);
            var at = middle + toward * Mathf.Min(1f, reach);
            pointer.style.left = at.x;
            pointer.style.top = at.y;
            var angle = Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg;
            arrow.style.rotate = new Rotate(angle);
        }

        // The arrow out of the pointer's disc, pointing to +x: the arrow element turns with the line to the target.
        private static void DrawArrow(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var middle = rect.center;
            var painter = context.painter2D;
            painter.BeginPath();
            painter.MoveTo(middle + new Vector2(ArrowReach, 0f));
            painter.LineTo(middle + new Vector2(ArrowReach - ArrowSize, -ArrowSize * 0.7f));
            painter.LineTo(middle + new Vector2(ArrowReach - ArrowSize, ArrowSize * 0.7f));
            painter.ClosePath();
            painter.fillColor = Gold;
            painter.Fill();
            painter.strokeColor = Navy;
            painter.lineWidth = 7f;
            painter.lineJoin = LineJoin.Round;
            painter.Stroke();
        }

        /// <summary>A gold crown, the scoreboard's and the bar's icon of the mode's points.</summary>
        private static VisualElement Icon(float size, float gap)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = size;
            icon.style.height = size * 0.82f;
            icon.style.marginRight = gap;
            icon.style.flexShrink = 0f;
            icon.generateVisualContent += RoundOverMatch.DrawCrown;
            return icon;
        }

        private static void SetBorder(VisualElement element, float width, float bottom, float radius)
        {
            element.style.borderTopWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = bottom;
            element.style.borderTopColor = Navy;
            element.style.borderLeftColor = Navy;
            element.style.borderRightColor = Navy;
            element.style.borderBottomColor = Navy;
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        /// <summary>
        /// A tilted sticker over the player's car that springs in, holds and flies up as it fades: GOT IT! or STOLEN!.
        /// </summary>
        private sealed class Pop
        {
            private readonly VisualElement element;
            private readonly VisualElement sticker;
            private readonly Label[] labels;
            private float startedAt = float.NegativeInfinity;

            public Pop(VisualElement root, Color colour)
            {
                element = new VisualElement { pickingMode = PickingMode.Ignore };
                element.style.position = Position.Absolute;
                element.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
                element.style.display = DisplayStyle.None;
                sticker = new VisualElement { pickingMode = PickingMode.Ignore };
                sticker.AddToClassList("hud__pass-sticker");
                sticker.style.backgroundColor = colour;
                sticker.style.rotate = new Rotate(-6f);
                sticker.Add(StrokeText.Create("", null, out labels, "display", "hud__pass-text"));
                element.Add(sticker);
                root.Add(element);
            }

            public void SetText(string value) => StrokeText.SetText(labels, value);

            public void Start(float time)
            {
                startedAt = time;
                element.style.display = DisplayStyle.Flex;
            }

            public void Stop()
            {
                startedAt = float.NegativeInfinity;
                element.style.display = DisplayStyle.None;
            }

            public void Tick(float time, Camera camera, VisualElement layer, Car car, float top)
            {
                var since = time - startedAt;
                if (since > PopIn + PopHold + PopOut || car == null || camera == null || layer.panel == null) {
                    if (element.style.display != DisplayStyle.None) {
                        element.style.display = DisplayStyle.None;
                    }

                    return;
                }

                var point = layer.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, car.Position,
                    camera));
                var size = layer.layout.size;
                var halfWidth = sticker.layout.width * 0.5f + 20f;
                point.x = Mathf.Clamp(point.x, halfWidth, Mathf.Max(halfWidth, size.x - halfWidth));
                point.y = Mathf.Max(point.y - PopLift, top + 120f);

                float scale;
                var rise = 0f;
                var opacity = 1f;
                if (since < PopIn) {
                    // In from small, past full size and back.
                    var t = since / PopIn;
                    scale = Mathf.LerpUnclamped(0.3f, 1f, 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f));
                }
                else if (since < PopIn + PopHold) {
                    scale = 1f;
                }
                else {
                    var t = (since - PopIn - PopHold) / PopOut;
                    scale = 1f + 0.15f * t;
                    rise = PopRise * t * t;
                    opacity = 1f - t;
                }

                element.style.left = point.x;
                element.style.top = point.y - rise;
                element.style.opacity = opacity;
                sticker.style.scale = new Scale(new Vector3(scale, scale, 1f));
            }

            public void Dispose() => element.RemoveFromHierarchy();
        }
    }
}
