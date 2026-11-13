using Reinforced.Typings.Fluent;
using Xunit;

namespace Reinforced.Typings.Tests.SpecificCases
{
    public class NamespaceKeywordDto
    {
        public string Name { get; set; }
    }

    // Upstream issue #286: TypeScript 6 and 7 reject "module X {}" (TS1540).
    public partial class SpecificTestCases
    {
        [Fact]
        public void NamespaceKeywordWhenEnabled()
        {
            const string result = @"
namespace Reinforced.Typings.Tests.SpecificCases {
	export interface INamespaceKeywordDto
	{
		Name: string;
	}
}";
            var actual = AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().UseNamespaceKeyword());
                s.ExportAsInterface<NamespaceKeywordDto>().WithPublicProperties();
            }, result);
            Assert.StartsWith("namespace ", actual.TrimStart());
        }

        [Fact]
        public void ModuleKeywordByDefault()
        {
            var actual = AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment());
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
        public void DeclareNamespaceInTypingsWhenEnabled()
        {
            var actual = AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().ExportPureTypings().UseNamespaceKeyword());
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
    }
}
