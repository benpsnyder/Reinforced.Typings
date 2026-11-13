using Reinforced.Typings.Fluent;
using Reinforced.Typings.Xmldoc;
using Xunit;

namespace Reinforced.Typings.Tests.SpecificCases
{
    /// <summary>Handles a collection of <see cref="DocTagItem" />s and <see cref="DocTagHidden"/>; see <typeparamref name="items"/>, <c>Count</c> and <see langword="null"/>.</summary>
    public class DocTagBar
    {
        /// <summary>The <see cref="DocTagItem.X"/> values of <see cref="DocTagItem">the items</see>.</summary>
        public int[] Values { get; set; }
    }

    /// <summary>One item.</summary>
    public class DocTagItem
    {
        /// <summary>X.</summary>
        public int X { get; set; }
    }

    /// <summary>Not exported.</summary>
    public class DocTagHidden
    {
    }

    // Upstream issue #310: <see cref="..."/> and similar inline tags were copied raw into JSDoc.
    public partial class SpecificTestCases
    {
        [Fact]
        public void DocumentationTagsAreConvertedWhenEnabled()
        {
            const string result = @"
module Reinforced.Typings.Tests.SpecificCases {
	/** Handles a collection of {@link Reinforced.Typings.Tests.SpecificCases.IDocTagItem}s and DocTagHidden; see items, `Count` and null. */
	export interface IDocTagBar
	{
		/** The DocTagItem.X values of {@link Reinforced.Typings.Tests.SpecificCases.IDocTagItem the items}. */
		Values: number[];
	}
	/** One item. */
	export interface IDocTagItem
	{
		/** X. */
		X: number;
	}
}";
            var actual = AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().GenerateDocumentation().ConvertDocumentationTags());
                s.TryLookupDocumentationForAssembly(typeof(SpecificTestCases).Assembly);
                s.ExportAsInterface<DocTagBar>().WithPublicProperties();
                s.ExportAsInterface<DocTagItem>().WithPublicProperties();
            }, result, compareComments: true);
            Assert.DoesNotContain("<see", actual);
        }

        [Fact]
        public void DocumentationTagsAreCopiedByDefault()
        {
            var data = InitializeSingleFile(s =>
            {
                s.Global(a => a.DontWriteWarningComment().GenerateDocumentation());
                s.TryLookupDocumentationForAssembly(typeof(SpecificTestCases).Assembly);
                s.ExportAsInterface<DocTagBar>().WithPublicProperties();
            });
            data.Exporter.Export();
            Assert.Contains("<see cref=\"T:Reinforced.Typings.Tests.SpecificCases.DocTagItem\" />", data.Files.ExportedFiles[Sample]);
        }

        [Fact]
        public void DocumentationTextConverterCases()
        {
            System.Func<string, string> resolve = c => c == "T:A.B" ? "A.IB" : null;
            Assert.Equal("see {@link A.IB}", DocumentationTextConverter.Convert("see <see cref=\"T:A.B\"/>", resolve));
            Assert.Equal("see {@link A.IB text}", DocumentationTextConverter.Convert("see <see cref=\"T:A.B\">text</see>", resolve));
            Assert.Equal("Run", DocumentationTextConverter.Convert("<see cref=\"M:A.C.Run(System.Int32)\"/>", resolve).Replace("C.", string.Empty));
            Assert.Equal("List", DocumentationTextConverter.Convert("<see cref=\"T:System.Collections.Generic.List`1\"/>", resolve));
            Assert.Equal("{@link https://x.y docs}", DocumentationTextConverter.Convert("<see href=\"https://x.y\">docs</see>", resolve));
            Assert.Equal("a < b & `c`", DocumentationTextConverter.Convert("a &lt; b &amp; <c>c</c>", resolve));
            Assert.Equal("T and x", DocumentationTextConverter.Convert("<typeparamref name=\"T\"/> and <paramref name=\"x\"/>", resolve));
            // Not well-formed: returned unchanged.
            Assert.Equal("a <b", DocumentationTextConverter.Convert("a <b", resolve));
        }
    }
}
