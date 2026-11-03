using System;
using System.Collections.Generic;
using ToyCarArena.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// The 3D pictures of the garage: the car the player drives next, painted and wearing its topper, slowly turning,
    /// and every topper of the shop side by side in one texture the cards crop. Both are built from the same code as
    /// the cars on the arena (<see cref="CarLook.BuildModel"/>, <see cref="CarLook.WearTopper"/>), so the garage shows
    /// exactly what the next round will. Nothing here is gameplay: no colliders, nothing on the Arena layer, built only
    /// when the garage asks.
    /// <para>
    /// It lives in the Game scene with the podium, which is always loaded under the result the garage opens from, and
    /// lit by its light; the garage lets go of the textures when it closes and the scene takes everything with it.
    /// </para>
    /// </summary>
    public sealed class GarageStage : ITickable, IDisposable
    {
        // Far over the arena and the podium (1500 m), beyond the arena camera's far plane, and far enough from the
        // podium that neither stage camera, with its short far plane, sees the other.
        private static readonly Vector3 Stage = new(0f, 3000f, 0f);

        private const int HeroWidth = 960;
        private const int HeroHeight = 600;
        private const float TurnSeconds = 7f;
        private const float FieldOfView = 24f;
        private const float CameraPitch = 24f;
        // The car first faces three quarters to the camera, as the portraits do.
        private const float StartYaw = 215f;
        private const float HeroBoost = 3f;

        private const int Columns = 5;
        private const int Rows = 2;
        private const int CellSize = 256;
        private const float HatPitch = 16f;
        private const float HatYaw = 200f;
        // The share of its cell the topper fills, its widest or tallest side.
        private const float HatFill = 0.92f;
        private const float HatBoost = 6f;

        private readonly CarLook look;
        private readonly Transform parent;
        private readonly List<Mesh> heroMeshes = new();
        private readonly List<Mesh> hatMeshes = new();
        private readonly Dictionary<string, Rect> cells = new();
        private GameObject root;
        private Transform turntable;
        private Transform car;
        private Transform spinner;
        private Mesh body;
        private Camera heroCamera;
        private Camera hatCamera;
        private RenderTexture heroTexture;
        private RenderTexture hatTexture;
        private string shownKey;
        private float shownAt;
        private int hatFrame = -1;

        public GarageStage(Car carPrefab, Transform parent)
        {
            this.look = carPrefab.GetComponent<CarLook>();
            this.parent = parent;
        }

        /// <summary>
        /// Puts <paramref name="entry"/> on the turntable in the colour and the topper given (none for an empty id) and
        /// starts drawing it; the same car again keeps turning as it was. The texture is the stage's to release.
        /// </summary>
        public RenderTexture ShowCar(CarCatalog.Entry entry, int colorId, string topper)
        {
            Build();
            var key = $"{entry.Id}|{colorId}|{topper}";
            if (key != shownKey) {
                shownKey = key;
                BuildCar(entry, colorId, topper);
            }

            if (!heroCamera.enabled) {
                shownAt = Time.unscaledTime;
                heroCamera.enabled = true;
                OutlineBoost.Set(heroCamera, HeroBoost);
            }

            Tick();
            return heroTexture;
        }

        /// <summary>
        /// Draws every topper of <paramref name="ids"/> into one texture, once: a card shows its own part of it,
        /// <see cref="CellOf"/>. The stage keeps the texture.
        /// </summary>
        public RenderTexture ShowToppers(IReadOnlyList<string> ids)
        {
            Build();
            if (cells.Count == 0) {
                BuildToppers(ids);
            }

            // One frame is enough: the toppers stand still, and the phones have no fill to spare under the garage.
            hatFrame = Time.frameCount;
            hatCamera.enabled = true;
            OutlineBoost.Set(hatCamera, HatBoost);
            return hatTexture;
        }

        /// <summary>The part of the toppers' texture showing <paramref name="id"/>, in UV; empty for an unknown one.</summary>
        public Rect CellOf(string id) => id != null && cells.TryGetValue(id, out var cell) ? cell : Rect.zero;

        /// <summary>Stops drawing; the textures keep their last picture while the garage fades out.</summary>
        public void Hide()
        {
            if (heroCamera != null) {
                heroCamera.enabled = false;
                OutlineBoost.Clear(heroCamera);
                hatCamera.enabled = false;
                OutlineBoost.Clear(hatCamera);
            }
        }

        public void Tick()
        {
            if (hatCamera != null && hatCamera.enabled && Time.frameCount > hatFrame) {
                hatCamera.enabled = false;
                OutlineBoost.Clear(hatCamera);
            }

            if (heroCamera == null || !heroCamera.enabled) {
                return;
            }

            var time = Time.unscaledTime - shownAt;
            turntable.localRotation = Quaternion.Euler(0f, StartYaw + time / TurnSeconds * 360f, 0f);
            if (spinner != null) {
                spinner.localRotation = Quaternion.Euler(0f, time * 540f, 0f);
            }
        }

        public void Dispose()
        {
            Hide();
            if (root != null) {
                Object.Destroy(root);
            }

            foreach (var texture in new[] { heroTexture, hatTexture }) {
                if (texture != null) {
                    texture.Release();
                    Object.Destroy(texture);
                }
            }

            DestroyCar();
            foreach (var mesh in hatMeshes) {
                Object.Destroy(mesh);
            }
        }

        private void Build()
        {
            if (root != null) {
                return;
            }

            root = new GameObject("Garage Stage");
            root.transform.SetParent(parent, false);
            root.transform.position = Stage;
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(root.transform, false);

            heroTexture = NewTexture("Garage Car", HeroWidth, HeroHeight);
            heroCamera = StageCamera("Garage Car Camera", heroTexture);
            heroCamera.fieldOfView = FieldOfView;

            hatTexture = NewTexture("Garage Toppers", Columns * CellSize, Rows * CellSize);
            hatCamera = StageCamera("Garage Topper Camera", hatTexture);
            hatCamera.orthographic = true;
            hatCamera.orthographicSize = Rows * 0.5f;
        }

        private static RenderTexture NewTexture(string name, int width, int height)
        {
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) {
                name = name,
                antiAliasing = 1,
            };
            texture.Create();
            return texture;
        }

        private Camera StageCamera(string name, RenderTexture target)
        {
            var camera = new GameObject(name).AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 40f;
            camera.targetTexture = target;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.enabled = false;
            return camera;
        }

        // The car as the arena builds it, its footprint centred on the turntable, then the camera framing it whole
        // whichever way it has turned.
        private void BuildCar(CarCatalog.Entry entry, int colorId, string topper)
        {
            DestroyCar();
            car = new GameObject("Car").transform;
            car.SetParent(turntable, false);
            var model = CarLook.BuildModel(entry, car, look.ModelRotation, look.ModelMaterial, look.Reference);
            body = CarModel.PaintBody(CarModel.FindBody(model, out _), entry.BodySwatch, look.SwatchOf(colorId));
            CarLook.WearTopper(topper, entry.Id, car, model, look.ModelMaterial, heroMeshes, out spinner);

            var bounds = CarModel.BoundsIn(car.gameObject, car.worldToLocalMatrix);
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var reach = new Vector2(Mathf.Max(-bounds.min.x, bounds.max.x) - bounds.center.x,
                Mathf.Max(-bounds.min.z, bounds.max.z) - bounds.center.z).magnitude;
            Frame(reach, bounds.size.y);
        }

        // Far enough that a circle of the car's reach fits across and its height (seen a little from above) fits up.
        private void Frame(float reach, float height)
        {
            var halfUp = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            var halfAcross = halfUp * HeroWidth / HeroHeight;
            var pitch = CameraPitch * Mathf.Deg2Rad;
            var tall = height * 0.5f * Mathf.Cos(pitch) + reach * Mathf.Sin(pitch);
            var distance = Mathf.Max(reach / halfAcross, tall / halfUp) * 1.05f + reach;
            var rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            heroCamera.transform.localRotation = rotation;
            heroCamera.transform.localPosition = new Vector3(0f, height * 0.5f, 0f) - rotation * Vector3.forward * distance;
        }

        private void DestroyCar()
        {
            if (car != null) {
                Object.Destroy(car.gameObject);
                car = null;
            }

            spinner = null;
            if (body != null) {
                Object.Destroy(body);
                body = null;
            }

            foreach (var mesh in heroMeshes) {
                Object.Destroy(mesh);
            }

            heroMeshes.Clear();
        }

        // The toppers in a grid on a board that faces the orthographic camera, a cell of one metre each: each turned
        // three quarters to it and scaled to fill its cell, its widest or tallest side as seen.
        private void BuildToppers(IReadOnlyList<string> ids)
        {
            var rotation = Quaternion.Euler(HatPitch, 0f, 0f);
            var board = new GameObject("Toppers").transform;
            board.SetParent(root.transform, false);
            board.localPosition = new Vector3(100f, 0f, 0f);
            board.localRotation = rotation;
            hatCamera.transform.localRotation = rotation;
            hatCamera.transform.localPosition = board.localPosition - rotation * Vector3.forward * 10f;

            for (var i = 0; i < ids.Count && i < Columns * Rows; i++) {
                var column = i % Columns;
                var row = i / Columns;
                var holder = new GameObject(ids[i]).transform;
                holder.SetParent(board, false);
                holder.rotation = Quaternion.Euler(0f, HatYaw, 0f);
                var hat = CarTopper.Build(ids[i], holder, look.ModelMaterial, hatMeshes, out _);
                if (hat == null) {
                    Object.Destroy(holder.gameObject);
                    continue;
                }

                Outline.Apply(hat.gameObject);
                var seen = CarModel.BoundsIn(hat.gameObject, board.worldToLocalMatrix);
                var scale = HatFill / Mathf.Max(seen.size.x, seen.size.y);
                var cell = new Vector3(column - (Columns - 1) * 0.5f, (Rows - 1) * 0.5f - row, 0f);
                holder.localScale = Vector3.one * scale;
                holder.localPosition = cell - (seen.center - holder.localPosition) * scale;
                cells[ids[i]] = new Rect((float)column / Columns, 1f - (row + 1f) / Rows, 1f / Columns, 1f / Rows);
            }
        }
    }
}
