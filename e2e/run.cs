// End-to-end test for the Reinforced.Typings.vNext package.
//
// Builds e2e/Consumer once per target framework against a package taken from a local feed,
// which exercises the MSBuild task, the targets file, tool selection and rtcli together,
// then compares the generated TypeScript byte-for-byte with e2e/golden/consumer.ts.
//
// Usage:
//   dotnet run e2e/run.cs -- --feed <dir with .nupkg> --version <version>
//       [--frameworks net10.0,net11.0,net481] [--msbuild <path to MSBuild.exe>]
//       [--work <scratch dir>] [--update-golden] [--bypass-run] [--modern-run] [--check-run]
//
// --msbuild additionally builds the net481 consumer with Visual Studio's MSBuild.exe
// (MSBuildRuntimeType=Full), the path that loads build/net472 of the package.
// --modern-run adds a build with -p:RtE2EVariant=modern (System.Text.Json attributes, the 3.1.0 output
// options and the Effect Schema emitter), compared with golden/consumer.modern.ts and
// golden/consumer.modern.schema.ts. --check-run rebuilds the first consumer to verify that unchanged
// output keeps its timestamp, that check mode (RtCheckMode=true) passes on current output and fails
// with error RT0019 (RTE0019) on drift without touching the file, and that RtSkipUnchanged skips an
// unchanged generation, honours RtForce and regenerates an edited output.
// Exit code is the number of failed builds (0 = all passed).

using System.Diagnostics;
using System.Text;

var options = Options.Parse(args);
var e2eDir = Path.GetDirectoryName(Path.GetFullPath(ThisFile()))!;
var consumerDir = Path.Combine(e2eDir, "Consumer");
var goldenDir = Path.Combine(e2eDir, "golden");
var work = Path.GetFullPath(options.Work ?? Path.Combine(Path.GetTempPath(), "rt-e2e"));

if (Directory.Exists(work)) Directory.Delete(work, recursive: true);
Directory.CreateDirectory(work);
var packages = Path.Combine(work, "packages");

