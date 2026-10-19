using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.UnityLinker;

namespace ToyCarArena.Editor
{
    /// <summary>
    /// Keeps Super Console's Pro and Input System assemblies in a development build. Nothing references them: each
    /// installs itself from a <c>[RuntimeInitializeOnLoadMethod]</c>, but without <c>[AlwaysLinkAssembly]</c> the linker
    /// at managed stripping High drops an unreferenced assembly whole, so the player had no PERF tab, no watch editing
    /// and no toggle key. A development build keeps both whole (Pro also finds some of its parts by reflection); a
    /// release build, which leaves the console out, gets nothing and the linker strips them as before.
    /// </summary>
    internal sealed class SuperConsoleLinker : IUnityLinkerProcessor
    {
        private const string OutputPath = "Library/ToyCarArena/SuperConsole.Preserve.xml";

        private static readonly string[] Assemblies = { "SuperConsole.Pro.Runtime", "SuperConsole.InputSystem" };

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            if ((report.summary.options & BuildOptions.Development) == 0) {
                return null;
            }

            var built = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)
                .Select(a => a.name), StringComparer.OrdinalIgnoreCase);
            var kept = Assemblies.Where(built.Contains).ToList();
            if (kept.Count == 0) {
                return null;
            }

            var xml = new StringBuilder("<linker>\n");
            foreach (var assembly in kept) {
                xml.Append($"  <assembly fullname=\"{assembly}\" preserve=\"all\"/>\n");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, xml.Append("</linker>\n").ToString());
            return Path.GetFullPath(OutputPath);
        }
    }
}
