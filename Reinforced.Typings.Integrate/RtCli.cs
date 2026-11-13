using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Reinforced.Typings.Cli;

namespace Reinforced.Typings.Integrate
{
    /// <summary>
    /// Task for gathering dynamic typings
    /// </summary>
    public class RtCli : ToolTask
    {
        private ToolChoice _choice;

        /// <summary>
        /// Consumer's $(TargetFramework)
        /// </summary>
        public string TargetFramework { get; set; }

        /// <summary>
        /// Consumer's $(TargetFrameworkIdentifier), e.g. .NETCoreApp
        /// </summary>
        public string TargetFrameworkIdentifier { get; set; }

        /// <summary>
        /// Consumer's $(TargetFrameworkVersion), e.g. v11.0
        /// </summary>
        public string TargetFrameworkVersion { get; set; }

        /// <summary>
        /// The SDK's own runtime version, $(BundledNETCoreAppTargetFrameworkVersion); used for netstandard consumers
        /// </summary>
        public string SdkRuntimeVersion { get; set; }

        /// <summary>
        /// Forced usage of target framework
        /// </summary>
        public string RtForceTargetFramework { get; set; }

        /// <summary>
        /// Package's "build" directory
        /// </summary>
        [Required]
        public string BuildDirectory { get; set; }

        /// <summary>
        /// Additional library references
        /// </summary>
        [Required]
        public ITaskItem[] References { get; set; }

        /// <summary>
        /// Source assembly
        /// </summary>
        [Required]
        public ITaskItem[] SourceAssembly { get; set; }

        /// <summary>
        /// Target .td/.d.ts file
        /// </summary>
        public string TargetFile { get; set; }

        /// <summary>
        /// Target directory for hierarchical generation
        /// </summary>
        public string TargetDirectory { get; set; }

        /// <summary>
        /// Generate types to multiple files
        /// </summary>
        public bool Hierarchical { get; set; }

        /// <summary>
        /// Additional source assemblies to import
        /// </summary>
        public ITaskItem[] AdditionalSourceAssemblies { get; set; }

        /// <summary>
        /// ProjectDir variable
        /// </summary>
        public string ProjectRoot { get; set; }

        /// <summary>
        /// Path to documentation XML
        /// </summary>
        public string DocumentationFilePath { get; set; }


        /// <summary>
        /// Full-qualified name of fluent configuration method
        /// </summary>
        public string ConfigurationMethod { get; set; }

        /// <summary>
        /// Semicolon-separated list of warnings to be suppressed
        /// </summary>
        public string SuppressedWarnings { get; set; }

        /// <summary>Projects may set this to override a task's ToolName. Tasks may override this to prevent that.</summary>
        public override string ToolExe
        {
            get; set;
        }

        private ToolChoice Choice
        {
            get
            {
                if (_choice == null)
                {
                    _choice = ToolSelection.Select(TargetFramework, TargetFrameworkIdentifier, TargetFrameworkVersion,
                        SdkRuntimeVersion, RtForceTargetFramework);
                }
                return _choice;
            }
        }

        private static bool IsWindows
        {
            get { return RuntimeInformation.IsOSPlatform(OSPlatform.Windows); }
        }

        protected override bool ValidateParameters()
        {
            var choice = Choice;
            if (choice.Error != null)
            {
                Log.LogError(null, "RTI0001", null, null, 0, 0, 0, 0, choice.Error);
                return false;
            }

            if (choice.IsNetFramework && !IsWindows)
            {
                Log.LogError(null, "RTI0002", null, null, 0, 0, 0, 0,
                    "Reinforced.Typings.vNext runs tools/net481/rtcli.exe for .NET Framework projects, which needs Windows. " +
                    "To generate on this OS, set RtForceTargetFramework to net10.0 or net11.0.");
                return false;
            }

            var toolDir = GetProperRtcliPath();
            var toolFile = Path.Combine(toolDir, choice.IsNetFramework ? "rtcli.exe" : "rtcli.dll");
            if (!File.Exists(toolFile))
            {
                Log.LogError(null, "RTI0003", null, null, 0, 0, 0, 0,
                    "Reinforced.Typings.vNext: rtcli was not found at '{0}'. The package may be damaged; restore it again.", toolFile);
                return false;
            }

            return base.ValidateParameters();
        }

        protected override string GenerateFullPathToTool()
        {
            if (Choice.IsNetFramework) return Path.Combine(GetProperRtcliPath(), ToolName);

            // Run the tool with the same dotnet host as the build when the SDK tells us which one that is.
            var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrEmpty(hostPath) && File.Exists(hostPath)) return hostPath;
            return ToolName;
        }

        private string GetProperRtcliPath()
        {
            var bd = new DirectoryInfo(BuildDirectory);
            var toolsPath = Path.Combine(bd.Parent.FullName, "tools");
            return Path.Combine(toolsPath, Choice.Folder);
        }

        protected override string ToolName
        {
            get
            {
                if (Choice.IsNetFramework) return "rtcli.exe";
                return IsWindows ? "dotnet.exe" : "dotnet";
            }
        }

        protected override string GenerateCommandLineCommands()
        {
            ExporterConsoleParameters consoleParams = new ExporterConsoleParameters()
            {
                Hierarchy = Hierarchical,
                TargetDirectory = FixTargetPath(TargetDirectory),
                TargetFile = FixTargetPath(TargetFile),
                ReferencesTmpFilePath = string.Empty,
                SourceAssemblies = ExtractSourceAssemblies(),
                SuppressedWarnings = SuppressedWarnings,
                DocumentationFilePath = DocumentationFilePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                    ? DocumentationFilePath
                    : String.Empty,
                ConfigurationMethod = ConfigurationMethod
            };

            var tmpFile = Path.GetTempFileName();
            using (var fs = File.OpenWrite(tmpFile))
            {
                using (var tw = new StreamWriter(fs))
                {
                    consoleParams.ToFile(tw);
                    PutReferencesToTempFile(tw);
                    tw.Flush();
                }
            }

            if (Choice.IsNetFramework)
            {
                return string.Format("profile \"{0}\"", tmpFile);
            }

            var pth = Path.Combine(GetProperRtcliPath(), "rtcli.dll");
            var rollForward = Choice.RollForward ? "--roll-forward Major " : string.Empty;
            return string.Format("{2}\"{1}\" profile \"{0}\"", tmpFile, pth, rollForward);
        }

        private string FixTargetPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (!Path.IsPathRooted(path))
                {
                    return Path.Combine(ProjectRoot, path);
                }
            }
            return path;
        }


        private void PutReferencesToTempFile(TextWriter tw)
        {
            if (References == null) return;
            foreach (var rf in References.Select(c => c.ItemSpec))
            {
                tw.WriteLine(rf);
            }
            tw.Flush();

        }

        private string[] ExtractSourceAssemblies()
        {
            List<string> srcAssemblies = new List<string>();
            if (AdditionalSourceAssemblies != null)
            {
                srcAssemblies.AddRange(AdditionalSourceAssemblies.Select(c => c.ItemSpec));
            }
            if (SourceAssembly != null)
            {
                srcAssemblies.AddRange(SourceAssembly.Select(c => Path.Combine(ProjectRoot, c.ItemSpec)));
            }
            return srcAssemblies.ToArray();
        }

    }
}