var defaultGolden = new[] { ("consumer.ts", "consumer.ts") };
var runs = new List<(string Label, string Framework, string? MsBuild, string[] Extra, (string Generated, string Golden)[] Files)>();
foreach (var fw in options.Frameworks) runs.Add(($"dotnet-{fw}", fw, null, [], defaultGolden));
if (options.MsBuild is not null) runs.Add(("msbuild-net481", "net481", options.MsBuild, [], defaultGolden));
// Same consumer with the TypeScript-bypass task enabled (defect 8 regression check).
if (options.Bypass) runs.Add(($"bypass-{options.Frameworks[0]}", options.Frameworks[0], null, ["-p:RtBypassTypeScriptCompilation=true"], defaultGolden));
// Upstream #313/#231: TypeScriptTarget set without the TypeScript MSBuild targets must not fail (MSB4057);
// with them (a stub CompileTypeScript), Reinforced.Typings must still run CompileTypeScript.
foreach (var tsFw in options.Frameworks.Where(f => !f.StartsWith("net4")).Take(1))
{
    runs.Add(($"tstarget-{tsFw}", tsFw, null, ["-p:TypeScriptTarget=ES2020"], defaultGolden));
    runs.Add(($"tsstub-{tsFw}", tsFw, null, ["-p:TypeScriptTarget=ES2020", "-p:RtE2ETypeScriptStub=true"],
        [("consumer.ts", "consumer.ts"), ("ts-compiled.txt", "ts-stub-marker.txt")]));
}
// System.Text.Json needs .NET, so the modern variant uses the first non-.NET Framework target.
var modernFw = options.Frameworks.FirstOrDefault(f => !f.StartsWith("net4"));
if (options.Modern && modernFw is not null)
{
    runs.Add(($"modern-{modernFw}", modernFw, null,
        ["-p:RtE2EVariant=modern", "-p:RtSuppress=RTW0016%3BRTW0017"],
        [("consumer.ts", "consumer.modern.ts"), ("consumer.schema.ts", "consumer.modern.schema.ts")]));
}

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
        // Visual Studio's MSBuild may predate the newest SDK; any 10.0.x SDK can build the
        // net481 consumer, and the point of this run is MSBuildRuntimeType=Full.
        File.WriteAllText(Path.Combine(dir, "global.json"), """{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }""");
        psi = new ProcessStartInfo(run.MsBuild) { ArgumentList = { Path.Combine(dir, "Consumer.csproj"), "-restore" } };
    }
    foreach (var a in common.Concat(run.Extra)) psi.ArgumentList.Add(a);
    psi.WorkingDirectory = dir;
    psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

    var log = Path.Combine(work, run.Label + ".log");
    var (exit, output) = Run(psi);
    File.WriteAllText(log, output);

    var verdict = exit != 0 ? $"FAIL build exited {exit}" : "PASS identical to golden";
    foreach (var (genName, goldName) in run.Files)
    {
        if (!verdict.StartsWith("PASS")) break;
        var generated = Path.Combine(dir, "generated", genName);
        var golden = Path.Combine(goldenDir, goldName);
        if (!File.Exists(generated))
        {
            verdict = $"FAIL build succeeded but generated/{genName} was not written";
        }
        else if (options.UpdateGolden)
        {
            Directory.CreateDirectory(goldenDir);
            File.Copy(generated, golden, overwrite: true);
            verdict = "PASS (golden updated)";
        }
        else if (!File.Exists(golden))
        {
            verdict = "FAIL no golden file at " + golden;
        }
        else
        {
            var v = Compare(File.ReadAllBytes(golden), File.ReadAllBytes(generated));
            verdict = v.StartsWith("PASS") ? (run.Files.Length > 1 ? $"PASS identical to {run.Files.Length} golden files" : v) : $"{v} ({goldName})";
        }
    }

    if (verdict.StartsWith("PASS") && options.Check && run.Label == $"dotnet-{options.Frameworks[0]}" && run.MsBuild is null)
    {
        var check = CheckRun(psi, dir, work);
        results.Add($"{"check-" + run.Framework,-18} {check}");
        if (!check.StartsWith("PASS")) failures++;
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
Console.WriteLine($"{results.Count - failures} passed, {failures} failed. Logs: {work}");
return failures;

// Rebuilds a consumer that just passed: unchanged output keeps its timestamp, check mode passes on
// current output, and check mode fails with RTE0019 on drift without modifying the file.
static string CheckRun(ProcessStartInfo template, string dir, string work)
{
    var target = Path.Combine(dir, "generated", "consumer.ts");
    var old = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    File.SetLastWriteTimeUtc(target, old);

    ProcessStartInfo Rebuild(params string[] extra)
    {
        var psi = new ProcessStartInfo(template.FileName) { WorkingDirectory = template.WorkingDirectory };
        foreach (var a in template.ArgumentList) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("--no-incremental");
        foreach (var a in extra) psi.ArgumentList.Add(a);
        foreach (var kv in template.Environment) psi.Environment[kv.Key] = kv.Value;
        return psi;
    }

    var (exit, output) = Run(Rebuild());
    File.WriteAllText(Path.Combine(work, "check-rebuild.log"), output);
    if (exit != 0) return $"FAIL rebuild exited {exit}";
    if (File.GetLastWriteTimeUtc(target) != old) return "FAIL unchanged consumer.ts was rewritten (timestamp changed)";

    (exit, output) = Run(Rebuild("-p:RtCheckMode=true"));
    File.WriteAllText(Path.Combine(work, "check-current.log"), output);
    if (exit != 0) return $"FAIL check mode on current output exited {exit}";

    // RtSkipUnchanged: the first run writes the stamp, the second skips rtcli, an edited output regenerates.
    var current = File.ReadAllText(target);
    (exit, output) = Run(Rebuild("-p:RtSkipUnchanged=true"));
    File.WriteAllText(Path.Combine(work, "skip-first.log"), output);
    if (exit != 0) return $"FAIL RtSkipUnchanged first run exited {exit}";
    (exit, output) = Run(Rebuild("-p:RtSkipUnchanged=true", "-v:n"));
    File.WriteAllText(Path.Combine(work, "skip-second.log"), output);
    if (exit != 0 || !output.Contains("generation; skipped")) return "FAIL RtSkipUnchanged did not skip an unchanged generation";
    (exit, output) = Run(Rebuild("-p:RtSkipUnchanged=true", "-p:RtForce=true", "-v:n"));
    File.WriteAllText(Path.Combine(work, "skip-force.log"), output);
    if (exit != 0 || output.Contains("generation; skipped")) return "FAIL RtForce=true did not force generation";
    File.WriteAllText(target, "// edited\n");
    (exit, output) = Run(Rebuild("-p:RtSkipUnchanged=true", "-v:n"));
    File.WriteAllText(Path.Combine(work, "skip-edited.log"), output);
    if (exit != 0 || output.Contains("generation; skipped") || File.ReadAllText(target) != current)
        return "FAIL RtSkipUnchanged skipped although the output had been edited";

    var drifted = File.ReadAllText(target) + "// drift\n";
    File.WriteAllText(target, drifted);
    (exit, output) = Run(Rebuild("-p:RtCheckMode=true"));
    File.WriteAllText(Path.Combine(work, "check-drift.log"), output);
    if (exit == 0) return "FAIL check mode passed although consumer.ts had drifted";
    if (!output.Contains("error RT0019")) return "FAIL check mode failed without error RT0019";
    if (File.ReadAllText(target) != drifted) return "FAIL check mode modified consumer.ts";
    return "PASS timestamp kept; skip/force/edit; check mode passes, then fails on drift (error RT0019)";
}

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

sealed record Options(string Feed, string Version, string[] Frameworks, string? MsBuild, string? Work, bool UpdateGolden, bool Bypass, bool Modern, bool Check)
{
    public static Options Parse(string[] args)
    {
        string? feed = null, version = null, msbuild = null, work = null;
        var frameworks = new[] { "net10.0", "net11.0", "net481" };
        var update = false;
        var bypass = false;
        var modern = false;
        var check = false;
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
                case "--modern-run": modern = true; break;
                case "--check-run": check = true; break;
                default: throw new ArgumentException("Unknown argument " + args[i]);
            }
        }
        if (feed is null || version is null) throw new ArgumentException("--feed and --version are required");
        return new Options(feed, version, frameworks, msbuild, work, update, bypass, modern, check);
    }
}
