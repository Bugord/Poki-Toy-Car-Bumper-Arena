using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ToyCarArena.Rendering
{
    /// <summary>
    /// How much thicker the comic outline is drawn by a camera that renders into a texture shown smaller than itself (the
    /// podium, the garage). The line's width is one global of <c>sh_outline</c>, so one handler sets it before every
    /// camera: the registered boost for the cameras here, 1 for all the others. With a handler per owner the last one to
    /// run would win for every camera.
    /// </summary>
    public static class OutlineBoost
    {
        private static readonly int BoostId = Shader.PropertyToID("_OutlineBoost");
        private static readonly Dictionary<Camera, float> Boosts = new();

        public static void Set(Camera camera, float boost)
        {
            if (Boosts.Count == 0) {
                RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            }

            Boosts[camera] = boost;
        }

        public static void Clear(Camera camera)
        {
            if (!Boosts.Remove(camera) || Boosts.Count > 0) {
                return;
            }

            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Shader.SetGlobalFloat(BoostId, 1f);
        }

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera) =>
            Shader.SetGlobalFloat(BoostId, Boosts.TryGetValue(camera, out var boost) ? boost : 1f);
    }
}
