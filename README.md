Reinforced.Typings.vNext
=================

> **Note:** This is an actively maintained fork of [Reinforced.Typings](https://github.com/reinforced/Reinforced.Typings), published as `Reinforced.Typings.vNext` on NuGet. The namespaces remain unchanged for easy migration.

What is that?
=================
You develop frontend applications with TypeScript and .NET Backend? You need Reinforced.Typings.

It converts .NET classes into TypeScript code. Suffering has ended - you don't need to write and maintain boring TypeScript glue code for your DTOs/ViewModels/APIs anymore - RT can generate it from C# app. 
Moreover, it integrates with MSBuild build process and (re)generates glue code every time you rebuild .NET backend. So you get immediately notified when backend's API/DTOs changed and frontend is broken.

Check out [documentation](https://github.com/reinforced/Reinforced.Typings/wiki) to discover numbers of useful features (type substitutions, modules, code generators, fluent configuration, multi-file export, JSDOC). 

Installation
=================
```sh
PM> Install-Package Reinforced.Typings.vNext
```

Or via .NET CLI:
```sh
dotnet add package Reinforced.Typings.vNext
```

**Find out detailed information in Reinforced.Typings [wiki](https://github.com/reinforced/Reinforced.Typings/wiki)**

News
=================
> Version **3.1.0**

Every new behaviour is opt-in; with none of the options below set, the generated output is byte-identical to 3.0.0.

- Generated files whose content did not change are no longer rewritten, so their timestamps survive and TypeScript watchers and incremental builds are not invalidated (always on; it changes no content).
- **Check mode** for CI drift gates, **System.Text.Json awareness**, **modern TypeScript output options**, an **Effect Schema emitter** and **skipping unchanged generations**: see [3.1.0 options](#310-options).

> Version **3.0.0**

- Supports projects targeting .NET 11, .NET 10 and .NET Framework 4.8.1 (`net11.0`, `net10.0`, `net481`). `net12.0` and later run the `net11.0` tool with major-version roll-forward; `netstandard2.x` projects use the tool matching the SDK.
- .NET 5 through 9 are no longer shipped: projects targeting them fail the build with a message naming the supported frameworks. `RtForceTargetFramework` (`net481`, `net10.0`, `net11.0`) overrides the choice.
- .NET Framework projects generate with `rtcli.exe`, so they need Windows.
- Built with `dotnet pack` (`dotnet pack Reinforced.Typings/Reinforced.Typings.csproj -c Release -o artifacts`); CI runs the unit tests and an end-to-end package test on Linux and Windows.
- All namespaces unchanged for easy migration from `Reinforced.Typings`

### Migration from Reinforced.Typings

Simply replace your NuGet package reference:
```diff
- <PackageReference Include="Reinforced.Typings" Version="1.6.7" />
+ <PackageReference Include="Reinforced.Typings.vNext" Version="3.1.0" />
```

No code changes required - all namespaces remain `Reinforced.Typings.*`. Only the NuGet package id (`Reinforced.Typings.vNext`) and the package's targets file name carry `vNext`: the assemblies are still `Reinforced.Typings`, `Reinforced.Typings.Integrate` and `rtcli`, `using Reinforced.Typings...` directives are unchanged, the settings file is still `Reinforced.Typings.settings.xml`, and MSBuild properties keep the `Rt*` prefix. A unit test guards the public namespaces and the assembly name.

### 3.1.0 options

MSBuild properties (set them in `Reinforced.Typings.settings.xml`, the project file, or on the command line):

| Property | rtcli argument | Default | Effect |
| --- | --- | --- | --- |
| `RtCheckMode` | `CheckMode=true` | `false` | Generates in memory and compares with the files on disk; nothing is written. Any difference fails the build with `error RT0019` naming the files (`RTE0019_CheckModeDrift` in the API). For CI: `dotnet build -p:RtCheckMode=true`. |
| `RtEffectSchemaFile` | `EffectSchemaFile=path` | empty | Also writes an Effect Schema module to this file (see below). Relative paths are relative to the project directory. |
| `RtSkipUnchanged` | none (task only) | `false` | Does not start rtcli when nothing relevant changed since the last successful generation (see below). |
| `RtForce` | none (task only) | `false` | With `RtSkipUnchanged`, regenerates anyway. |

Generation options (fluent `builder.Global(g => g...)` or `[assembly: TsGlobal(...)]`):

| Fluent method / `TsGlobal` property | Default | Effect |
| --- | --- | --- |
| `UseSystemTextJson()` / `UseSystemTextJson` | off | Honours System.Text.Json attributes. `[JsonPropertyName]` renames (an explicit `TsProperty` name still wins). `[JsonIgnore]` drops the member; with `Condition = WhenWritingNull` or `WhenWritingDefault` the member is kept but optional (`?`), since it may be absent. `[JsonPropertyOrder]` orders members unless a `TsProperty` order is set. An enum with `[JsonConverter(typeof(JsonStringEnumConverter))]` (or the generic converter) is exported with string values, using `[JsonStringEnumMemberName]` when present. A type with `[JsonDerivedType]` (and optionally `[JsonPolymorphic(TypeDiscriminatorPropertyName = ...)]`, default `$type`) also gets a discriminated union alias, `export type <Name>Union = ({ "$type": "a" } & A) \| ...`. Attributes are found by full name through reflection, so no package is added and .NET Framework consumers work unchanged. Not honoured, because reflection cannot see them: converters on individual properties, converters and naming policies registered in `JsonSerializerOptions`. |
| `JsonNamingPolicy(JsonNamingPolicyKind.X)` / `JsonNamingPolicy` | `None` | `CamelCase`, `SnakeCaseLower`, `SnakeCaseUpper`, `KebabCaseLower`, `KebabCaseUpper`, matching System.Text.Json's `JsonNamingPolicy` (unit-tested against it on .NET). Applies to properties without an explicit name and takes precedence over `CamelCaseForProperties`. Works with or without `UseSystemTextJson`. |
| `UseTypeImports()` / `UseTypeImports` | off | Module mode: imports that RT generates for types exported as interfaces (and for enums with `EnumsAsUnionTypes`) are written as `import type`. |
| `EnumsAsUnionTypes()` / `EnumsAsUnionTypes` | off | Enums become `export type E = 0 \| 1` or `"A" \| "B"` instead of `enum`. Code that uses enum members as values (`E.A`, for example in constant initializers) does not compile with this option. |
| `ReadonlyProperties()` / `ReadonlyProperties` | off | Properties and fields are emitted as `readonly`. |
| `StrictNullability()` / `StrictNullability` | off | Separates nullable from optional: `int?` and nullable reference types become `T \| null` and stay required; `?` is used only for members that may be absent (`ForceNullable`, or `JsonIgnore` WhenWritingNull/WhenWritingDefault). While on, it replaces `AutoOptionalProperties` for nullability. |
| `UseNamespaceKeyword()` / `UseNamespaceKeyword` | off | Writes `namespace X {` (and `declare namespace X {` for `.d.ts`) instead of the legacy `module X {`. TypeScript 6.0.3 and 7.0.2 both reject `module X {}` with **error TS1540**, also with `ignoreDeprecations: "6.0"`, so enable this (or `UseModules`) when compiling the output with TypeScript 6 or later. Upstream [#286](https://github.com/reinforced/Reinforced.Typings/issues/286). |
| `ConvertDocumentationTags()` / `ConvertDocumentationTags` | off | Converts inline XML documentation tags in JSDoc: `<see cref="T:X"/>` and `<seealso>` become `{@link X}` when X is an exported type and plain text otherwise (members become `Type.Member`); `<see href>` becomes `{@link url text}`; `<see langword>`, `<paramref>`, `<typeparamref>` become their names; `<c>`/`<code>` become backtick code; other tags keep their text. Without it the XML is copied as it is. Upstream [#310](https://github.com/reinforced/Reinforced.Typings/issues/310). |

#### Upstream issues fixed in 3.1.0

Always on, because they only remove build failures or invalid output:

- [#313](https://github.com/reinforced/Reinforced.Typings/issues/313), [#231](https://github.com/reinforced/Reinforced.Typings/issues/231): a build with `TypeScriptTarget` set but no TypeScript MSBuild targets imported no longer fails with MSB4057 "The target CompileTypeScript does not exist"; projects that do import them still compile their TypeScript after generation.
- [#284](https://github.com/reinforced/Reinforced.Typings/issues/284), [#206](https://github.com/reinforced/Reinforced.Typings/issues/206): RT0999 "Could not load file or assembly System.Text.Json, Version=11.0.0.0" when the consumer references a package newer than the runtime that runs rtcli; the runtime's copy of the assembly is used instead.
- [#250](https://github.com/reinforced/Reinforced.Typings/issues/250): explicit interface implementations (`string IFoo.Bar`) are no longer exported (they produced `Namespace.IFoo.Bar: string`, which is not valid TypeScript); the interface is exported on its own.
- [#221](https://github.com/reinforced/Reinforced.Typings/issues/221): `ulong` enums with values above `long.MaxValue` no longer throw; negative values of signed enums stay negative.
- [#226](https://github.com/reinforced/Reinforced.Typings/issues/226): `rtcli profile` without a path prints usage and exits with code 1 instead of throwing; a missing profile file also exits with code 1.

Opt-in, because they change output: `UseNamespaceKeyword` ([#286](https://github.com/reinforced/Reinforced.Typings/issues/286)) and `ConvertDocumentationTags` ([#310](https://github.com/reinforced/Reinforced.Typings/issues/310)), in the table above.

#### Effect Schema emitter

With `RtEffectSchemaFile` set, the build also writes an ES module for [Effect](https://effect.website) 4 (`import { Schema } from "effect"`). It is derived from the same model as the TypeScript output, so names, optionality, nullability, substitutions and the System.Text.Json options above apply. For each exported DTO `X` it emits `export const X = Schema.Struct({...})`, `export type X = typeof X.Type` and `export type XEncoded = typeof X.Encoded` (an interface's leading `I` is dropped):

- interfaces, and classes that have properties (classes with only methods are not DTOs and are left out); base types are spread with `...Base.fields`;
- optional members use `Schema.optionalKey`, `T | null` uses `Schema.NullOr`; arrays `Schema.Array`, string- or number-keyed dictionaries `Schema.Record`, tuples `Schema.Tuple`;
- enums become `Schema.Literals([...])` with their numeric or string values;
- System.Text.Json discriminated unions become `Schema.Union([Schema.Struct({ ...A.fields, kind: Schema.Literal("a") }), ...])`;
- generic types, types in a reference cycle, and anything that depends on them are not emitted (warning RTW0017); a TypeScript type with no schema equivalent (a raw `TsProperty(Type = ...)`, enum-keyed dictionaries) becomes `Schema.Unknown` (warning RTW0016).

Definitions are ordered so that each schema is declared before it is used. The module generated for the end-to-end consumer is type-checked with TypeScript 6.0 (`strict`, `exactOptionalPropertyTypes`) against `effect` 4.1.3.

#### Skipping unchanged generations

With `RtSkipUnchanged=true` the MSBuild task computes a key from the content of the source assemblies (`IntermediateAssembly` and `RtAdditionalAssembly`) and of the XML documentation file; the path, length and last-write time of every reference, of the task and of the rtcli tool; every setting passed to rtcli; and the content of the current generated output (the target file, or every file under `RtTargetDirectory`, plus the Effect Schema file). If the key equals the one stored in `obj/.../Reinforced.Typings.stamp` by the last successful run, rtcli is not started. Editing or deleting generated output, rebuilding a referenced project, or changing any setting regenerates; check mode never skips; a failed run deletes the stamp. Limitation: an assembly that rtcli resolves from the source assembly's folder without it being in the reference list is not part of the key; set `RtForce=true` if you depend on that.

Best to be used for
=================

Exporting ViewModels
-----------------
<table>
<tr><td align="center" width="48%">C#</td><td></td><td align="center"  width="48%">TypeScript</td></tr>
<tr>
	<td>
<pre lang="csharp">
namespace MyApp
{
    using Reinforced.Typings.Attributes;

    [TsInterface]
    public class Order
    {
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public double Subtotal { get; set; }
        public bool IsPaid { get; set; }
        public string ClientName { get; set; }
        public string Address { get; set; }
    }
    
    [TsClass]
    public class User
    {
        public string FirstName { get; set; }
        public string Email { get; set; }
        public UserType Type { get; set; }
    }

    [TsEnum]
    public enum UserType { One, Two }
}
</pre>
</td>
	<td><h1>&#8680;</h1></td>
	<td>
	<pre lang="typescript">
module MyApp {
	export interface IOrder
	{
		ItemName: string;
		Quantity: number;
		Subtotal: number;
		IsPaid: boolean;
		ClientName: string;
		Address: string;
	}
	export class User
	{
		public FirstName: string;
		public Email: string;
		public Type: MyApp.UserType;
	}
	export enum UserType { 
		One = 0, 
		Two = 1, 
	}
}	
	</pre>
	</td>
</tr>
</table>

...even complex ViewModels
-------------
<table>
<tr><td align="center" width="43%">C#</td><td></td><td align="center"  width="48%">TypeScript</td></tr>
<tr>
	<td>
<pre lang="csharp">
namespace MyApp
{
    using Reinforced.Typings.Attributes;

    [TsInterface]
    public class Page
    {
        public List<Order> Orders { get; set; }

        public Dictionary<int, Order> 
                        Cache { get; set; }

        public string[] Tags { get; set; }

        public IEnumerable<object> 
                        Things { get; set; }
    }
}
</pre>
</td>
	<td><h1>&#8680;</h1></td>
	<td>
	<pre lang="typescript">
module MyApp {
	export interface IPage
	{
		Orders: MyApp.IOrder[];
		Cache: { [key:number]: MyApp.IOrder };
		Tags: string[];
		Things: any[];
	}
}	
	</pre>
	</td>
</tr>
</table>

Temporary disabling TypeScript compilation in your project
-------------
Now you will not stay powerless when generated typings fail your TypeScript build in project. See [RtBypassTypeScriptCompilation](https://github.com/reinforced/Reinforced.Typings/wiki/Reinforced.Typings.settings.xml#RtBypassTypeScriptCompilation) configuration parameter.

Inheritance preservation
-------------
<table>
<tr><td align="center" width="43%">C#</td><td></td><td align="center"  width="48%">TypeScript</td></tr>
<tr>
	<td>
<pre lang="csharp">
namespace MyApp
{
    using Reinforced.Typings.Attributes;

    public interface INonExport
    {
        string Boom { get; }
    }

    [TsInterface]
    public class WithoutInterface
                : INonExport
    {
        public string Boom { get; set; }
    }

    [TsInterface]
    public interface IEntity
    {
        int Id { get; set; }
    }

    [TsInterface]
    public class User : IEntity
    {
        public int Id { get; set; }

        public string Login { get; set; }
    }
}
</pre>
</td>
	<td><h1>&#8680;</h1></td>
	<td>
	<pre lang="typescript">
module MyApp {
	export interface IWithoutInterface
	{
		Boom: string;
	}
	export interface IEntity
	{
		Id: number;
	}
	export interface IUser extends MyApp.IEntity
	{
		Id: number;
		Login: string;
	}
}	
	</pre>
	</td>
</tr>
</table>

Use fluent configuration
-------------
Details can be found [on the corresponding wiki page](https://github.com/reinforced/Reinforced.Typings/wiki/Fluent-configuration)
<table>
<tr><td align="center" width="43%">C#</td><td></td><td align="center"  width="48%">TypeScript</td></tr>
<tr>
	<td>
<pre lang="csharp">
namespace MyApp
{
    using Reinforced.Typings.Fluent;
    using System.Web.Mvc;
    
    public class Configuration
    {
        public static void 
            Configure(ConfigurationBuilder builder)
        {
            builder
            	.ExportAsInterface<SelectListItem>()
                .OverrideNamespace("MyApp")
                .WithPublicProperties();
        }
    }
}
</pre>
</td>
	<td><h1>&#8680;</h1></td>
	<td>
	<pre lang="typescript">
module MyApp {
	export interface ISelectListItem
	{
		Disabled: boolean;
		Group: any;
		Selected: boolean;
		Text: string;
		Value: string;
	}
}	
	</pre>
	</td>
</tr>
<tr><td align="center" colspan="3">Reinforced.Typings.settings.xml: <code>&lt;RtConfigurationMethod&gt;MyApp.Configuration.Configure&lt;/RtConfigurationMethod&gt;</code></td></tr>
</table>

Generate any custom glue code
-------------
Read more [here](https://github.com/reinforced/Reinforced.Typings/wiki#writing-custom-code-generators).

<table>
<tr><td align="center" width="30%">C#</td><td></td><td align="center"  width="48%">TypeScript</td></tr>
<tr>
	<td>
<pre lang="csharp">
namespace MyApp
{
    using Reinforced.Typings.Fluent;
    using System.Web.Mvc;
    
    [TsClass(CodeGeneratorType = typeof(AngularControllerGenerator)]
    public class AngularController : Controller
    {
        [AngularMethod(typeof(SampleResponseModel))]
        public ActionResult Save(Order order)
        {
            return Json(new {
                Message = "Success",
                Success = true
            });
        }
    }
    
    public class AngularMethodAttribute 
            : TsFunctionAttribute
    {
        public AngularMethodAttribute(Type returnType)
        {
            StrongType = returnType;
            CodeGeneratorType = typeof 
             (AngularActionCallGenerator);
        }
    }
    
    public class AngularActionCallGenerator 
            : MethodCodeGenerator
    {
        // too long - see sample
    }
    
    public class AngularControllerGenerator 
            : ClassCodeGenerator
    {
        // too long - see sample
    }
    
    [TsInterface]
    public class SampleResponseModel
    {
        public string Message { get; set; }
        public bool Success { get; set; }    
    }
}
</pre>
</td>
	<td><h1>&#8680;</h1></td>
	<td>
	<pre lang="typescript">
module MyApp {
	export interface ISampleResponseModel
	{
		Message: string;
		Success: boolean;
	}
    
	if (window['app']) {
        window['app'].factory('Api.AngularController', 
        ['$http', 
            ($http: angular.IHttpService) => new AngularController($http)]);
    }
    
	/** Result of AngularControllerGenerator activity */
	export class AngularController
	{
		constructor ($http: angular.IHttpService)
		{
			this.http = $http;
		}
		public Save(order: IOrder) : angular.IPromise&lt;ISampleResponseModel&gt;
		{
			var params = { 'order': order };
			return this.http.post('/Angular/Save', params)
			    .then((response) => { return response.data; });
		}
    }        
}	
	</pre>
	</td>
</tr>
</table>

