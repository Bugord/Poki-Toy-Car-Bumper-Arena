using System.IO;
using ToyCarArena.Gameplay.Cars;
using UnityEditor;
using UnityEngine;

namespace ToyCarArena.Editor
{
    internal static class CarCatalogUpdater
    {
        private const string CatalogPath = "Assets/Data/Cars/data_car_catalog.asset";
        private const string PortraitFolder = "Assets/Art/Textures/Cars";
        private const int PortraitSize = 256;

        [MenuItem("Tools/Cars/Update Catalog")]
        private static void UpdateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>(CatalogPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/Car.prefab");
            if (catalog == null || prefab == null) {
                throw new MissingReferenceException("Update Catalog needs data_car_catalog and Car.prefab.");
            }

            var look = prefab.GetComponent<CarLook>();
            if (look == null || look.ModelMaterial == null) {
                throw new MissingReferenceException("Car.prefab needs CarLook and its body material.");
            }

            if (!AssetDatabase.IsValidFolder(PortraitFolder)) {
                AssetDatabase.CreateFolder("Assets/Art/Textures", "Cars");
            }

            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("cars");
            for (var i = 0; i < entries.arraySize; i++) {
                var entry = entries.GetArrayElementAtIndex(i);
                var id = entry.FindPropertyRelative("id").stringValue;
                var modelProperty = entry.FindPropertyRelative("model");
                // Also repairs an unset FBX root reference using Unity's importer, without guessing a local fileID.
                var model = modelProperty.objectReferenceValue as GameObject;
                if (model == null) {
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"Assets/Art/Models/Cars/mdl_car_{id.Replace('-', '_')}.fbx");
                    modelProperty.objectReferenceValue = model;
                }

                if (model == null) {
                    Debug.LogWarning($"Car {id} has no model; skipping its portrait.", catalog);
                    continue;
                }

                var body = CarModel.FindBody(model, out var swatch);
                entry.FindPropertyRelative("bodySwatch").vector2IntValue = swatch;
                AuditSwatch(model, body, swatch);
                var path = $"{PortraitFolder}/tex_car_{id.Replace('-', '_')}.png";
                RenderPortrait(model, swatch, look, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.maxTextureSize = PortraitSize;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                entry.FindPropertyRelative("portrait").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Debug.Log($"Car {id}: body {body.name}, paint {swatch}, portrait {path}.", catalog);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Car catalog updated.", catalog);
        }

        [MenuItem("Tools/Cars/Update Catalog", true)]
        private static bool CanUpdateCatalog() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void AuditSwatch(GameObject model, MeshFilter body, Vector2Int swatch)
        {
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true)) {
                if (filter == body || filter.sharedMesh == null) {
                    continue;
                }

                foreach (var uv in filter.sharedMesh.uv) {
                    if (CarModel.SwatchOf(uv) == swatch) {
                        Debug.LogWarning($"{model.name}: body paint {swatch} is also used by {filter.name}. " +
                            "That mesh is intentionally left unchanged.", model);
                        break;
                    }
                }
            }
        }

        private static void RenderPortrait(GameObject source, Vector2Int swatch, CarLook look, string path)
        {
            // PreviewRenderUtility owns an isolated preview scene, camera and lights. No scene, render settings or
            // gameplay components from the open arena are changed, and Cleanup releases the temporary model too.
            var preview = new PreviewRenderUtility();
            Mesh mesh = null;
            Texture2D portrait = null;
            var previewOpen = false;
            var previousTarget = RenderTexture.active;
            try {
                var root = new GameObject("Car Portrait");
                preview.AddSingleGO(root);
                root.transform.rotation = look.ModelRotation;
                var model = UnityEngine.Object.Instantiate(source, root.transform, false);
                CarModel.SetMaterial(model, look.ModelMaterial);
                mesh = CarModel.PaintBody(CarModel.FindBody(model, out _), swatch, look.PlayerSwatch);

                preview.ambientColor = new Color(0.55f, 0.55f, 0.55f);
                preview.lights[0].intensity = 1.1f;
                preview.lights[0].color = Color.white;
                preview.lights[0].transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                preview.lights[1].intensity = 0.65f;
                preview.lights[1].color = new Color(0.85f, 0.9f, 1f);
                preview.lights[1].transform.rotation = Quaternion.Euler(30f, 145f, 0f);

                var camera = preview.camera;
                camera.orthographic = true;
                camera.aspect = 1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.allowHDR = false;
                var bounds = CarModel.BoundsIn(model, Matrix4x4.identity);
                var distance = bounds.size.magnitude * 3f + 1f;
                camera.transform.position = bounds.center + new Vector3(4f, 3f, 5f).normalized * distance;
                camera.transform.LookAt(bounds.center);
                var projected = CarModel.BoundsIn(model, camera.transform.worldToLocalMatrix);
                camera.transform.position += camera.transform.right * projected.center.x
                    + camera.transform.up * projected.center.y;
                // Eight percent of the image on each side of the longest projected dimension.
                camera.orthographicSize = Mathf.Max(projected.extents.x, projected.extents.y) / 0.84f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = distance * 2f;

                var points = PortraitSize / EditorGUIUtility.pixelsPerPoint;
                preview.BeginPreview(new Rect(0f, 0f, points, points), GUIStyle.none);
                previewOpen = true;
                preview.Render(true);
                var rendered = (RenderTexture)preview.EndPreview();
                previewOpen = false;
                // EndStaticPreview creates RGB24 and discards alpha. Read the RGBA preview target ourselves instead.
                RenderTexture.active = rendered;
                portrait = new Texture2D(PortraitSize, PortraitSize, TextureFormat.RGBA32, false);
                portrait.ReadPixels(new Rect(0f, 0f, PortraitSize, PortraitSize), 0, 0);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) {
                    var pixels = portrait.GetPixels();
                    for (var i = 0; i < pixels.Length; i++) {
                        pixels[i] = pixels[i].gamma;
                    }

                    portrait.SetPixels(pixels);
                }

                portrait.Apply();
                File.WriteAllBytes(path, portrait.EncodeToPNG());
            }
            finally {
                if (previewOpen) {
                    preview.EndPreview();
                }

                RenderTexture.active = previousTarget;
                preview.Cleanup();
                if (mesh != null) {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }

                if (portrait != null) {
                    UnityEngine.Object.DestroyImmediate(portrait);
                }
            }
        }
    }
}
