using System;
using Reinforced.Typings.Fluent;
using Xunit;

namespace Reinforced.Typings.Tests.SpecificCases
{
    public interface IExplicitContract
    {
        string Property { get; set; }

        void Run();
    }

    public class ExplicitImplementer : IExplicitContract
    {
        string IExplicitContract.Property
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        void IExplicitContract.Run()
        {
        }

        public int Own { get; set; }
    }

    // Upstream issue #250: explicit interface implementations produced
    // "Namespace.Interface.Property: string", which is not valid TypeScript.
    // Rule: explicit implementations are not part of the type's own surface (they are private in C#
    // and reached only through the interface), so they are not exported; the interface still is.
    public partial class SpecificTestCases
    {
        [Fact]
        public void ExplicitInterfaceImplementationsAreNotExported()
        {
            const string result = @"
module Reinforced.Typings.Tests.SpecificCases {
	export interface IExplicitContract
	{
		Property: string;
		Run() : void;
	}
	export interface IExplicitImplementer extends Reinforced.Typings.Tests.SpecificCases.IExplicitContract
	{
		Own: number;
	}
}";
            var actual = AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment());
                s.ExportAsInterface<IExplicitContract>().WithPublicProperties().WithPublicMethods();
                s.ExportAsInterface<ExplicitImplementer>().WithAllProperties().WithAllMethods();
            }, result);
            Assert.DoesNotContain("IExplicitContract.Property", actual);
        }

        [Fact]
        public void ExplicitInterfaceImplementationsAreNotExportedFromClasses()
        {
            var data = InitializeSingleFile(s =>
            {
                s.Global(a => a.DontWriteWarningComment());
                s.ExportAsInterface<IExplicitContract>().WithPublicProperties().WithPublicMethods();
                s.ExportAsClass<ExplicitImplementer>().WithAllProperties().WithAllMethods();
            });
            data.Exporter.Export();
            var actual = data.Files.ExportedFiles[Sample];
            Assert.DoesNotContain("IExplicitContract.", actual);
            Assert.Contains("Own: number", actual);
        }
    }
}
