using System;
using System.Globalization;

namespace Reinforced.Typings.Integrate
{
    /// <summary>
    /// Which rtcli build runs for a consumer, and how.
    /// </summary>
    internal sealed class ToolChoice
    {
        private ToolChoice(string folder, bool isNetFramework, bool rollForward, string error)
        {
            Folder = folder;
            IsNetFramework = isNetFramework;
            RollForward = rollForward;
            Error = error;
        }

        /// <summary>Folder under the package's tools/ directory, e.g. "net10.0".</summary>
        public string Folder { get; }

        /// <summary>True for tools/net481/rtcli.exe, false for "dotnet rtcli.dll".</summary>
        public bool IsNetFramework { get; }

        /// <summary>True when the consumer is newer than the newest tool, so the host must roll forward a major version.</summary>
        public bool RollForward { get; }

        /// <summary>Set when the consumer framework is not supported; the build should fail with this message.</summary>
        public string Error { get; }

        internal static ToolChoice Tool(string folder, bool isNetFramework, bool rollForward = false)
        {
            return new ToolChoice(folder, isNetFramework, rollForward, null);
        }

        internal static ToolChoice Fail(string error)
        {
            return new ToolChoice(null, false, false, error);
        }
    }

    /// <summary>
    /// The tool selection rule. Parses the consumer's framework once:
    /// net481 uses tools/net481/rtcli.exe; net10.0 and net11.0 use their own tool; net12.0 and later use
    /// the net11.0 tool with major-version roll-forward; netstandard2.x uses the tool matching the SDK's
    /// runtime; everything else fails with a message naming the supported frameworks.
    /// RtForceTargetFramework overrides the rule.
    /// </summary>
    internal static class ToolSelection
    {
        internal const string NetFrameworkTool = "net481";
        internal const string Net10Tool = "net10.0";
        internal const string Net11Tool = "net11.0";
        internal const int NewestToolMajor = 11;
        internal const string SupportedList = "net481, net10.0, net11.0";

        /// <param name="targetFramework">$(TargetFramework), e.g. "net11.0" or "net10.0-windows".</param>
        /// <param name="targetFrameworkIdentifier">$(TargetFrameworkIdentifier), e.g. ".NETCoreApp"; may be empty.</param>
        /// <param name="targetFrameworkVersion">$(TargetFrameworkVersion), e.g. "v11.0"; may be empty.</param>
        /// <param name="sdkRuntimeVersion">$(BundledNETCoreAppTargetFrameworkVersion), e.g. "11.0"; may be empty.</param>
        /// <param name="forced">$(RtForceTargetFramework); may be empty.</param>
        internal static ToolChoice Select(string targetFramework, string targetFrameworkIdentifier,
            string targetFrameworkVersion, string sdkRuntimeVersion, string forced)
        {
            if (!string.IsNullOrWhiteSpace(forced)) return SelectForced(forced.Trim());

            string identifier;
            Version version;
            if (!TryGetFramework(targetFramework, targetFrameworkIdentifier, targetFrameworkVersion, out identifier, out version))
            {
                return Unsupported(targetFramework);
            }

            switch (identifier)
            {
                case ".NETFramework":
                    if (version.Major == 4 && version.Minor == 8 && version.Build == 1) return ToolChoice.Tool(NetFrameworkTool, true);
                    return Unsupported(targetFramework);

                case ".NETCoreApp":
                    return SelectForNetMajor(version.Major, targetFramework);

                case ".NETStandard":
                    if (version.Major != 2) return Unsupported(targetFramework);
                    Version sdk;
                    if (TryParseVersion(sdkRuntimeVersion, out sdk) && sdk.Major >= 10) return SelectForNetMajor(sdk.Major, targetFramework);
                    // No SDK runtime information (for example a non-SDK project): the LTS tool.
                    return ToolChoice.Tool(Net10Tool, false);

                default:
                    return Unsupported(targetFramework);
            }
        }

