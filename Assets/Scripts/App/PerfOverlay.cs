#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using ToyCarArena.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if BUGORD_SUPERCONSOLE
// Inside this namespace the bare name SuperConsole would be the Bugord.SuperConsole namespace.
using DevConsole = Bugord.SuperConsole.SuperConsole;
#endif

namespace ToyCarArena.App
{
    /// <summary>
    /// A small readout for development builds, off until <see cref="PerfDevTools"/> enables the component: frames per second, average and worst frame time over the last
    /// second, and the size the 3D scene renders at. Meant for a real phone, where the editor's profiler cannot go.
    /// IMGUI, because it needs no panel or asset and draws over everything; the text is rebuilt twice a second and the
    /// frame times come from a ring buffer (<see cref="FrameStats"/>), so a frame allocates nothing. While the component is
    /// disabled Unity calls neither <c>Update</c> nor <c>OnGUI</c>, so a hidden readout costs nothing.
    /// </summary>
    public sealed class PerfOverlay : MonoBehaviour
    {
        private const float Window = 1f;
        private const float Refresh = 0.5f;

        private readonly StringBuilder builder = new(128);
        private readonly GUIContent content = new();

        private FrameStats stats;
        private RenderScaler scaler;
        private GUIStyle style;
        private Texture2D background;
        private float sinceRefresh = Refresh;
        private int styleHeight = -1;

        public void Bind(FrameStats frameStats, RenderScaler renderScaler)
        {
            stats = frameStats;
            scaler = renderScaler;
        }

        private void Awake()
        {
            background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            background.Apply();
        }

        private void OnEnable()
        {
            // Rebuilt on the first Update, before this frame's OnGUI, instead of showing numbers from when it was hidden.
            sinceRefresh = Refresh;
            content.text = null;
        }

        private void OnDestroy()
        {
            if (background != null) {
                Destroy(background);
            }
        }

        private void Update()
        {
            sinceRefresh += Time.unscaledDeltaTime;
            if (sinceRefresh >= Refresh) {
                sinceRefresh = 0f;
                Rebuild();
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(content.text)) {
                return;
            }

#if BUGORD_SUPERCONSOLE
            // IMGUI draws over the console, on top of its command line; the console has the same numbers pinned.
            if (DevConsole.IsVisible) {
                return;
            }
#endif

            // The text is sized to the screen's short side, so it reads the same on a phone and on a desktop.
            var shortSide = Mathf.Min(Screen.width, Screen.height);
            if (style == null || styleHeight != shortSide) {
                styleHeight = shortSide;
                style = new GUIStyle(GUI.skin.label) {
                    fontSize = Mathf.Max(12, Mathf.RoundToInt(shortSide / 1080f * 30f)),
                    padding = new RectOffset(10, 10, 6, 6),
                    wordWrap = false,
                    normal = { textColor = Color.white, background = background },
                };
            }

            var size = style.CalcSize(content);
            GUI.Label(new Rect(8f, Screen.height - size.y - 8f, size.x, size.y), content, style);
        }

        private void Rebuild()
        {
            if (stats == null || !stats.Measure(Window, out var average, out var worst)) {
                return;
            }

            var render = scaler != null ? scaler.RenderSize : new Vector2Int(Screen.width, Screen.height);
            var scale = scaler != null ? scaler.Scale : 1f;
            var msaa = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset ? asset.msaaSampleCount : 1;

            builder.Clear();
            builder.Append(Mathf.RoundToInt(1f / average)).Append(" fps   avg ").Append((average * 1000f).ToString("0.0"))
                .Append(" ms   max ").Append((worst * 1000f).ToString("0.0")).Append(" ms\n");
            builder.Append("3D ").Append(render.x).Append('x').Append(render.y).Append("  scale ").Append(scale.ToString("0.00"))
                .Append("  msaa ").Append(msaa).Append("\nscreen ").Append(Screen.width).Append('x').Append(Screen.height);
            content.text = builder.ToString();
        }
    }
}
#endif
