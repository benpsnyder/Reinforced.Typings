using System;
using System.Linq;
using Xunit;

namespace Reinforced.Typings.Tests
{
    /// <summary>
    /// The NuGet package id is Reinforced.Typings.vNext, but the public API keeps the
    /// Reinforced.Typings assembly name and namespaces, so consumers' using directives are unchanged.
    /// </summary>
    public class PublicNamespaceTests
    {
        [Fact]
        public void PublicTypesStayInReinforcedTypingsNamespace()
        {
            var assembly = typeof(TsExporter).Assembly;
            Assert.Equal("Reinforced.Typings", assembly.GetName().Name);

            var offending = assembly.GetExportedTypes()
                .Where(t => t.Namespace == null
                            || !(t.Namespace == "Reinforced.Typings" || t.Namespace.StartsWith("Reinforced.Typings.", StringComparison.Ordinal))
                            || t.Namespace.IndexOf("vNext", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(t => t.FullName)
                .ToList();

            Assert.Empty(offending);
        }
    }
}
