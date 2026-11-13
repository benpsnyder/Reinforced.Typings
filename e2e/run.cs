// End-to-end test for the Reinforced.Typings.vNext package.
//
// Builds e2e/Consumer once per target framework against a package taken from a local feed,
// which exercises the MSBuild task, the targets file, tool selection and rtcli together,
// then compares the generated TypeScript byte-for-byte with e2e/golden/consumer.ts.
//
// Usage:
//   dotnet run e2e/run.cs -- --feed <dir with .nupkg> --version <version>
//       [--frameworks net10.0,net11.0,net481] [--msbuild <path to MSBuild.exe>]
//       [--work <scratch dir>] [--update-golden]
//
// --msbuild additionally builds the net481 consumer with Visual Studio's MSBuild.exe
// (MSBuildRuntimeType=Full), the path that loads build/net472 of the package.
// Exit code is the number of failed builds (0 = all passed).

using System.Diagnostics;
using System.Text;

var options = Options.Parse(args);
var e2eDir = Path.GetDirectoryName(Path.GetFullPath(ThisFile()))!;
var consumerDir = Path.Combine(e2eDir, "Consumer");
var goldenFile = Path.Combine(e2eDir, "golden", "consumer.ts");
var work = Path.GetFullPath(options.Work ?? Path.Combine(Path.GetTempPath(), "rt-e2e"));

if (Directory.Exists(work)) Directory.Delete(work, recursive: true);
Directory.CreateDirectory(work);
var packages = Path.Combine(work, "packages");

var runs = new List<(string Label, string Framework, string? MsBuild, string[] Extra)>();
foreach (var fw in options.Frameworks) runs.Add(($"dotnet-{fw}", fw, null, []));
if (options.MsBuild is not null) runs.Add(("msbuild-net481", "net481", options.MsBuild, []));
// Same consumer with the TypeScript-bypass task enabled (defect 8 regression check).
if (options.Bypass) runs.Add(($"bypass-{options.Frameworks[0]}", options.Frameworks[0], null, ["-p:RtBypassTypeScriptCompilation=true"]));

var failures = 0;
var results = new List<string>();
foreach (var run in runs)
{
    var dir = Path.Combine(work, run.Label);
    CopyConsumer(consumerDir, dir);
    WriteNuGetConfig(dir, Path.GetFullPath(options.Feed));

    var common = new List<string>
    {
        "-p:RtE2EFramework=" + run.Framework,
        "-p:RtPackageVersion=" + options.Version,
        "-p:RestorePackagesPath=" + packages,
        "-p:NuGetAudit=false",
        "-p:Configuration=Release",
        "-nodeReuse:false",
        "-warnaserror",
        "-nologo",
        "-v:minimal",
    };
    ProcessStartInfo psi;
    if (run.MsBuild is null)
    {
        psi = new ProcessStartInfo("dotnet") { ArgumentList = { "build", Path.Combine(dir, "Consumer.csproj"), "-tl:off" } };
    }
    else
    {
        psi = new ProcessStartInfo(run.MsBuild) { ArgumentList = { Path.Combine(dir, "Consumer.csproj"), "-restore" } };
    }
    foreach (var a in common.Concat(run.Extra)) psi.ArgumentList.Add(a);
    psi.WorkingDirectory = dir;
    psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

    var log = Path.Combine(work, run.Label + ".log");
    var (exit, output) = Run(psi);
    File.WriteAllText(log, output);

    var generated = Path.Combine(dir, "generated", "consumer.ts");
    string verdict;
    if (exit != 0)
    {
        verdict = $"FAIL build exited {exit}";
    }
    else if (!File.Exists(generated))
    {
        verdict = "FAIL build succeeded but generated/consumer.ts was not written";
    }
    else if (options.UpdateGolden)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(goldenFile)!);
        File.Copy(generated, goldenFile, overwrite: true);
        verdict = "PASS (golden updated)";
    }
    else if (!File.Exists(goldenFile))
    {
        verdict = "FAIL no golden file at " + goldenFile;
    }
    else
    {
        verdict = Compare(File.ReadAllBytes(goldenFile), File.ReadAllBytes(generated));
    }

    if (!verdict.StartsWith("PASS"))
    {
        failures++;
        Console.WriteLine($"---- {run.Label}: {verdict}; last lines of {log}:");
        foreach (var line in output.Split('\n').TakeLast(25)) Console.WriteLine("  " + line.TrimEnd());
    }
    results.Add($"{run.Label,-18} {verdict}");
}

