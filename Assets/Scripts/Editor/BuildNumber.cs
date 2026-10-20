using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Build;
using Debug = UnityEngine.Debug;

namespace ToyCarArena.Editor
{
    /// <summary>
    /// The build number: how many commits HEAD has (<c>git rev-list --count HEAD</c>), so it goes up with every commit
    /// and a local and a CI build of the same commit get the same one; with the commit's short hash, since two branches
    /// can reach the same count. The upload names the version after it (<c>Bonk Arena v0.1.0 (12)</c>). A CI
    /// build that cannot run git passes <c>-buildNumber</c> and <c>-buildCommit</c>, which win over git. A shallow clone
    /// has only part of the history, so its count is wrong and the build fails instead.
    /// </summary>
    public readonly struct BuildNumber
    {
        public readonly string Number;
        public readonly string Commit;

        private BuildNumber(string number, string commit)
        {
            this.Number = number;
            this.Commit = commit;
        }

        public override string ToString() => $"{Number} (commit {Commit})";

        /// <summary>
        /// The number and commit of the checkout, or the ones given on the command line.
        /// </summary>
        public static BuildNumber Current()
        {
            var number = Argument("-buildNumber");
            var commit = Argument("-buildCommit");
            if (number == null) {
                if (Git("rev-parse --is-shallow-repository") == "true") {
                    throw new BuildFailedException("A shallow clone cannot count its commits for the build number; " +
                        "fetch the whole history (git fetch --unshallow) or pass -buildNumber.");
                }

                number = Git("rev-list --count HEAD");
            }

            commit ??= Git("rev-parse --short HEAD");
            if (!Regex.IsMatch(number, @"^\d+$")) {
                throw new BuildFailedException($"No build number: git rev-list --count HEAD gave '{number}'. " +
                    "Build from a git checkout with git on the PATH, or pass -buildNumber.");
            }

            return new BuildNumber(number, string.IsNullOrEmpty(commit) ? "unknown" : commit);
        }

        /// <summary>
        /// Runs git in the project folder and returns its output, or an empty string when it fails.
        /// </summary>
        public static string Git(string arguments)
        {
            try {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                var output = process.StandardOutput.ReadToEnd().Trim();
                var error = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode != 0) {
                    Debug.LogWarning($"[{nameof(BuildNumber)}] git {arguments} failed: {error}");
                    return "";
                }

                return output;
            }
            catch (Exception exception) {
                Debug.LogWarning($"[{nameof(BuildNumber)}] git {arguments} failed: {exception.Message}");
                return "";
            }
        }

        private static string Argument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}
