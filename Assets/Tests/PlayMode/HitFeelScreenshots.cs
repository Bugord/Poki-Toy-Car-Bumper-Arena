using System.Collections;
using System.IO;
using NUnit.Framework;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using UnityEngine;
using UnityEngine.TestTools;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Pictures of a car hit next to the player's marker, for a look: the comic burst in the air, its lines and stars,
    /// the squash of the hit car, and the effects' outline beside the cars'. A weak and a full hit, each at 0.03, 0.1
    /// and 0.25 s, in portrait and landscape, to <c>Logs/hit-feel/&lt;power&gt;_&lt;time&gt;_&lt;w&gt;x&lt;h&gt;.png</c>.
    /// Explicit: it needs graphics and is for a person to look at.
    /// </summary>
    [Explicit("Writes pictures of the hit effect for a visual check; run it by name.")]
    public sealed class HitFeelScreenshots : ObjectsBFixture
    {
        private static readonly float[] Moments = { 0.03f, 0.1f, 0.25f };

        [UnityTest]
        public IEnumerator Hits()
        {
            yield return Open("sandbox", "rake");
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hit-feel");
            Directory.CreateDirectory(directory);
            var effects = Object.FindAnyObjectByType<EffectPlayer>();
            var player = Round.Player;
            var bot = Round.Cars[1];
            var captured = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try {
                foreach (var size in new[] { new Vector2Int(1080, 1920), new Vector2Int(1920, 1080) }) {
                    var camera = Camera.main;
                    var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                    camera.targetTexture = target;
                    foreach (var power in new[] { 0.2f, 1f }) {
                        // The bot right beside the player, as if the player had just driven into it.
                        var side = player.transform.right;
                        bot.Movement.Place(player.Position + side * 2.6f, player.transform.rotation);
                        for (var i = 0; i < 30; i++) {
                            yield return null;
                        }

                        var point = (player.Position + bot.Position) * 0.5f;
                        effects.PlayHit(point, side, power, power > 0.5f, power > 0.5f);
                        bot.GetComponent<CarLook>().Squash(Mathf.Lerp(0.12f, 0.3f, power));
                        var elapsed = 0f;
                        foreach (var moment in Moments) {
                            while (elapsed < moment) {
                                yield return null;
                                elapsed += Time.unscaledDeltaTime;
                            }

                            // The frame just rendered, into the texture.
                            yield return null;
                            Write(target, Path.Combine(directory,
                                $"{(power > 0.5f ? "full" : "weak")}_{moment:0.00}_{size.x}x{size.y}.png"));
                        }

                        for (var i = 0; i < 40; i++) {
                            yield return null;
                        }
                    }

                    camera.targetTexture = null;
                    Object.DestroyImmediate(target);
                }
            }
            finally {
                Time.captureDeltaTime = captured;
            }
        }

        /// <summary>
        /// Wheel smoke: a bot shoved sideways sliding past the player, and the same bot landing after a drop, to
        /// <c>Logs/hit-feel/smoke-&lt;slide|land&gt;_&lt;time&gt;_&lt;w&gt;x&lt;h&gt;.png</c>.
        /// </summary>
        [UnityTest]
        public IEnumerator Smoke()
        {
            yield return Open("sandbox", "rake");
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hit-feel");
            Directory.CreateDirectory(directory);
            var player = Round.Player;
            var bot = Round.Cars[1];
            var captured = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try {
                foreach (var size in new[] { new Vector2Int(1080, 1920), new Vector2Int(1920, 1080) }) {
                    var camera = Camera.main;
                    var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                    camera.targetTexture = target;
                    foreach (var kind in new[] { "slide", "land" }) {
                        var side = player.transform.right;
                        var start = player.Position + side * 3f + player.transform.forward * 2f;
                        bot.Movement.Place(start + Vector3.up * (kind == "land" ? 3f : 0f), player.transform.rotation);
                        if (kind == "slide") {
                            for (var i = 0; i < 20; i++) {
                                yield return null;
                            }

                            bot.Movement.Stagger(0.8f);
                            bot.Movement.Push(side * 9f);
                        }

                        var elapsed = 0f;
                        foreach (var moment in new[] { 0.25f, 0.5f, 0.75f }) {
                            while (elapsed < moment) {
                                yield return null;
                                elapsed += Time.unscaledDeltaTime;
                            }

                            yield return null;
                            Write(target, Path.Combine(directory, $"smoke-{kind}_{moment:0.00}_{size.x}x{size.y}.png"));
                        }

                        for (var i = 0; i < 40; i++) {
                            yield return null;
                        }
                    }

                    camera.targetTexture = null;
                    Object.DestroyImmediate(target);
                }
            }
            finally {
                Time.captureDeltaTime = captured;
            }
        }

        private static void Write(RenderTexture target, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Debug.Log($"Hit picture: {path}");
        }
    }
}
