using Reinforced.Typings.Fluent;
using Xunit;

namespace Reinforced.Typings.Tests.SpecificCases
{
    public class NamespaceKeywordDto
    {
        public string Name { get; set; }
    }

    public class NamespaceKeywordService
    {
        public NamespaceKeywordDto Load(int id) { return null; }

        public System.Threading.Tasks.Task<NamespaceKeywordDto> LoadAsync(int id) { return null; }
    }

    // Upstream issue #286: TypeScript 6 and 7 reject "module X {}" (TS1540). Since 3.1.0 "namespace" is
    // the default; UseNamespaceKeyword(false) restores the legacy "module" keyword.
    public partial class SpecificTestCases
    {
        [Fact]
        public void NamespaceKeywordByDefault()
        {
            var actual = AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment());
                s.ExportAsInterface<NamespaceKeywordDto>().WithPublicProperties();
            }, @"
namespace Reinforced.Typings.Tests.SpecificCases {
	export interface INamespaceKeywordDto
	{
		Name: string;
	}
}");
            Assert.StartsWith("namespace ", actual.TrimStart());
        }

        [Fact]
        public void NamespaceKeywordWhenEnabledExplicitly()
        {
            var actual = AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().UseNamespaceKeyword());
                s.ExportAsInterface<NamespaceKeywordDto>().WithPublicProperties();
            }, @"
namespace Reinforced.Typings.Tests.SpecificCases {
	export interface INamespaceKeywordDto
	{
		Name: string;
	}
}");
            Assert.StartsWith("namespace ", actual.TrimStart());
        }

        [Fact]
        public void ModuleKeywordWhenDisabled()
        {
            var actual = AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().UseNamespaceKeyword(false));
                s.ExportAsInterface<NamespaceKeywordDto>().WithPublicProperties();
            }, @"
module Reinforced.Typings.Tests.SpecificCases {
	export interface INamespaceKeywordDto
	{
		Name: string;
	}
}");
            Assert.StartsWith("module ", actual.TrimStart());
        }

        [Fact]
        public void DeclareNamespaceInTypingsByDefault()
        {
            var actual = AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().ExportPureTypings());
                s.ExportAsInterface<NamespaceKeywordDto>().WithPublicProperties();
            }, @"
declare namespace Reinforced.Typings.Tests.SpecificCases {
	export interface INamespaceKeywordDto
	{
		Name: string;
	}
}");
            Assert.StartsWith("declare namespace ", actual.TrimStart());
        }

        [Fact]
        public void DeclareModuleInTypingsWhenDisabled()
        {
            var actual = AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().ExportPureTypings().UseNamespaceKeyword(false));
                s.ExportAsInterface<NamespaceKeywordDto>().WithPublicProperties();
            }, @"
declare module Reinforced.Typings.Tests.SpecificCases {
	export interface INamespaceKeywordDto
	{
		Name: string;
	}
}");
            Assert.StartsWith("declare module ", actual.TrimStart());
        }

        [Fact]
        public void HierarchicalOutputUsesNamespaceByDefault()
        {
            var data = InitializeMultipleFiles(s =>
            {
                s.Global(a => a.DontWriteWarningComment());
                s.ExportAsInterface<NamespaceKeywordDto>().WithPublicProperties();
            });
            data.Exporter.Export();
            foreach (var file in data.Files.ExportedFiles.Values)
            {
                Assert.DoesNotContain("module ", file);
            }
        }

        // The stub method bodies of exported classes must type-check under strict null checks.
        [Fact]
        public void StubMethodBodiesUseNonNullAssertion()
        {
            var data = InitializeSingleFile(s =>
            {
                s.Global(a => a.DontWriteWarningComment());
                s.ExportAsInterface<NamespaceKeywordDto>().WithPublicProperties();
                s.ExportAsClass<NamespaceKeywordService>().WithPublicMethods();
            });
            data.Exporter.Export();
            var actual = data.Files.ExportedFiles[Sample];
            Assert.Contains("return null!;", actual);
            Assert.DoesNotContain("return null;", actual);
        }
    }
}
