using System.Linq;
using Reinforced.Typings.Fluent;
using Xunit;

namespace Reinforced.Typings.Tests.SpecificCases
{
    public class ModernDto
    {
        public string Name { get; set; }

        public int? Count { get; set; }

        public int Total { get; set; }
    }

    public class ModernRef
    {
        public ModernDto Dto { get; set; }
    }

    public partial class SpecificTestCases
    {
        [Fact]
        public void ReadonlyProperties()
        {
            const string result = @"
module Reinforced.Typings.Tests.SpecificCases {
	export interface IModernDto
	{
		readonly name: string;
		readonly count: number;
		readonly total: number;
	}
}";
            AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().CamelCaseForProperties().ReadonlyProperties());
                s.ExportAsInterface<ModernDto>().WithPublicProperties();
            }, result);
        }

        [Fact]
        public void StrictNullabilitySeparatesNullFromOptional()
        {
            const string result = @"
module Reinforced.Typings.Tests.SpecificCases {
	export interface IModernDto
	{
		name: string;
		count: number | null;
		total?: number;
	}
}";
            AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().CamelCaseForProperties().AutoOptionalProperties().StrictNullability());
                s.ExportAsInterface<ModernDto>()
                    .WithPublicProperties()
                    .WithProperty(x => x.Total, p => p.ForceNullable());
            }, result);
        }

        [Fact]
        public void TypeImportsForInterfaces()
        {
            var data = InitializeMultipleFiles(s =>
            {
                s.Global(a => a.DontWriteWarningComment().UseModules().UseTypeImports());
                s.ExportAsInterface<ModernDto>().WithPublicProperties().ExportTo("Dto/ModernDto.ts");
                s.ExportAsInterface<ModernRef>().WithPublicProperties().ExportTo("Ref/ModernRef.ts");
            });
            data.Exporter.Export();
            var refFile = data.Files.ExportedFiles.Single(f => f.Value.Contains("interface IModernRef")).Value;
            Assert.Contains("import type { IModernDto } from '../Dto/ModernDto';", refFile);
        }

        [Fact]
        public void PlainImportsByDefault()
        {
            var data = InitializeMultipleFiles(s =>
            {
                s.Global(a => a.DontWriteWarningComment().UseModules());
                s.ExportAsInterface<ModernDto>().WithPublicProperties().ExportTo("Dto/ModernDto.ts");
                s.ExportAsInterface<ModernRef>().WithPublicProperties().ExportTo("Ref/ModernRef.ts");
            });
            data.Exporter.Export();
            var refFile = data.Files.ExportedFiles.Single(f => f.Value.Contains("interface IModernRef")).Value;
            Assert.Contains("import { IModernDto } from '../Dto/ModernDto';", refFile);
            Assert.DoesNotContain("import type", refFile);
        }
    }
}
