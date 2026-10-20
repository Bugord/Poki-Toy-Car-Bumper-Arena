using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ToyCarArena.Editor
{
    /// <summary>
    /// Uploads <c>Builds/WebGL</c> to Poki for Developers from the Editor, as <c>Tools/upload-poki.sh</c> does: the same
    /// checks, the version named after the build's product name, version and build number (<c>Toy Car Bumper Arena v0.1.0 (36)</c>,
    /// <c>Toy Car Bumper Arena v0.1.0 (36) dev</c> for a development build, which carries the cheat console: test it, never release it;
    /// the number is from the build's <c>build.json</c>, see <see cref="BuildNumber"/>) and the commit built in the
    /// notes. Poki's CLI runs in a console window of its own, so the Editor is not blocked. It signs in with the upload
    /// token in <c>UserSettings/poki-upload-token.txt</c> (ignored by git), or else opens the browser to sign in.
    /// </summary>
    public static class PokiUpload
    {
        private const string BuildPath = "Builds/WebGL";
        // In the version's name, so a development build is never taken for the release.
        private const string DevelopmentSuffix = " dev";
        // Poki's upload token, kept out of git; the CLI takes it from POKI_UPLOAD_TOKEN over its browser login.
        private const string TokenFile = "UserSettings/poki-upload-token.txt";
        private const string TokenVariable = "POKI_UPLOAD_TOKEN";

        [MenuItem("Tools/Build/Upload to Poki")]
        public static void Upload()
        {
            var indexJson = Path.Combine(BuildPath, "index.json");
            if (!File.Exists(Path.Combine(BuildPath, "index.html")) || !File.Exists(indexJson)) {
                Fail($"No build in {BuildPath}; build it with Tools > Build > WebGL (Poki).");
                return;
            }

            var index = File.ReadAllText(indexJson);
            var development = !index.Contains("\"development_player\": \"false\"");

            if (Directory.GetFiles(Path.Combine(BuildPath, "Build")).Any(file => file.EndsWith(".br") || file.EndsWith(".gz"))) {
                Fail($"{BuildPath} is compressed; Poki wants compression Disabled.");
                return;
            }

            var version = Regex.Match(index, "\"product_version\": \"([^\"]*)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(version)) {
                Fail($"No product_version in {indexJson}.");
                return;
            }

            var product = Regex.Match(index, "\"product_name\": \"([^\"]*)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(product)) {
                Fail($"No product_name in {indexJson}.");
                return;
            }

            var buildJson = Path.Combine(BuildPath, WebGLBuild.BuildFile);
            var buildInfo = File.Exists(buildJson) ? File.ReadAllText(buildJson) : "";
            var number = Regex.Match(buildInfo, "\"build_number\": \"([^\"]*)\"").Groups[1].Value;
            var commit = Regex.Match(buildInfo, "\"commit\": \"([^\"]*)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(number) || string.IsNullOrEmpty(commit)) {
                Fail($"No build_number or commit in {buildJson}; build again with Tools > Build > WebGL (Poki).");
                return;
            }

            var name = $"{product} v{version} ({number}){(development ? DevelopmentSuffix : "")}";
            // The notes name the commit built, which need not be the one checked out now.
            var message = Git($"log -1 --pretty=%s {commit}");
            var notes = string.IsNullOrEmpty(message) ? commit : $"{commit}: {message}";
            var otherCommit = !Git("rev-parse HEAD").StartsWith(commit, StringComparison.Ordinal)
                ? $"\n\nBuilt from {commit}, not from the commit checked out ({Git("rev-parse --short HEAD")})."
                : "";
            var uncommitted = !string.IsNullOrEmpty(Git("status --porcelain -- Assets Packages ProjectSettings"))
                ? "\n\nAssets, Packages or ProjectSettings have uncommitted changes, which the build may hold."
                : "";
            var console = development ? "\n\nA development build, with Super Console: test it on Poki, never release it." : "";
            if (!EditorUtility.DisplayDialog("Upload to Poki", $"Upload {BuildPath} as {name}?\n\nNotes: {notes}{otherCommit}{uncommitted}{console}",
                    "Upload", "Cancel")) {
                return;
            }

            RunPokiCli($"upload --name \"{ForCmd(name)}\" --notes \"{ForCmd(notes)}\"");
            Debug.Log($"[{nameof(PokiUpload)}] Uploading {BuildPath} as {name} ({commit}) in Poki's CLI window.");
        }

        [MenuItem("Tools/Build/WebGL (Poki) and Upload")]
        public static void BuildAndUpload()
        {
            WebGLBuild.Build();
            Upload();
        }

        [MenuItem("Tools/Build/WebGL Development (Poki) and Upload")]
        public static void BuildDevelopmentAndUpload()
        {
            WebGLBuild.BuildDevelopment();
            Upload();
        }

        /// <summary>
        /// Runs <c>npx @poki/cli</c> in a console window that stays open until a key is pressed, to show how it went.
        /// </summary>
        private static void RunPokiCli(string arguments)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) {
                Fail("The Editor upload runs on Windows only; run Tools/upload-poki.sh.");
                return;
            }

            // The console window inherits the Editor's environment, so the token reaches the CLI without a command line.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TokenVariable)) && File.Exists(TokenFile)) {
                Environment.SetEnvironmentVariable(TokenVariable, File.ReadAllText(TokenFile).Trim());
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // /s strips the outer quotes and keeps the inner ones for npx. poki-auth.js refreshes an expired login
                // first: the CLI only does it after a 401, which an upload never gets (the connection is reset).
                Arguments = $"/s /c \"node Tools\\poki-auth.js && npx --yes @poki/cli {arguments} & pause\"",
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = true
            });
        }

        private static string Git(string arguments) => BuildNumber.Git(arguments);

        /// <summary>
        /// Keeps text inside cmd's double quotes: no quote to end them and no <c>%</c> to expand a variable.
        /// </summary>
        private static string ForCmd(string text) => text.Replace('"', '\'').Replace("%", " percent");

        private static void Fail(string message)
        {
            Debug.LogError($"[{nameof(PokiUpload)}] {message}");
            EditorUtility.DisplayDialog("Upload to Poki", message, "OK");
        }
    }
}
