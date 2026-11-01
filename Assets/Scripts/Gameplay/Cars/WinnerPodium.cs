using System;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
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

        private const int TextureSize = 768;
        private const float TurnSeconds = 4f;
        // The car hops off the podium with a whole turn as it comes in, then settles to the slow turn.
        private const float HopSeconds = 0.55f;
        private const float HopHeight = 0.9f;
        private const float BaseRadius = 2.1f;
        private const float BaseHeight = 0.7f;
        private const float TopRadius = 1.8f;
        private const float TopHeight = 0.18f;
        private const float FieldOfView = 26f;
        private const float CameraPitch = 16f;
        // The car first faces three quarters to the camera, as the portraits do.
        private const float StartYaw = 215f;
        // The texture shows at about half its size, so its comic outline is drawn thicker to read as the arena's.
        private const float OutlineBoost = 2.4f;
        private static readonly int OutlineBoostId = Shader.PropertyToID("_OutlineBoost");

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

            Build();
            if (car != null) {
                Object.Destroy(car.gameObject);
            }

            car = CopyCar(look);
            shownAt = Time.unscaledTime;
            camera.enabled = true;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
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
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Shader.SetGlobalFloat(OutlineBoostId, 1f);
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

        // Every camera sets the line for its own render: thick for the podium, as usual for the arena.
        private void OnBeginCamera(ScriptableRenderContext context, Camera rendered) =>
            Shader.SetGlobalFloat(OutlineBoostId, rendered == camera ? OutlineBoost : 1f);

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

            texture = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32) {
                name = "Winner Podium",
                antiAliasing = 1,
            };
            texture.Create();

            camera = new GameObject("Podium Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.fieldOfView = FieldOfView;
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
        private Transform CopyCar(CarLook look)
        {
            var holder = new GameObject("Car").transform;
            holder.SetParent(turntable, false);
            var model = Object.Instantiate(look.Model, holder, false);
            model.SetActive(true);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = look.ModelRotation;
            model.transform.localScale = look.Model.transform.localScale;
            CarModel.SetMaterial(model, look.ModelMaterial);
            Outline.Apply(model);

            // The model's footprint centred on the podium, its wheels on the top.
            var bounds = CarModel.BoundsIn(model, holder.worldToLocalMatrix);
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            carBase = 0f;
            Frame(bounds.size);
            return holder;
        }

        // The camera sees the podium and the car whole, from the podium's foot to the car's roof, looking a little down.
        private void Frame(Vector3 carSize)
        {
            var bottom = -TopHeight - BaseHeight;
            var top = carSize.y + HopHeight * 0.3f;
            var halfWidth = Mathf.Max(BaseRadius, new Vector2(carSize.x, carSize.z).magnitude * 0.5f);
            // The near side of the podium's foot reaches lower than its middle, so the frame keeps room under it.
            var radius = Mathf.Max(halfWidth, (top - bottom) * 0.5f) * 1.14f;
            var target = new Vector3(0f, (top + bottom) * 0.5f - 0.2f, 0f);
            var distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad);
            var rotation = Quaternion.Euler(CameraPitch, 180f, 0f);
            camera.transform.localRotation = rotation;
            camera.transform.localPosition = target - rotation * Vector3.forward * distance;
        }
    }
}
