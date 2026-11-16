using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ToyCarArena.Gameplay;
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
    /// The floors past the first round's, the car portraits and the music go into bundles under StreamingAssets
    /// (<see cref="DeferredContent"/>), downloaded after the start; the report counts any of them still in the player.
    /// The build carries its <see cref="BuildNumber"/>: in <c>Resources/data_build_info.txt</c> while it builds, which
    /// the game logs at startup (<see cref="ToyCarArena.App.BuildInfo"/>), in <c>build.json</c> beside <c>index.json</c>, which
    /// the upload names the version after, and in the report.
    /// <para>
    /// Tools > Build > WebGL Playtest > (mode) builds a release that playtests one event mode with new players
    /// (<see cref="GameTuning.PlaytestSettings"/>): the mode is set in the tuning asset for the build only, and the file
    /// is written back byte for byte after it. Its id goes into <c>build.json</c>, so the upload names the version after
    /// it (<c>Bonk Arena v0.19.0 (640) playtest crown</c>) and builds of different modes are never taken for each other.
    /// </para>
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

        [MenuItem("Tools/Build/WebGL Playtest/HOT POTATO")]
        public static void PlaytestHotPotato() => Run(false, EventMode.HotPotato);

        [MenuItem("Tools/Build/WebGL Playtest/CROWN")]
        public static void PlaytestCrown() => Run(false, EventMode.Crown);

        [MenuItem("Tools/Build/WebGL Playtest/GOLD RUSH")]
        public static void PlaytestGoldRush() => Run(false, EventMode.GoldRush);

        [MenuItem("Tools/Build/WebGL Playtest/ITEM FRENZY")]
        public static void PlaytestItemFrenzy() => Run(false, EventMode.ItemFrenzy);

        [MenuItem("Tools/Build/WebGL Playtest/OVERHEAT")]
        public static void PlaytestOverheat() => Run(false, EventMode.Overheat);

        [MenuItem("Tools/Build/WebGL Playtest/TEAM PUSH")]
        public static void PlaytestTeamPush() => Run(false, EventMode.TeamPush);

        [MenuItem("Tools/Build/WebGL Playtest/FOOTBALL")]
        public static void PlaytestFootball() => Run(false, EventMode.Football);

        // Not an overload of Build: -executeMethod finds the method by name alone.
        private static void Run(bool development, EventMode playtest = EventMode.None)
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
            var playtestId = playtest != EventMode.None ? EventModes.IdOf(playtest) : null;
            using var deferred = DeferredContent.Build();
            using var playtestBuild = new PlaytestBuild(playtest);
            // Super Console keeps its settings there, but a project without it may have no Resources folder yet.
            Directory.CreateDirectory(Path.GetDirectoryName(BuildInfoAsset));
            File.WriteAllText(BuildInfoAsset, $"{build.Number}\n{build.Commit}\n");
            AssetDatabase.ImportAsset(BuildInfoAsset, ImportAssetOptions.ForceSynchronousImport);
            try {
                deferred.Strip();
                playtestBuild.Apply();
                report = BuildPipeline.BuildPlayer(options);
            }
            finally {
                playtestBuild.Dispose();
                deferred.Dispose();
                AssetDatabase.DeleteAsset(BuildInfoAsset);
            }

            if (report.summary.result == BuildResult.Succeeded) {
                deferred.CopyTo(OutputPath);
            }

            var shipped = WriteReport(report, build, deferred, playtestId);
            if (report.summary.result != BuildResult.Succeeded) {
                throw new BuildFailedException($"WebGL build {report.summary.result}, see {ReportPath}");
            }

            if (shipped > 0) {
                throw new BuildFailedException($"{shipped} assets meant for the content bundles are still in the player, see {ReportPath}");
            }

            var playtestLine = playtestId != null ? $",\n  \"playtest\": \"{playtestId}\"" : "";
            File.WriteAllText(buildFile,
                $"{{\n  \"build_number\": \"{build.Number}\",\n  \"commit\": \"{build.Commit}\"{playtestLine}\n}}\n");
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

        /// <summary>Writes the report; how many of the deferred assets still shipped in the player.</summary>
        private static int WriteReport(BuildReport report, BuildNumber build, DeferredContent deferred, string playtest)
        {
            var deferredPaths = new HashSet<string>(deferred.Paths);
            var shipped = report.packedAssets
                .SelectMany(packed => packed.contents)
                .Where(content => deferredPaths.Contains(content.sourceAssetPath))
                .Select(content => content.sourceAssetPath)
                .Distinct()
                .ToList();
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
            text.AppendLine($"Content bundles: {string.Join(", ", deferred.BundleNames)}; " +
                $"{deferredPaths.Count} assets moved, {shipped.Count} still in the player" +
                (shipped.Count > 0 ? $" ({string.Join(", ", shipped)})" : ""));
            text.AppendLine($"Playtest: {playtest ?? "none"}");
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
            return shipped.Count;
        }

        private static string FormatSize(long bytes)
        {
            return bytes >= 1024 * 1024 ? $"{bytes / (1024f * 1024f):0.00} MB" : $"{bytes / 1024f:0.0} KB";
        }

        private static string FormatSize(ulong bytes) => FormatSize((long)bytes);

        /// <summary>
        /// Sets the playtested mode in the tuning asset while the player builds, then writes the file back byte for
        /// byte: saving the asset would also write the sections that live only in code, with their defaults frozen.
        /// </summary>
        private sealed class PlaytestBuild : IDisposable
        {
            private const string ModeProperty = "playtest.mode";

            private readonly EventMode mode;
            private string path;
            private byte[] original;

            public PlaytestBuild(EventMode mode)
            {
                this.mode = mode;
            }

            public void Apply()
            {
                var guids = AssetDatabase.FindAssets($"t:{nameof(GameTuning)}");
                if (guids.Length != 1) {
                    throw new BuildFailedException($"Expected one {nameof(GameTuning)}, found {guids.Length}.");
                }

                var tuningPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(tuningPath);
                // Only the build sets a mode: a tuning that already playtests one would ship it in a plain build too.
                if (tuning.Playtest.Mode != EventMode.None) {
                    throw new BuildFailedException($"{tuningPath} already playtests {tuning.Playtest.Mode}; set it to None.");
                }

                if (mode == EventMode.None) {
                    return;
                }

                this.path = tuningPath;
                this.original = File.ReadAllBytes(tuningPath);
                var serialized = new SerializedObject(tuning);
                var property = serialized.FindProperty(ModeProperty);
                if (property == null) {
                    throw new BuildFailedException($"{nameof(GameTuning)} has no {ModeProperty}.");
                }

                property.intValue = (int)mode;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(tuning);
                if (tuning.Playtest.Mode != mode) {
                    throw new BuildFailedException($"The tuning playtests {tuning.Playtest.Mode}, not {mode}.");
                }

                Debug.Log($"[{nameof(WebGLBuild)}] Playtest build of {EventModes.IdOf(mode)}");
            }

            public void Dispose()
            {
                if (original == null) {
                    return;
                }

                File.WriteAllBytes(path, original);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                original = null;
            }
        }
    }
}
