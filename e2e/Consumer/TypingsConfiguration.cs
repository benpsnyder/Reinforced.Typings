using System;
using E2E.Consumer.Models;
using Reinforced.Typings.Ast.TypeNames;
using Reinforced.Typings.Fluent;

namespace E2E.Consumer
{
    /// <summary>Fluent configuration used by the end-to-end test (RtConfigurationMethod).</summary>
    public static class TypingsConfiguration
    {
        public static void Configure(ConfigurationBuilder builder)
        {
            builder.Global(g => g
                .NewLine("\n")
                .CamelCaseForProperties()
                .CamelCaseForMethods()
                .GenerateDocumentation()
                .AutoOptionalProperties());

            builder.Substitute(typeof(Guid), new RtSimpleTypeName("string"));
            builder.Substitute(typeof(DateTime), new RtSimpleTypeName("string"));
            builder.Substitute(typeof(DateTimeOffset), new RtSimpleTypeName("string"));

            builder.ExportAsInterface<OrderLine>().WithPublicProperties();
            builder.ExportAsInterface<Address>().WithPublicProperties();
            builder.ExportAsInterface<IAuditable>().WithPublicProperties();
            builder.ExportAsEnum<Currency>().UseString();
        }
    }
}
