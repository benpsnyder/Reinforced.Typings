# Reinforced.Typings.vNext: .NET 11 migration plan

Target release: **Reinforced.Typings.vNext 3.0.0**
Supported consumers after this work: **.NET 11**, **.NET 10**, **.NET Framework 4.8.1**.
Dropped: .NET 5 through 9 (all shipped in 2.0.3 today).

## 1. Where things stand

All vNext work lives on the `fork` branch (last commit 2025-12-21, version 2.0.3). `master` only mirrors upstream at 1.6.7. Every step below branches from `fork`.

| Area | Today (2.0.3) |
| --- | --- |
| Package assets | `lib/` and `tools/` for net481, net5.0, net6.0, net7.0, net8.0, net9.0, net10.0; MSBuild task in `build/net481` and `build/netstandard2.0` |
| Project files | Three flavours per project (`.csproj` legacy v4.5, `.Dev.csproj`, `.NETCore.csproj`), three `.slnx` files |
| Build | `cake/build.cake` rewrites `<TargetFramework>` by regex and publishes once per framework, then packs a hand-written nuspec with a checked-in `nuget.exe` |
| Code | 21k lines of C#; 38 `#if NETCORE` blocks, most of them `GetTypeInfo()` shims for .NET Core 1.x; no C# changes versus upstream |
| Tests | Test project targets `netstandard2.0;netstandard2.1;netcoreapp2.1;netcoreapp2.2;netcoreapp3.1`, so it cannot run on a current SDK; CI runs it with `\|\| true` |
| In-house use | `snyder-apps` pins 2.0.3 centrally; 54 project references, all `net10.0` |

### Defects found while reading the code

These were read from the `fork` branch and the installed 2.0.3 package; none was reproduced by running a build.

1. **.NET Framework builds under Visual Studio cannot find the task.** `Reinforced.Typings.vNext.targets` points full-framework MSBuild at `build/net46/`, but the package ships `build/net481/`.
2. **.NET Framework consumers resolve a tools folder that does not exist.** `RtCli.NormalizeFramework()` maps `net48*` to `net461`; the package ships only `tools/net481/`.
3. **A `net11.0` consumer would be treated as .NET Framework.** `RtCli.IsCore` is a hardcoded list ending at `net10`, so `net11.0` falls through to `rtcli.exe`.
4. **The nuspec declares dependency groups for frameworks it no longer ships** (net45, net461, netstandard1.5, netcoreapp1.0 through 3.1).
5. **The tests do not gate anything** (see table).
6. **Consumers carry a workaround.** `snyder-apps/Directory.Build.Config.props` adds two `Microsoft.CodeAnalysis.*Scripting` references to every project because of a transitive-pinning failure attributed to this package. No C# in the fork uses Roslyn; the pins in `Directory.Packages.props` are the only trace. Cause not yet established.

Defects 1 and 2 mean the ".NET Framework 4.8.1" half of the requirement is probably not working today, so this plan treats it as new work to prove, not existing behaviour to preserve.

## 2. Target shape

### Framework matrix

| Asset | Frameworks | Notes |
| --- | --- | --- |
| `lib/` (attributes, fluent API, generators) | `net481`, `net10.0`, `net11.0` | |
| `tools/` (`rtcli`) | `net481`, `net10.0`, `net11.0` | The tool executes the consumer's assembly, so it must run on a runtime at least as new as the consumer's |
| `build/` (MSBuild task) | `net472`, `netstandard2.0` | `net472` is what Visual Studio's MSBuild loads; `netstandard2.0` loads in every `dotnet` SDK. The task only spawns the tool, so it gains nothing from newer frameworks |

### Tool selection rule (replaces the hardcoded lists)

Parse the consumer's `TargetFramework` once, in the task:

| Consumer framework | Tool used |
| --- | --- |
| `net481` | `tools/net481/rtcli.exe` |
| `net10.0` | `tools/net10.0/rtcli.dll` |
| `net11.0` | `tools/net11.0/rtcli.dll` |
| `net12.0` or later | `tools/net11.0/rtcli.dll` with major-version roll-forward, so the next release does not break on day one |
| `netstandard2.x` | The tool matching the SDK's own runtime (`$(BundledNETCoreAppTargetFrameworkVersion)`) |
| `net5.0` through `net9.0`, older .NET Framework | Build error with a clear message naming the supported frameworks; `RtForceTargetFramework` stays as the override |

### One project file per project

Collapse the three flavours into one SDK-style project each, with `<TargetFrameworks>net481;net10.0;net11.0</TargetFrameworks>`. This removes the regex rewriting in Cake, `packages.config`, the legacy v4.5 projects and two of the three solution files. The `net481` target builds on macOS and Linux through `Microsoft.NETFramework.ReferenceAssemblies`; it can only be *run* on Windows.

## 3. Phases

Each phase ends with a check that must pass before the next starts.

### Phase 0: baseline

