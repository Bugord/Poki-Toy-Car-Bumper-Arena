using System;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// The match winner's car on a turning podium, for the result screen: a copy of the car as it drove (model, colour,
    /// comic outline) stands on a gold podium far above the arena, and a camera of its own draws them into a texture with
    /// a clear background, which the screen lays over its rays. Nothing here is gameplay: no colliders, nothing on the
    /// Arena layer, built only when the screen asks.
    /// <para>
    /// It lives in the Game scene because the copy shares the winner's painted mesh, which goes with the scene; the
    /// result screen lets go of the texture before the next arena loads.
    /// </para>
    /// </summary>
    public sealed class WinnerPodium : ITickable, IDisposable
    {
        // Far over the arena and beyond its camera's far plane, so the arena camera never sees the podium and the
        // podium camera (with a short far plane of its own) never sees the arena or its world.
        private static readonly Vector3 Stage = new(0f, 1500f, 0f);

        /// <summary>
        /// Where the top of the car's roof over the turning axis is in the picture, as a share of its height from the top
        /// (its middle across): every car is framed to put it there, so the result screen can set a crown on it.
        /// </summary>
        public const float RoofTop = 0.12f;

        // Twice as wide as tall: the car and its podium are wide, and on a phone held upright the picture gets a short,
        // wide box over the cards, which it fits by its height.
        private const int TextureWidth = 1024;
        private const int TextureHeight = 512;
        private const float TurnSeconds = 4f;
        // The car hops off the podium with a whole turn as it comes in, then settles to the slow turn.
        private const float HopSeconds = 0.55f;
        // Low, so the roof stays in the picture, which keeps little room over it.
        private const float HopHeight = 0.3f;
        // A little wider than a car's diagonal: the podium is the car's stage, not the picture's subject.
        private const float BaseRadius = 1.6f;
        private const float BaseHeight = 0.34f;
        private const float TopRadius = 1.38f;
        private const float TopHeight = 0.18f;
        private const float FieldOfView = 26f;
        private const float CameraPitch = 13f;
        // The car first faces three quarters to the camera, as the portraits do.
        private const float StartYaw = 215f;
        // Room kept between the podium or the car and the picture's edges, as a share of the picture, for the outline.
        private const float FrameMargin = 0.03f;
        // The texture shows at about its own size on a phone held upright; its short side is under the 1080 the
        // outline's width is measured at, so the line is drawn a little thicker to read as the arena's.
        private const float LineBoost = 1.6f;

        private readonly ArenaRound round;
        private readonly MechanicArt art;
        private readonly Transform parent;
        private GameObject root;
        private Transform turntable;
        private Transform car;
        private Camera camera;
        private RenderTexture texture;
        private Mesh[] meshes;
        private float shownAt;
        private float carBase;

        public WinnerPodium(ArenaRound round, MechanicArt art, Transform parent)
        {
            this.round = round;
            this.art = art;
            this.parent = parent;
        }

        /// <summary>
        /// Puts the car at <paramref name="index"/> of the round on the podium and starts drawing it; the texture is the
        /// podium's to release. Null when the round has no such car.
        /// </summary>
        public RenderTexture Show(int index)
        {
            if (index < 0 || index >= round.Cars.Count || !round.Cars[index].TryGetComponent<CarLook>(out var look) ||
                look.Model == null) {
                return null;
            }

            return Show(look.Model, look.ModelRotation, look.ModelScale, look.ModelMaterial);
        }

        /// <summary>
        /// Puts a copy of a car's model (as <see cref="CarLook"/> holds it: turned and scaled under the car) on the
        /// podium and starts drawing it; the screenshot tests show a catalog car this way, without a round.
        /// </summary>
        public RenderTexture Show(GameObject model, Quaternion rotation, Vector3 scale, Material material)
        {
            Build();
            if (car != null) {
                Object.Destroy(car.gameObject);
            }

            car = CopyCar(model, rotation, scale, material);
            shownAt = Time.unscaledTime;
            camera.enabled = true;
            OutlineBoost.Set(camera, LineBoost);
            Tick();
            return texture;
        }

        public void Tick()
        {
            if (camera == null || !camera.enabled) {
                return;
            }

            var time = Time.unscaledTime - shownAt;
            var hop = Mathf.Clamp01(time / HopSeconds);
            // One extra whole turn eased out over the hop, then the turntable's own slow turn.
            var spin = 360f * (1f - (1f - hop) * (1f - hop)) + time / TurnSeconds * 360f;
            turntable.localRotation = Quaternion.Euler(0f, StartYaw + spin, 0f);
            if (car != null) {
                car.localPosition = new Vector3(0f, carBase + Mathf.Sin(hop * Mathf.PI) * HopHeight, 0f);
            }
        }

        public void Dispose()
        {
            if (camera != null) {
                OutlineBoost.Clear(camera);
            }

            if (root != null) {
                Object.Destroy(root);
            }

            if (texture != null) {
                texture.Release();
                Object.Destroy(texture);
            }

            if (meshes != null) {
                foreach (var mesh in meshes) {
                    Object.Destroy(mesh);
                }
            }
        }

        private void Build()
        {
            if (root != null) {
                return;
            }

            root = new GameObject("Winner Podium");
            root.transform.SetParent(parent, false);
            root.transform.position = Stage;

            meshes = new[] {
                ToyMeshes.Cylinder(BaseRadius, BaseRadius * 0.92f, BaseHeight, 40),
                ToyMeshes.Cylinder(TopRadius, TopRadius, TopHeight, 40),
                ToyMeshes.Box(new Vector3(0.36f, 0.36f, 0.14f)),
            };
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(root.transform, false);
            ToyMeshes.Part(turntable, "Base", meshes[0], art.Get("train_yellow"), new Vector3(0f, -TopHeight - BaseHeight * 0.5f, 0f));
            ToyMeshes.Part(turntable, "Top", meshes[1], art.Get("train_white"), new Vector3(0f, -TopHeight * 0.5f, 0f));
            // Red studs round the rim show the turn even while the car faces the camera.
            for (var i = 0; i < 8; i++) {
                var angle = i * Mathf.PI * 2f / 8f;
                var at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * BaseRadius * 0.96f;
                ToyMeshes.Part(turntable, "Stud", meshes[2], art.Get("train_red"),
                    at + Vector3.up * (-TopHeight - BaseHeight * 0.5f), new Vector3(0f, 90f - angle * Mathf.Rad2Deg, 0f));
            }
            ToyMeshes.Outlined(turntable.gameObject);

            texture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32) {
                name = "Winner Podium",
                antiAliasing = 1,
            };
            texture.Create();

            camera = new GameObject("Podium Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.fieldOfView = FieldOfView;
            camera.aspect = (float)TextureWidth / TextureHeight;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 40f;
            camera.targetTexture = texture;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.enabled = false;
        }

        // A copy of the car's model as the round drew it: its own painted body, the normal material even if the car
        // ended the match see-through after coming back, and the outline. The car itself may already be switched off
        // below the arena; the copy is not.
        private Transform CopyCar(GameObject source, Quaternion rotation, Vector3 scale, Material material)
        {
            var holder = new GameObject("Car").transform;
            holder.SetParent(turntable, false);
            var model = Object.Instantiate(source, holder, false);
            model.SetActive(true);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = rotation;
            // The car's own size: a match can end with a mega item still running.
            model.transform.localScale = scale;
            CarModel.SetMaterial(model, material);
            Outline.Apply(model);

            // The model's footprint centred on the podium, its wheels on the top.
            var bounds = CarModel.BoundsIn(model, holder.worldToLocalMatrix);
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            carBase = 0f;
            Frame(bounds.size);
            return holder;
        }

        // The camera looks a little down at the podium and the car, with the roof over the axis at RoofTop and as close
        // as lets the podium's foot and both sides of the car and the podium in: a tall car stands further away, so
        // every car's roof is in the same place and the whole stage stays in the picture.
        private void Frame(Vector3 carSize)
        {
            var rotation = Quaternion.Euler(CameraPitch, 180f, 0f);
            camera.transform.localRotation = rotation;
            var roof = new Vector3(0f, carSize.y, 0f);
            var bottom = -TopHeight - BaseHeight;
            var half = new Vector2(carSize.x, carSize.z).magnitude * 0.5f;
            var tan = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            Span<Vector3> edges = stackalloc Vector3[] {
                new(0f, bottom, BaseRadius), new(BaseRadius, bottom, 0f), new(-BaseRadius, bottom, 0f),
                new(BaseRadius * 0.7f, bottom, BaseRadius * 0.7f), new(-BaseRadius * 0.7f, bottom, BaseRadius * 0.7f),
                new(half, carSize.y, 0f), new(-half, carSize.y, 0f), new(half, 0f, 0f), new(-half, 0f, 0f),
            };
            for (var distance = 3f; distance < 40f; distance *= 1.02f) {
                // In the camera's own space the roof is this far ahead and as high as puts it at RoofTop.
                var height = (0.5f - RoofTop) * 2f * distance * tan;
                camera.transform.localPosition = roof - rotation * new Vector3(0f, height, distance);
                if (Fits(edges)) {
                    break;
                }
            }
        }

        private bool Fits(Span<Vector3> points)
        {
            foreach (var point in points) {
                var at = camera.WorldToViewportPoint(root.transform.TransformPoint(point));
                if (at.x < FrameMargin || at.x > 1f - FrameMargin || at.y < FrameMargin || at.y > 1f - FrameMargin) {
                    return false;
                }
            }

            return true;
        }
    }
}
