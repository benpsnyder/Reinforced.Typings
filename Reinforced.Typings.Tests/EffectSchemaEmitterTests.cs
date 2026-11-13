using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Reinforced.Typings.Fluent;
using Reinforced.Typings.Tests.Core;
using Reinforced.Typings.Tests.SpecificCases;
using Xunit;

namespace Reinforced.Typings.Tests
{
    public class EffectSchemaEmitterTests
    {
        public enum EsColor { Red = 1, Green = 2 }

        public class EsBase
        {
            public string Name { get; set; }
        }

        public class EsDto : EsBase
        {
            public int? Count { get; set; }
            public List<string> Tags { get; set; }
            public Dictionary<string, double> Scores { get; set; }
            public EsColor Color { get; set; }
            public EsBase Parent { get; set; }
        }

        public class EsGeneric<T>
        {
            public T Value { get; set; }
        }

        public class EsUsesGeneric
        {
            public EsGeneric<int> Inner { get; set; }
        }

        public class EsNode
        {
            public EsNode Next { get; set; }
        }

        public class EsMethodsOnly
        {
            public void Run() { }
        }

        private const string Header = "import { Schema } from \"effect\";\n";

        private static (string Schema, ExportContext Context) Emit(Action<ConfigurationBuilder> configure)
        {
            var mfo = new MockFileOperations();
            var ec = new ExportContext(new Assembly[] { Assembly.GetExecutingAssembly() }, mfo)
            {
                Hierarchical = false,
                TargetFile = "target.ts",
                EffectSchemaFile = "schema.ts",
                ConfigurationMethod = b =>
                {
                    b.Global(g => g.DontWriteWarningComment().NewLine("\n").TabSymbol("  ").CamelCaseForProperties());
                    configure(b);
                }
            };
            new TsExporter(ec).Export();
            return (mfo.ExportedFiles["schema.ts"], ec);
        }

        private static string Block(string name, string body)
        {
            return "\nexport const " + name + " = " + body + ";\n" +
                   "export type " + name + " = typeof " + name + ".Type;\n" +
                   "export type " + name + "Encoded = typeof " + name + ".Encoded;\n";
        }

        [Fact]
        public void NotEmittedUnlessConfigured()
        {
            var mfo = new MockFileOperations();
            var ec = new ExportContext(new Assembly[] { Assembly.GetExecutingAssembly() }, mfo)
            {
                Hierarchical = false,
                TargetFile = "target.ts",
                ConfigurationMethod = b => b.ExportAsInterface<EsBase>().WithPublicProperties()
            };
            new TsExporter(ec).Export();
            Assert.Equal(new[] { "target.ts" }, mfo.ExportedFiles.Keys.ToArray());
        }

        [Fact]
        public void StructsEnumsAndInheritance()
        {
            var (schema, _) = Emit(b =>
            {
                b.Global(g => g.AutoOptionalProperties());
                b.ExportAsEnum<EsColor>();
                b.ExportAsInterface<EsBase>().WithPublicProperties();
                b.ExportAsInterface<EsDto>().WithPublicProperties();
            });
            var expected = Header
                + Block("EsColor", "Schema.Literals([1, 2])")
                + Block("EsBase", "Schema.Struct({\n  name: Schema.String\n})")
                + Block("EsDto", "Schema.Struct({\n  ...EsBase.fields,\n  count: Schema.optionalKey(Schema.Number),\n  tags: Schema.Array(Schema.String),\n  scores: Schema.Record(Schema.String, Schema.Number),\n  color: EsColor,\n  parent: EsBase\n})");
            Assert.Equal(expected, schema);
        }

        [Fact]
        public void StrictNullabilityUsesNullOr()
        {
            var (schema, _) = Emit(b =>
            {
                b.Global(g => g.StrictNullability());
                b.ExportAsInterface<ModernDto>().WithPublicProperties();
            });
            Assert.Contains("count: Schema.NullOr(Schema.Number)", schema);
            Assert.Contains("total: Schema.Number", schema);
        }

        [Fact]
        public void DiscriminatedUnionFromSystemTextJson()
        {
            var (schema, _) = Emit(b =>
            {
                b.Global(g => g.UseSystemTextJson());
                b.ExportAsInterface<StjShape>().WithPublicProperties();
                b.ExportAsInterface<StjCircle>().WithPublicProperties();
                b.ExportAsInterface<StjSquare>().WithPublicProperties();
            });
            Assert.Contains(Block("StjShapeUnion",
                "Schema.Union([Schema.Struct({ ...StjCircle.fields, kind: Schema.Literal(\"circle\") }), Schema.Struct({ ...StjSquare.fields, kind: Schema.Literal(2) })])"), schema);
            // The union comes after the members it spreads.
            Assert.True(schema.IndexOf("export const StjShapeUnion", StringComparison.Ordinal) > schema.IndexOf("export const StjSquare", StringComparison.Ordinal));
        }

        [Fact]
        public void GenericAndRecursiveTypesAreSkippedWithWarnings()
        {
            var (schema, ec) = Emit(b =>
            {
                b.ExportAsInterfaces(new[] { typeof(EsGeneric<>) }, c => c.WithPublicProperties());
                b.ExportAsInterface<EsUsesGeneric>().WithPublicProperties();
                b.ExportAsInterface<EsNode>().WithPublicProperties();
                b.ExportAsInterface<EsBase>().WithPublicProperties();
            });
            Assert.DoesNotContain("EsGeneric", schema);
            Assert.DoesNotContain("EsNode", schema);
            Assert.Contains("export const EsBase", schema);
            var codes = ec.Warnings.Select(w => w.Code).ToList();
            Assert.Contains(17, codes);
            Assert.Contains(16, codes); // EsUsesGeneric.inner has no schema
        }

        [Fact]
        public void ClassesWithoutPropertiesAreNotDtos()
        {
            var (schema, _) = Emit(b =>
            {
                b.ExportAsClass<EsMethodsOnly>().WithPublicMethods();
            });
            Assert.Equal(Header, schema);
        }
    }
}
