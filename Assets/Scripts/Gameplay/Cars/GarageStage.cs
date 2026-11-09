using System;
using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
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

        // The main menu stands the car on a plain 3D pedestal in the stickers' colours, turning with it: a navy drum,
        // a gold band round its top and a top set in from the band, so a gold rim shows round it. The top takes the
        // colour of the player's league; the drum and the band are the same in every league.
        private const float PedestalRadius = 2f;
        private const float PedestalHeight = 0.45f;
        private const float BandHeight = 0.1f;
        private const float TopHeight = 0.04f;
        private const float TopRadius = 1.8f;
        // The car's shadow on the top: a flat navy oval a little wider than the car, as the arena's spots read.
        private const float ShadowLift = 0.004f;
        private const float ShadowSpread = 1.1f;

        private readonly CarLook look;
        private readonly MechanicArt art;
        private readonly Transform parent;
        private readonly List<Mesh> heroMeshes = new();
        private readonly List<Mesh> hatMeshes = new();
        private readonly Dictionary<string, Rect> cells = new();
        private GameObject root;
        private Transform turntable;
        private Transform car;
        private Transform spinner;
        private Transform pedestal;
        private Mesh[] pedestalMeshes;
        // The top's own copy of the white material, tinted by league: the white one is shared with the train and the
        // podium, and tinting it in the editor would write into the asset.
        private Material pedestalTop;
        private Transform shadow;
        private Mesh body;
        private Camera heroCamera;
        private Camera hatCamera;
        private RenderTexture heroTexture;
        private RenderTexture hatTexture;
        private string shownKey;
        private float shownAt;
        // Seconds the turntable had turned when it was last hidden: the next ShowCar turns on from there, so the car
        // keeps its turn from the garage back to the menu as it does from the menu into the garage (which finds the
        // camera still on).
        private float turned;
        private int hatFrame = -1;

        public GarageStage(Car carPrefab, MechanicArt art, Transform parent)
        {
            this.look = carPrefab.GetComponent<CarLook>();
            this.art = art;
            this.parent = parent;
        }

        /// <summary>
        /// Where the turntable's middle, the ground under the car, falls in the car's texture: x from the left, y from
        /// the top, both 0..1. The camera frames each car by its own size, so the garage stands the picture on its
        /// plinth by this point.
        /// </summary>
        public Vector2 HeroGround { get; private set; } = new(0.5f, 0.5f);

        /// <summary>The turntable's turn in degrees about the vertical, as last drawn.</summary>
        public float Yaw => turntable != null ? turntable.localEulerAngles.y : StartYaw;

        /// <summary>
        /// Puts <paramref name="entry"/> on the turntable in the colour and the topper given (none for an empty id) and
        /// starts drawing it. The turntable turns on from where it was, even after <see cref="Hide"/> and for another
        /// car, colour or topper: the menu and the garage hand the car to each other mid-turn. With <paramref name="onPedestal"/> it stands
        /// on a 3D pedestal framed with it (the main menu); without, on nothing (the garage has a plinth of its own). The
        /// texture is the stage's to release. <paramref name="pedestalTopColor"/> tints the pedestal's top (the menu's
        /// league); without it the top is the white of the stickers.
        /// </summary>
        public RenderTexture ShowCar(CarCatalog.Entry entry, int colorId, string topper, bool onPedestal = false,
            Color? pedestalTopColor = null)
        {
            Build();
            var key = $"{entry.Id}|{colorId}|{topper}|{onPedestal}";
            if (key != shownKey) {
                shownKey = key;
                BuildCar(entry, colorId, topper, onPedestal);
            }

            if (pedestalTop != null) {
                pedestalTop.color = pedestalTopColor ?? art.Get("train_white").color;
            }

            if (!heroCamera.enabled) {
                shownAt = Time.unscaledTime - turned;
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
                // Every topper, on sale and exclusive, whatever the first caller asks for: the texture is drawn once,
                // and the garage, the result's gift and the menu's daily gift each crop their own from it.
                var all = new List<string>(CarTopper.Ids);
                all.AddRange(CarTopper.ExclusiveIds);
                foreach (var id in ids) {
                    if (!all.Contains(id)) {
                        all.Add(id);
                    }
                }

                BuildToppers(all);
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
                if (heroCamera.enabled) {
                    turned = Time.unscaledTime - shownAt;
                }

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
            if (pedestalTop != null) {
                Object.Destroy(pedestalTop);
            }

            if (pedestalMeshes != null) {
                foreach (var mesh in pedestalMeshes) {
                    Object.Destroy(mesh);
                }
            }

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
        private void BuildCar(CarCatalog.Entry entry, int colorId, string topper, bool onPedestal)
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
            if (onPedestal) {
                BuildPedestal();
            }

            if (pedestal != null) {
                pedestal.gameObject.SetActive(onPedestal);
            }

            if (onPedestal) {
                // The car's footprint, recentred on the turntable above; the oval turns with the car.
                shadow.localScale = new Vector3(bounds.size.x * 0.5f * ShadowSpread, 1f,
                    bounds.size.z * 0.5f * ShadowSpread);
            }

            var low = onPedestal ? PedestalHeight : 0f;
            Frame(onPedestal ? Mathf.Max(reach, PedestalRadius) : reach, bounds.size.y + low, -low);
        }

        // The pedestal under the car: its top is the ground the car stands on.
        private void BuildPedestal()
        {
            if (pedestal != null) {
                return;
            }

            pedestalMeshes = new[] {
                ToyMeshes.Cylinder(PedestalRadius, PedestalRadius, PedestalHeight - BandHeight, 48),
                ToyMeshes.Cylinder(PedestalRadius, PedestalRadius, BandHeight - TopHeight, 48),
                ToyMeshes.Cylinder(TopRadius, TopRadius, TopHeight, 48),
                ToyMeshes.Disc(1f, 32),
            };
            pedestal = new GameObject("Pedestal").transform;
            pedestal.SetParent(turntable, false);
            ToyMeshes.Part(pedestal, "Side", pedestalMeshes[0], art.Get("train_dark"),
                new Vector3(0f, -BandHeight - (PedestalHeight - BandHeight) * 0.5f, 0f));
            ToyMeshes.Part(pedestal, "Band", pedestalMeshes[1], art.Get("train_yellow"),
                new Vector3(0f, -BandHeight + (BandHeight - TopHeight) * 0.5f, 0f));
            pedestalTop = new Material(art.Get("train_white")) { name = "Pedestal Top" };
            ToyMeshes.Part(pedestal, "Top", pedestalMeshes[2], pedestalTop,
                new Vector3(0f, -TopHeight * 0.5f, 0f));
            ToyMeshes.Outlined(pedestal.gameObject);
            // After the outline: a flat sticker takes none.
            shadow = ToyMeshes.Part(pedestal, "Shadow", pedestalMeshes[3], art.Get("top_shadow"),
                new Vector3(0f, ShadowLift, 0f));
        }

        // Far enough that a circle of the car's reach fits across and its height (seen a little from above) fits up;
        // <paramref name="bottom"/> is where that height starts, below the ground for a pedestal.
        private void Frame(float reach, float height, float bottom)
        {
            var halfUp = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            var halfAcross = halfUp * HeroWidth / HeroHeight;
            var pitch = CameraPitch * Mathf.Deg2Rad;
            var tall = height * 0.5f * Mathf.Cos(pitch) + reach * Mathf.Sin(pitch);
            var distance = Mathf.Max(reach / halfAcross, tall / halfUp) * 1.05f + reach;
            var rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            heroCamera.transform.localRotation = rotation;
            heroCamera.transform.localPosition = new Vector3(0f, bottom + height * 0.5f, 0f) -
                rotation * Vector3.forward * distance;
            var ground = heroCamera.WorldToViewportPoint(turntable.position);
            HeroGround = new Vector2(ground.x, 1f - ground.y);
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