- Branch `feat/net11` from `fork`. Merge upstream `master` first (9 commits, README only).
- Install the .NET 11 SDK and add a `global.json` to the fork pinning it. Only SDK 10.0.403 is installed on this machine.
- Capture golden output: run 2.0.3 against the repo's samples and against three or four real `snyder-apps` projects, and save the generated TypeScript.
- Capture timing: wall-clock cost of the `ReinforcedTypingsGenerate` target across a full meritOS build (54 tool launches).

**Check:** golden files and a timing number are committed to the branch.

### Phase 1: make the tests real

- Retarget the test project to `net481;net10.0;net11.0`; move to current xunit and `Microsoft.NET.Test.Sdk`.
- Add an end-to-end test: pack the package into a local feed, build one small consumer project per supported framework against it, and diff the generated TypeScript against the golden files. This is the only test that exercises the task, the targets file and the tool selection together.
- Remove `|| true` from CI. Add a Windows job, since `net481` tests and the Visual Studio MSBuild path cannot run elsewhere.

**Check:** the unit tests pass on `net10.0`; the end-to-end test fails on `net481` for the reasons in defects 1 and 2. That failure is the proof the test works.

### Phase 2: consolidate and fix the matrix

- Single multi-targeted project files (section 2).
- Implement the tool selection rule; fix the task folder name in the targets file. This closes defects 1, 2 and 3.
- Delete dead conditionals: every `#if NETCORE` shim, the `NETCOREAPP1_0` / `NETSTANDARD1_x` branches, and the `System.Runtime`, `System.Runtime.Loader` and `System.Xml.XmlSerializer` 4.3.0 package references, which are part of the framework on every remaining target. What remains is a single `#if NETFRAMEWORK` split for assembly loading.
- Find the cause of defect 6 and remove it at the source.

**Check:** the end-to-end test passes for `net481`, `net10.0` and `net11.0`, with output identical to the Phase 0 golden files.

### Phase 3: optimise

Do these in order and keep each only if the Phase 0 timing improves and the golden output is unchanged.

