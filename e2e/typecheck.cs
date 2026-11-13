// Type-checks every e2e golden TypeScript file with TypeScript 6 and TypeScript 7 in strict mode.
// The 3.1.0 default output must compile under both (no TS1540 for "module X {}", no TS2322 for the
// stub method bodies), and the Effect Schema golden must compile against effect 4.
//
// Usage:
//   dotnet run e2e/typecheck.cs -- --node-modules <dir> [--node <path to node>] [--work <scratch dir>] [--golden <dir, default e2e/golden>]
//
// <dir> is a node_modules folder that contains the pinned compilers and effect, for example:
//   npm install --no-save --prefix <tools> @typescript/typescript6@6.0.2 typescript@7.0.2 effect@4.1.3
//   dotnet run e2e/typecheck.cs -- --node-modules <tools>/node_modules
// These are the packages behind the tsc6/tsc7 binaries of the main in-house consumer.
// Exit code is the number of golden/compiler pairs with errors (0 = all clean).

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

string? nodeModules = null, node = null, work = null, goldenDir = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--node-modules": nodeModules = args[++i]; break;
        case "--node": node = args[++i]; break;
        case "--work": work = args[++i]; break;
        case "--golden": goldenDir = args[++i]; break;
        default: throw new ArgumentException("Unknown argument " + args[i]);
    }
}
if (nodeModules is null) throw new ArgumentException("--node-modules is required");
nodeModules = Path.GetFullPath(nodeModules);
node ??= "node";

var compilers = new (string Label, string Path)[]
{
    ("TypeScript 6", Path.Combine(nodeModules, "@typescript", "typescript6", "bin", "tsc6")),
    ("TypeScript 7", Path.Combine(nodeModules, "typescript", "bin", "tsc")),
};
foreach (var c in compilers)
{
    if (!File.Exists(c.Path)) throw new FileNotFoundException($"{c.Label} compiler not found", c.Path);
}

var e2eDir = Path.GetDirectoryName(Path.GetFullPath(ThisFile()))!;
var goldens = Directory.GetFiles(goldenDir ?? Path.Combine(e2eDir, "golden"), "*.ts").OrderBy(f => f, StringComparer.Ordinal).ToArray();
if (goldens.Length == 0) throw new InvalidOperationException("No golden .ts files found");
work = Path.GetFullPath(work ?? Path.Combine(Path.GetTempPath(), "rt-typecheck"));
if (Directory.Exists(work)) Directory.Delete(work, recursive: true);
Directory.CreateDirectory(work);

var failures = 0;
var results = new List<string>();
foreach (var golden in goldens)
{
    // Each golden is checked on its own (the files share namespaces and type names).
    var name = Path.GetFileName(golden);
    var dir = Path.Combine(work, name + ".d");
    Directory.CreateDirectory(dir);
    File.Copy(golden, Path.Combine(dir, name));
    // Normal package resolution (honours effect's "exports") through a node_modules link.
    Directory.CreateSymbolicLink(Path.Combine(dir, "node_modules"), nodeModules);
    File.WriteAllText(Path.Combine(dir, "tsconfig.json"), $$"""
        {
          "compilerOptions": {
            "strict": true,
            "noEmit": true,
            "target": "es2022",
            "module": "esnext",
            "moduleResolution": "bundler",
            "skipLibCheck": true,
            "types": [],
            "typeRoots": []
          },
          "files": ["{{name}}"]
        }
        """);
    foreach (var c in compilers)
    {
        var psi = new ProcessStartInfo(node) { WorkingDirectory = dir };
        psi.ArgumentList.Add(c.Path);
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(Path.Combine(dir, "tsconfig.json"));
        var (exit, output) = Run(psi);
        var errors = Regex.Matches(output, @"error TS\d+").Count;
        var verdict = exit == 0 && errors == 0 ? "PASS 0 errors" : $"FAIL exit {exit}, {errors} errors";
        if (!verdict.StartsWith("PASS"))
        {
            failures++;
            Console.WriteLine($"---- {name} with {c.Label}:");
            foreach (var line in output.Split('\n').Take(40)) Console.WriteLine("  " + line.TrimEnd());
        }
        results.Add($"{name,-28} {c.Label,-13} {verdict}");
    }
}

Console.WriteLine();
Console.WriteLine("Golden type-check (strict):");
foreach (var r in results) Console.WriteLine("  " + r);
Console.WriteLine($"{results.Count - failures} passed, {failures} failed.");
return failures;

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
