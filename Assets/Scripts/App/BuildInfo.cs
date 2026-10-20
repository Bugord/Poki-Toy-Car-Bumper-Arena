using UnityEngine;

namespace ToyCarArena.App
{
    /// <summary>
    /// Which build this is: the build number (the commit count of the commit it was built from, as in the name of its
    /// upload to Poki, <c>Bonk Arena v0.1.0 (12)</c>) and that commit's short hash.
    /// <c>ToyCarArena.Editor.WebGLBuild</c> writes them to <c>Resources/data_build_info.txt</c> for the build only
    /// (ignored by git, deleted after the build), so in the Editor there are none. Logged at startup, so a player's
    /// console log says which build it came from.
    /// </summary>
    public static class BuildInfo
    {
        /// <summary>
        /// The text asset in <c>Resources</c>: the number on the first line, the commit on the second.
        /// </summary>
        public const string ResourceName = "data_build_info";

        /// <summary>
        /// The build number, or empty in the Editor.
        /// </summary>
        public static string Number { get; private set; } = "";

        /// <summary>
        /// The short hash of the commit built, or empty in the Editor.
        /// </summary>
        public static string Commit { get; private set; } = "";

        /// <summary>
        /// The upload's name with the commit: <c>Bonk Arena v0.1.0 (12) dev, commit abc1234</c>, or
        /// <c>Bonk Arena v0.1.0 (editor)</c>.
        /// </summary>
        public static string Label
        {
            get
            {
                var number = string.IsNullOrEmpty(Number) ? "editor" : Number;
                var development = Debug.isDebugBuild && !Application.isEditor ? " dev" : "";
                var commit = string.IsNullOrEmpty(Commit) ? "" : $", commit {Commit}";
                return $"{Application.productName} v{Application.version} ({number}){development}{commit}";
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            Number = "";
            Commit = "";
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset != null) {
                var lines = asset.text.Split('\n');
                Number = lines[0].Trim();
                Commit = lines.Length > 1 ? lines[1].Trim() : "";
                Resources.UnloadAsset(asset);
            }

            Debug.Log($"[{nameof(BuildInfo)}] {Label}");
        }
    }
}