        private static ToolChoice SelectForNetMajor(int major, string targetFramework)
        {
            if (major == 10) return ToolChoice.Tool(Net10Tool, false);
            if (major == NewestToolMajor) return ToolChoice.Tool(Net11Tool, false);
            if (major > NewestToolMajor) return ToolChoice.Tool(Net11Tool, false, rollForward: true);
            return Unsupported(targetFramework);
        }

        private static ToolChoice SelectForced(string forced)
        {
            switch (forced.ToLowerInvariant())
            {
                case NetFrameworkTool: return ToolChoice.Tool(NetFrameworkTool, true);
                case Net10Tool: return ToolChoice.Tool(Net10Tool, false);
                case Net11Tool: return ToolChoice.Tool(Net11Tool, false);
                default:
                    return ToolChoice.Fail(string.Format(CultureInfo.InvariantCulture,
                        "RtForceTargetFramework is '{0}', but Reinforced.Typings.vNext ships rtcli only for {1}. Set it to one of those or remove it.",
                        forced, SupportedList));
            }
        }

        private static ToolChoice Unsupported(string targetFramework)
        {
            return ToolChoice.Fail(string.Format(CultureInfo.InvariantCulture,
                "Reinforced.Typings.vNext supports projects targeting .NET 11, .NET 10 and .NET Framework 4.8.1 " +
                "(TargetFramework net11.0, net10.0, net481; net12.0 and later run the net11.0 tool with roll-forward; " +
                "netstandard2.x uses the tool matching the SDK). '{0}' is not supported. " +
                "Retarget the project, or set RtForceTargetFramework to one of: {1}.",
                string.IsNullOrEmpty(targetFramework) ? "(no TargetFramework)" : targetFramework, SupportedList));
        }

        internal static bool TryGetFramework(string targetFramework, string identifier, string version,
            out string resultIdentifier, out Version resultVersion)
        {
            resultIdentifier = null;
            resultVersion = null;
            Version v;
            if (!string.IsNullOrEmpty(identifier) && TryParseVersion(version, out v))
            {
                resultIdentifier = identifier;
                resultVersion = v;
                return true;
            }
            return TryParseTargetFramework(targetFramework, out resultIdentifier, out resultVersion);
        }

        /// <summary>Parses short framework names: net481, net10.0, net11.0-windows, netstandard2.0, netcoreapp3.1.</summary>
        internal static bool TryParseTargetFramework(string targetFramework, out string identifier, out Version version)
        {
            identifier = null;
            version = null;
            if (string.IsNullOrWhiteSpace(targetFramework)) return false;
            var tf = targetFramework.Trim().ToLowerInvariant();
            var dash = tf.IndexOf('-');
            if (dash >= 0) tf = tf.Substring(0, dash);

            string digits;
            if (tf.StartsWith("netstandard", StringComparison.Ordinal))
            {
                identifier = ".NETStandard";
                digits = tf.Substring("netstandard".Length);
            }
            else if (tf.StartsWith("netcoreapp", StringComparison.Ordinal))
            {
                identifier = ".NETCoreApp";
                digits = tf.Substring("netcoreapp".Length);
            }
            else if (tf.StartsWith("net", StringComparison.Ordinal))
            {
                digits = tf.Substring(3);
                if (digits.IndexOf('.') >= 0)
                {
                    // net5.0 and later
                    identifier = ".NETCoreApp";
                }
                else
                {
                    // net45, net472, net481: one digit per component
                    identifier = ".NETFramework";
                    if (digits.Length < 2 || digits.Length > 3) return false;
                    digits = string.Join(".", digits.ToCharArray());
                }
            }
            else
            {
                return false;
            }

            return TryParseVersion(digits, out version);
        }

        internal static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var t = text.Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase)) t = t.Substring(1);
            if (t.IndexOf('.') < 0) t += ".0";
            Version parsed;
            if (!Version.TryParse(t, out parsed)) return false;
            // Normalise "4.8" to 4.8.0 so that Build is comparable.
            version = new Version(parsed.Major, parsed.Minor, parsed.Build < 0 ? 0 : parsed.Build);
            return true;
        }
    }
}
