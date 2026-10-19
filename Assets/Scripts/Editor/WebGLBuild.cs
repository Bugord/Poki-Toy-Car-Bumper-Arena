using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ToyCarArena.Editor
{
    /// <summary>
    /// Builds the Poki release: WebGL with <c>POKI_ENABLED</c>, the scenes from the build settings, into
    /// <c>Builds/WebGL</c> (ignored by git), then writes <c>Builds/WebGL-report.txt</c> with the size of every output
    /// file and the biggest assets. Tools > Build > WebGL (Poki), or <c>Tools/build-webgl.sh</c> in batch mode; WebGL
    /// Development (Poki) or <c>-development</c> makes a development build. Super Console leaves itself out of a release
    /// build (its Include In Builds setting is development builds only), and the report counts its assets that shipped.
    /// The wasm is built for disk size; compression stays off, since Poki compresses the files itself.
    /// The build carries its <see cref="BuildNumber"/>: in <c>Resources/data_build_info.txt</c> while it builds, which
    /// the game logs at startup (<see cref="ToyCarArena.App.BuildInfo"/>), in <c>build.json</c> beside <c>index.json</c>, which
    /// the upload names the version after, and in the report.
    /// </summary>
    public static class WebGLBuild
    {
        private const string PokiDefine = "POKI_ENABLED";
        private const string OutputPath = "Builds/WebGL";
        private const string ReportPath = "Builds/WebGL-report.txt";
        // Ignored by git: it only exists while the build runs.
        private const string BuildInfoAsset = "Assets/Resources/" + ToyCarArena.App.BuildInfo.ResourceName + ".txt";
        // The build number and commit for the upload, beside index.json (in its "key": "value" lines).
        internal const string BuildFile = "build.json";
        private const int TopAssetCount = 30;
        // In a release build none of its assets should ship (its settings asset is in Assets/Resources).
        private const string SuperConsolePackage = "com.superconsole.runtime";

        // -development (Tools/build-webgl.sh --development) for a build that names the line of every exception.
        [MenuItem("Tools/Build/WebGL (Poki)")]
        public static void Build() => Run(Environment.GetCommandLineArgs().Contains("-development"));

        /// <summary>
        /// A development build: Super Console in it, and exceptions name their lines.
        /// </summary>
        [MenuItem("Tools/Build/WebGL Development (Poki)")]
        public static void BuildDevelopment() => Run(true);

        // Not an overload of Build: -executeMethod finds the method by name alone.
        private static void Run(bool development)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
            if (!defines.Split(';').Contains(PokiDefine)) {
                throw new BuildFailedException($"Add {PokiDefine} to the WebGL scripting defines first.");
            }

            // On for playing in an unfocused Editor; in the player the game would run on with the page in the background.
            if (PlayerSettings.runInBackground && !development) {
                throw new BuildFailedException("Run In Background is on; turn it off (Player Settings > Resolution and Presentation) for the release.");
            }

            // -noLTO (the development builds on GitHub Actions): on a small runner the link with LTO takes longer than
            // the rest of the build, for a few percent of wasm.
            SetCodeOptimization(Environment.GetCommandLineArgs().Contains("-noLTO") ? "DiskSize" : "DiskSizeLTO");

            var build = BuildNumber.Current();
            var buildFile = Path.Combine(OutputPath, BuildFile);
            // A failed build leaves no number behind, so an older build in its place cannot be uploaded under it.
            if (File.Exists(buildFile)) {
                File.Delete(buildFile);
            }

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            BuildReport report;
            // Super Console keeps its settings there, but a project without it may have no Resources folder yet.
            Directory.CreateDirectory(Path.GetDirectoryName(BuildInfoAsset));
            File.WriteAllText(BuildInfoAsset, $"{build.Number}\n{build.Commit}\n");
            AssetDatabase.ImportAsset(BuildInfoAsset, ImportAssetOptions.ForceSynchronousImport);
            try {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally {
                AssetDatabase.DeleteAsset(BuildInfoAsset);
            }

            WriteReport(report, build);
            if (report.summary.result != BuildResult.Succeeded) {
                throw new BuildFailedException($"WebGL build {report.summary.result}, see {ReportPath}");
            }

            File.WriteAllText(buildFile,
                $"{{\n  \"build_number\": \"{build.Number}\",\n  \"commit\": \"{build.Commit}\"\n}}\n");
            Debug.Log($"[{nameof(WebGLBuild)}] Built {OutputPath}, build {build}, " +
                $"{FormatSize(report.summary.totalSize)}; see {ReportPath}");
        }

        /// <summary>
        /// Sets the WebGL Code Optimization build setting. Its type lives in the WebGL module's editor assembly, which
        /// this assembly cannot reference, so it is set by reflection.
        /// </summary>
        private static void SetCodeOptimization(string value)
        {
            var settings = Type.GetType("UnityEditor.WebGL.UserBuildSettings, UnityEditor.WebGL.Extensions");
            var property = settings?.GetProperty("codeOptimization");
            if (property == null) {
                Debug.LogWarning($"[{nameof(WebGLBuild)}] WebGL code optimization setting not found; left as it is.");
                return;
            }

            property.SetValue(null, Enum.Parse(property.PropertyType, value));
        }

        private static void WriteReport(BuildReport report, BuildNumber build)
        {
            var consoleAssets = report.packedAssets
                .SelectMany(packed => packed.contents)
                // Scripts are listed for every type the player's assemblies hold; stripping removes the console's code.
                .Count(content => content.type != typeof(MonoScript) &&
                    content.sourceAssetPath?.Contains(SuperConsolePackage) == true);
            var text = new StringBuilder();
            text.AppendLine($"Build: {PlayerSettings.productName} v{PlayerSettings.bundleVersion} ({build.Number}), " +
                $"commit {build.Commit}");
            text.AppendLine($"Result: {report.summary.result}, {report.summary.totalTime:mm\\:ss}");
            text.AppendLine($"Development: {(report.summary.options & BuildOptions.Development) != 0}, " +
                $"compression: {PlayerSettings.WebGL.compressionFormat}, " +
                $"stripping: {PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL)}, " +
                $"Super Console assets: {consoleAssets}");
            text.AppendLine();

            text.AppendLine("Output files:");
            if (Directory.Exists(OutputPath)) {
                var files = Directory.GetFiles(OutputPath, "*", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.Length)
                    .ToList();
                foreach (var file in files) {
                    text.AppendLine($"  {FormatSize(file.Length),10}  {Path.GetRelativePath(OutputPath, file.FullName)}");
                }

                text.AppendLine($"  {FormatSize(files.Sum(file => file.Length)),10}  total");
            }

            text.AppendLine();
            text.AppendLine($"Biggest assets (packed, top {TopAssetCount}):");
            var assets = report.packedAssets
                .SelectMany(packed => packed.contents)
                .GroupBy(content => string.IsNullOrEmpty(content.sourceAssetPath) ? "(built-in)" : content.sourceAssetPath)
                .Select(group => (path: group.Key, size: group.Sum(content => (long)content.packedSize)))
                .OrderByDescending(asset => asset.size)
                .ToList();
            foreach (var (path, size) in assets.Take(TopAssetCount)) {
                text.AppendLine($"  {FormatSize(size),10}  {path}");
            }

            text.AppendLine();
            text.AppendLine("Packed assets by type:");
            foreach (var group in report.packedAssets.SelectMany(packed => packed.contents)
                         .GroupBy(content => content.type?.Name ?? "?")
                         .Select(group => (type: group.Key, size: group.Sum(content => (long)content.packedSize)))
                         .OrderByDescending(group => group.size)) {
                text.AppendLine($"  {FormatSize(group.size),10}  {group.type}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, text.ToString());
        }

        private static string FormatSize(long bytes)
        {
            return bytes >= 1024 * 1024 ? $"{bytes / (1024f * 1024f):0.00} MB" : $"{bytes / 1024f:0.0} KB";
        }

        private static string FormatSize(ulong bytes) => FormatSize((long)bytes);
    }
}
