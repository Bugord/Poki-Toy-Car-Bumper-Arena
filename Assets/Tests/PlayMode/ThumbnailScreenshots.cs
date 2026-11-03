using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.Rendering;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Candidates for the Poki thumbnail: on each arena a crowned car rams another off the near edge in a burst of stars,
    /// close up, two more cars and the arena's floor behind them, from a couple of camera angles:
    /// <c>Logs/thumbnail/&lt;arena&gt;_&lt;camera&gt;_&lt;moment&gt;.png</c>, square and twice the thumbnail's size, to be
    /// scaled down (Marketing/thumbnail.png is tabletop_high, cropped a little). Explicit: it is for picking a picture,
    /// and needs graphics.
    /// </summary>
    [Explicit("Writes thumbnail candidates for a person to pick from; run it by name.")]
    public sealed class ThumbnailScreenshots : SimulationSceneFixture
    {
        private const int Size = 2512;

        // Camera offsets in the frame of the hit (out past the edge, to the right, up) and how wide it sees.
        private static readonly (string Name, Vector3 Offset, float Fov)[] Cameras =
        {
            ("high", new Vector3(9.5f, 1f, 6.5f), 38f),
            ("side", new Vector3(9.5f, 3f, 6.5f), 38f),
        };

        private static readonly MethodInfo DrawEffects =
            typeof(EffectPlayer).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo Particles =
            typeof(EffectPlayer).GetField("particles", BindingFlags.Instance | BindingFlags.NonPublic);

        // Real seconds after the hit: the stars fly out, then spread.
        private static readonly float[] Moments = { 0.15f };

        [UnityTest]
        public IEnumerator Tabletop() => Capture("tabletop", "cloth", 160f);

        [UnityTest]
        public IEnumerator Playroom() => Capture("playroom", "puzzle", 200f);

        [UnityTest]
        public IEnumerator Sandbox() => Capture("sandbox", "rake", 160f);

        private IEnumerator Capture(string map, string variant, float angle)
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "thumbnail");
            Directory.CreateDirectory(directory);
            PlayerTopper = "crown";
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 1 }, map, variant);
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var edge = round.Edge;
            var theme = container.Resolve<ArenaMap>().Theme;
            var catalog = LoadCarCatalog();
            round.Place();
            for (var frame = 0; frame < 40; frame++) {
                yield return null;
            }

            // Out over the near side, so the car flies at the camera and the world's furniture stands behind the arena.
            var outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var right = Vector3.Cross(Vector3.up, outward);
            var rim = edge.FullExtent + 2f;
            while (rim > 0f && edge.DistanceToEdge(outward * rim, 0f) < 0f) {
                rim -= 0.05f;
            }

            var brink = outward * rim;
            var floor = round.Player.transform.position.y;

            // A car flying off the edge at the camera, the player's car charging behind it, two more coming in.
            var looks = new (string Car, int Color, string Topper, Vector3 Position, Quaternion Rotation)[]
            {
                ("monster-truck", 0, "crown", brink - outward * 1.7f - right * 1.1f + Vector3.up * (floor + 0.25f),
                    Quaternion.LookRotation(outward + right * 0.45f) * Quaternion.Euler(-10f, 0f, 0f)),
                ("racer", 1, "", brink + outward * 1.3f + right * 3f + Vector3.up * 1.3f,
                    Quaternion.LookRotation(right - outward * 0.6f) * Quaternion.Euler(-20f, 0f, -40f)),
                ("speedster", 2, "party-hat", brink - outward * 7f + right * 4.5f + Vector3.up * floor,
                    Quaternion.LookRotation(outward - right * 0.4f)),
                ("suv", 3, "", brink - outward * 7.5f - right * 4.5f + Vector3.up * floor,
                    Quaternion.LookRotation(outward + right * 0.6f)),
            };

            for (var i = 0; i < round.Cars.Count; i++) {
                var car = round.Cars[i];
                if (i >= looks.Length) {
                    car.gameObject.SetActive(false);
                    continue;
                }

                var look = car.GetComponent<CarLook>();
                look.Apply(1, catalog.Find(looks[i].Car), looks[i].Color, looks[i].Topper);
                var body = car.GetComponent<Rigidbody>();
                body.interpolation = RigidbodyInterpolation.None;
                car.Movement.Place(looks[i].Position, looks[i].Rotation);
                body.isKinematic = true;
            }

            // The player's marker reads as interface on a thumbnail.
            foreach (var marker in new[] { "Player Ring", "Player Arrow" }) {
                var found = GameObject.Find(marker);
                while (found != null) {
                    found.SetActive(false);
                    found = GameObject.Find(marker);
                }
            }

            Time.timeScale = 0f;
            var camera = Camera.main;
            camera.GetComponent<ArenaCamera>().enabled = false;
            var effects = Object.FindFirstObjectByType<EffectPlayer>();
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            var hit = brink - outward * 0.3f + right * 1.3f + Vector3.up * 0.9f;
            var lookAt = brink - outward * 3.5f + right * 0.9f + Vector3.up * 0.2f;
            try {
                camera.targetTexture = target;
                camera.aspect = 1f;
                OutlineBoost.Set(camera, 1.5f);
                foreach (var (name, offset, fov) in Cameras) {
                    var position = brink + outward * offset.x + right * offset.y + Vector3.up * offset.z;
                    camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                    camera.fieldOfView = fov;
                    if (theme != null) {
                        RenderSettings.fogStartDistance = position.magnitude + theme.FogStart;
                        RenderSettings.fogEndDistance = position.magnitude + theme.FogEnd;
                    }

                    foreach (var moment in Moments) {
                        Random.InitState(7);
                        effects.PlayHit(hit, right, 0.7f, true, false);
                        HideRings(effects);
                        effects.PlayKnockoutCredit(hit + right * 1.2f);
                        var until = Time.realtimeSinceStartup + moment;
                        while (Time.realtimeSinceStartup < until) {
                            yield return null;
                        }

                        // The effects queue their meshes in LateUpdate for that frame only (a batch-mode test gets no end of
                        // frame), so they are queued again right before this render.
                        DrawEffects.Invoke(effects, null);
                        camera.Render();
                        var previous = RenderTexture.active;
                        RenderTexture.active = target;
                        var picture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                        picture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                        picture.Apply();
                        RenderTexture.active = previous;
                        var path = Path.Combine(directory, $"{map}_{name}_{Mathf.RoundToInt(moment * 100f):00}.png");
                        File.WriteAllBytes(path, picture.EncodeToPNG());
                        Object.DestroyImmediate(picture);
                        Debug.Log($"Thumbnail candidate: {path}");

                        // Let the burst die out before the next one.
                        until = Time.realtimeSinceStartup + 2f;
                        while (Time.realtimeSinceStartup < until) {
                            yield return null;
                        }
                    }
                }
            }
            finally {
                OutlineBoost.Clear(camera);
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Time.timeScale = 1f;
            }

            yield return UnloadGame();
        }

        // The hit's ring lies on the floor round the point and, this close, fills the foreground: it is shrunk away.
        private static void HideRings(EffectPlayer effects)
        {
            var particles = (System.Array)Particles.GetValue(effects);
            var type = particles.GetType().GetElementType();
            var shape = type.GetField("Shape");
            var size = type.GetField("Size");
            var start = type.GetField("StartSize");
            for (var i = 0; i < particles.Length; i++) {
                var particle = particles.GetValue(i);
                if (shape.GetValue(particle).ToString() == "Ring") {
                    size.SetValue(particle, 0f);
                    start.SetValue(particle, 0f);
                    particles.SetValue(particle, i);
                }
            }
        }
    }
}
