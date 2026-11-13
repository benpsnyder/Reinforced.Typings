// Times rtcli in isolation: the same profile the MSBuild task would pass, run repeatedly.
//
// Usage:
//   dotnet run e2e/measure.cs -- --inputs <generated/rt-inputs.txt from an e2e consumer build>
//       --tool <path to rtcli.dll> [--runs 30] [--warmup 3] [--golden e2e/golden/consumer.ts]
//
// Each run launches "dotnet <rtcli.dll> profile <file>" exactly as the task does on .NET, and
// checks the generated file against the golden file. Prints min/median/mean/max wall-clock ms.

using System.Diagnostics;

string? inputs = null, tool = null, golden = null;
int runs = 30, warmup = 3;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--inputs": inputs = args[++i]; break;
        case "--tool": tool = args[++i]; break;
        case "--golden": golden = args[++i]; break;
        case "--runs": runs = int.Parse(args[++i]); break;
        case "--warmup": warmup = int.Parse(args[++i]); break;
        default: throw new ArgumentException("Unknown argument " + args[i]);
    }
}
if (inputs is null || tool is null) throw new ArgumentException("--inputs and --tool are required");

var lines = File.ReadAllLines(inputs);
string Value(string key) => lines.First(l => l.StartsWith(key + "=")).Substring(key.Length + 1);
var source = Value("source");
var doc = Value("doc");
var method = Value("method");
var refs = lines.Where(l => l.StartsWith("ref=")).Select(l => l.Substring(4)).ToArray();

var scratch = Directory.CreateTempSubdirectory("rt-measure-");
var output = Path.Combine(scratch.FullName, "out.ts");
var goldenBytes = golden is null ? null : File.ReadAllBytes(golden);
var times = new List<double>();
var mismatches = 0;

for (var i = 0; i < warmup + runs; i++)
{
    var profile = Path.Combine(scratch.FullName, $"profile-{i}.txt");
    var profileLines = new List<string> { output, "", "", "False", doc, method, "", "1", source };
    profileLines.AddRange(refs);
    File.WriteAllLines(profile, profileLines);
    if (File.Exists(output)) File.Delete(output);

    var psi = new ProcessStartInfo("dotnet") { ArgumentList = { tool, "profile", profile }, RedirectStandardOutput = true, RedirectStandardError = true };
    var sw = Stopwatch.StartNew();
    using (var p = Process.Start(psi)!)
    {
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        sw.Stop();
        if (p.ExitCode != 0) throw new Exception($"rtcli exited {p.ExitCode}");
    }
    if (goldenBytes is not null && !File.ReadAllBytes(output).AsSpan().SequenceEqual(goldenBytes)) mismatches++;
    if (i >= warmup) times.Add(sw.Elapsed.TotalMilliseconds);
}

times.Sort();
var median = times.Count % 2 == 1 ? times[times.Count / 2] : (times[times.Count / 2 - 1] + times[times.Count / 2]) / 2;
Console.WriteLine($"tool={tool}");
Console.WriteLine($"runs={times.Count} warmup={warmup} min={times[0]:F0}ms median={median:F0}ms mean={times.Average():F0}ms max={times[^1]:F0}ms golden-mismatches={(goldenBytes is null ? "n/a" : mismatches.ToString())}");
scratch.Delete(recursive: true);
return mismatches;