Console.WriteLine();
Console.WriteLine($"Reinforced.Typings.vNext {options.Version} end-to-end results:");
foreach (var r in results) Console.WriteLine("  " + r);
Console.WriteLine($"{runs.Count - failures} passed, {failures} failed. Logs: {work}");
return failures;

static string Compare(byte[] expected, byte[] actual)
{
    if (expected.AsSpan().SequenceEqual(actual)) return "PASS identical to golden";
    var e = Encoding.UTF8.GetString(expected).Split('\n');
    var a = Encoding.UTF8.GetString(actual).Split('\n');
    for (var i = 0; i < Math.Max(e.Length, a.Length); i++)
    {
        var el = i < e.Length ? e[i] : "<eof>";
        var al = i < a.Length ? a[i] : "<eof>";
        if (el != al) return $"FAIL differs from golden at line {i + 1}: expected [{el.TrimEnd('\r')}] got [{al.TrimEnd('\r')}]";
    }
    return "FAIL differs from golden (line endings or encoding)";
}

static void CopyConsumer(string from, string to)
{
    Directory.CreateDirectory(to);
    foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(from, file);
        var first = rel.Split(Path.DirectorySeparatorChar)[0];
        if (first is "bin" or "obj" or "generated") continue;
        var target = Path.Combine(to, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, overwrite: true);
    }
}

static void WriteNuGetConfig(string dir, string feed)
{
    File.WriteAllText(Path.Combine(dir, "NuGet.config"), $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="local" value="{feed}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
          <packageSourceMapping>
            <packageSource key="local"><package pattern="Reinforced.Typings.vNext" /></packageSource>
            <packageSource key="nuget.org"><package pattern="*" /></packageSource>
          </packageSourceMapping>
        </configuration>
        """);
}

static (int Exit, string Output) Run(ProcessStartInfo psi)
{
    psi.RedirectStandardOutput = true;
    psi.RedirectStandardError = true;
    var sb = new StringBuilder();
    using var p = new Process { StartInfo = psi };
    p.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (sb) sb.AppendLine(e.Data); };
    p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (sb) sb.AppendLine(e.Data); };
    p.Start();
    p.BeginOutputReadLine();
    p.BeginErrorReadLine();
    p.WaitForExit();
    return (p.ExitCode, sb.ToString());
}

static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

sealed record Options(string Feed, string Version, string[] Frameworks, string? MsBuild, string? Work, bool UpdateGolden, bool Bypass)
{
    public static Options Parse(string[] args)
    {
        string? feed = null, version = null, msbuild = null, work = null;
        var frameworks = new[] { "net10.0", "net11.0", "net481" };
        var update = false;
        var bypass = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--feed": feed = args[++i]; break;
                case "--version": version = args[++i]; break;
                case "--frameworks": frameworks = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries); break;
                case "--msbuild": msbuild = args[++i]; break;
                case "--work": work = args[++i]; break;
                case "--update-golden": update = true; break;
                case "--bypass-run": bypass = true; break;
                default: throw new ArgumentException("Unknown argument " + args[i]);
            }
        }
        if (feed is null || version is null) throw new ArgumentException("--feed and --version are required");
        return new Options(feed, version, frameworks, msbuild, work, update, bypass);
    }
}