1. **Replace `XmlSerializer` in `DocumentationManager`** with a hand-written `XmlReader` pass. `XmlSerializer` generates a serialization assembly at start-up on every run; this is likely the largest single start-up cost and the fix helps all three frameworks. It also removes the `Reinforced.Typings.XmlSerializers` special case in the assembly resolver.
2. **Stop building the tool with `Microsoft.NET.Sdk.Web`.** Use `Microsoft.NET.Sdk` plus a `FrameworkReference` to `Microsoft.AspNetCore.App` (still needed so consumers' ASP.NET types load). This drops `web.config`, the static-web-assets manifest and the web host start-up configuration from a console tool.
3. **Load consumer assemblies in a dedicated `AssemblyLoadContext`** backed by `AssemblyDependencyResolver` and the consumer's own `.deps.json`, falling back to the reference list the task already passes. Today everything is loaded into the default context with a hand-rolled probing handler, which is where the RTW0013/RTW0014 warnings come from. `net481` keeps the `AppDomain.AssemblyResolve` path.
4. **Use `NullabilityInfoContext`** on `net10.0`/`net11.0` in place of the string-matched `NullableAttribute` reader in `TypeExtensions.cs`; keep the existing reader for `net481`. Only if output is byte-identical.
5. **Runtime configuration for a short-lived process** on `net11.0`: invariant globalization, workstation non-concurrent GC, and tiered-compilation settings chosen by measurement.
6. **.NET 11-only APIs and C# 15 features** in hot paths found by profiling the 54-project build. I have not listed specific APIs because I could not verify the .NET 11 API surface from here; this item is driven by a profile, not a wish list.

Rejected: ReadyToRun or Native AOT for the tool. Both are per-OS and per-architecture, which would multiply the package size, and AOT cannot load and execute the consumer's assembly.

**Check:** timing is better than Phase 0 on `net10.0` and `net11.0`, no worse on `net481`, golden output unchanged.

### Phase 4: package and release

- Replace Cake and the checked-in `nuget.exe` with `dotnet pack` driven from the project files; keep one small targets file that lays out `tools/` and `build/`.
- Clean the package metadata: dependency groups for the three shipped frameworks only, no `frameworkAssemblies`, repository URL and commit, README, SourceLink, a symbols package, deterministic build.
- Publish `3.0.0-rc.1` to nuget.org, then `3.0.0` once it has been through Phase 5.

**Check:** the package installs into a clean project on each of the three frameworks with no extra references and no warnings.

### Phase 5: adopt in snyder-apps

- Bump `Reinforced.Typings.vNext` to the release candidate in `Directory.Packages.props`.
- Remove the two `Microsoft.CodeAnalysis.*Scripting` workaround references from `Directory.Build.Config.props`.
- `just dotnet-build` the meritOS and infraOS solutions; confirm generated TypeScript has no diff.

**Check:** no diff in generated files, and the workaround is gone. `snyder-apps` is all `net10.0`, so this proves the .NET 10 path on real code. The .NET 11 path is covered only by the end-to-end test until `snyder-apps` itself moves.

## 4. Decisions needed

1. **Drop .NET 5 through 9?** The request names only 11, 10 and 4.8.1, so this plan drops them and calls the release 3.0.0. Keeping .NET 8 (still in support) costs one more framework in each of `lib/` and `tools/`.
2. **Signing key.** The assemblies are strong-named, but `Reinforced.snk` is not in the repository and CI generates a throwaway key. Which key signed 2.0.3, and where is it kept? A different key in 3.0.0 changes the assembly identity.
3. **.NET 11 status.** GA is scheduled for this month and I could not confirm from here whether it has shipped. If it is still a release candidate, 3.0.0 stays at `-rc` until it does.
4. **CI runners.** The existing workflow uses GitHub-hosted runners. `snyder-apps` requires self-hosted runners; say whether that rule extends to this personal fork.

## 5. Existing branches: what goes into 3.0.0

Reviewed 2026-11-12 against `origin` at the commits shown. There are no pull requests on the fork.

| Branch | Tip | Status against `fork` | Verdict |
| --- | --- | --- | --- |
| `fork` | `68144b5` (2025-12-21) | Base of the 2.0.3 release | **Base for 3.0.0.** Branch `feat/net11` from here |
| `feat/central-package-management` | `d403e53` | Fully contained in `fork` | Nothing to integrate; delete the branch after 3.0.0 |
| `feat/code-analysys-5` | `315efb5` | Fully contained in `fork` | Nothing to integrate; delete the branch after 3.0.0 |
| `feat/migrate-from-sln-to-slnx` | `90e705a` | Superseded by `8a462fa` on `fork`, which redid the same conversion | One file is missing from `fork`: `Samples/Reinforced.Typings.Samples.slnx`. Take it only if the samples become the end-to-end test consumers (Phase 1); otherwise nothing. Delete the branch afterwards |
| `master` | `1601c2c` (2026-10-23) | 9 upstream commits ahead of the fork point | All nine edit `README.md`; the net change is removing the 7-line "Support policy" section. No code. Merge for a clean ancestry, or skip |

So no unmerged feature work exists outside `fork`. The question is which parts of what `fork` already contains survive into 3.0.0.

### Already on `fork`: keep, change or remove

| Work (commit) | 3.0.0 |
| --- | --- |
| Central package management, `Directory.Packages.props` (`d403e53`) | **Keep**, then prune: the `System.Runtime*` and `System.Xml.XmlSerializer` 4.3.x entries go in Phase 2 |
| Seven `Microsoft.CodeAnalysis.*` version pins (`40856f1`, `68144b5`) | **Remove unless Phase 2 proves they are needed.** No project references any of them, and `PrivateAssets` is not a valid attribute on `PackageVersion`, so that part has no effect. They exist only to steer transitive versions; this is the fork side of defect 6 |
| vNext package identity: nuspec, renamed targets files, README fork notice (`7ec2c75`, `8a462fa`) | **Keep.** Fix the task folder name (defect 1) and the stale dependency groups (defect 4). README needs its framework list and version updated, and "4.8.1 support via Mono" removed |
| `.slnx` solutions (`8a462fa`) | **Keep one**, delete the other two with the project consolidation |
| `.Dev.csproj` flavour, retargeted to `net5.0` (`315efb5`) | **Remove** with the project consolidation |
| Cake script trimmed to net481 + net5.0–net10.0 (`315efb5`) | **Remove** in Phase 4 |
| GitHub workflow `test-dotnet-versions.yml` (`315efb5`) | **Rewrite.** Matrix becomes net481 (Windows), net10.0, net11.0; tests must fail the job |
| Docker test matrix and `test-all-versions.ps1` (`315efb5`) | **Remove.** It exists to cover .NET 5–9 SDK images, which 3.0.0 drops |
| Mono-based net481 check, `docker/Dockerfile.mono` (`315efb5`) | **Remove.** Every build step ends in `\|\| echo "Build completed with warnings"` and the output folders are created with `touch`, so it passes whether or not the build worked. A Windows job replaces it |
| `Reinforced.Typings.Cli/app.config` (`315efb5`) | **Fix or remove.** Still declares `.NETFramework,Version=v4.5` for a tool that now targets 4.8.1 |
| Empty `dotnet-tools.json` (`d403e53`) | **Remove** once Cake is gone |

### One more defect, found in this review

7. **A file with a Windows drive letter in its path is committed** (`d403e53`): `Reinforced.Typings.Tests/w:/Work/Reinforced/Typings/Reinforced.Typings/Reinforced.Typings.Tests/Reinforced.Typings.Tests.Dev.xml`. It is a stray XML-doc output. A path containing `:` cannot be checked out on Windows, which matters because 3.0.0 needs a Windows CI job. Delete it and the sibling `Reinforced.Typings.Tests.Dev.xml` in Phase 0, and ignore `*.Dev.xml`.
