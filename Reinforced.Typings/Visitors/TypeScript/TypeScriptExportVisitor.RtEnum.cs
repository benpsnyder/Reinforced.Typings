using System.Linq;
using Reinforced.Typings.Ast;
#pragma warning disable 1591
namespace Reinforced.Typings.Visitors.TypeScript
{
    partial class TypeScriptExportVisitor
    {
        /// <summary>
        /// Writes "type Name = v1 | v2 ...;" for an enum emitted as a union type
        /// </summary>
        protected void WriteEnumUnion(RtEnum node)
        {
            Write("type ");
            Visit(node.EnumName);
            Write(" = ");
            var values = node.Values.Select(v => v.EnumValue).Distinct().ToArray();
            Write(values.Length == 0 ? "never" : string.Join(" | ", values));
            WriteLine(";");
        }

        public override void Visit(RtEnum node)
        {
            if (node == null) return;
            Visit(node.Documentation);
            var prev = Context;
            Context = WriterContext.Enum;
            AppendTabs();
            if (node.IsUnionType)
            {
                if (node.Export) Write("export ");
                WriteEnumUnion(node);
                Context = prev;
                return;
            }
            if (node.Export) Write("export ");
            if (node.IsConst) Write("const ");
            Write("enum ");
            Visit(node.EnumName);
            WriteLine(" {");
            Tab();
            var arr = node.Values.ToArray();
            for (int i = 0; i < arr.Length; i++)
            {
                Visit(arr[i]);
                if (i != arr.Length - 1) WriteLine(",");
                if (!string.IsNullOrEmpty(arr[i].LineAfter))
                {
                    AppendTabs();
                    Write(arr[i].LineAfter);
                    Br();
                }
            }
            WriteLine(string.Empty);
            UnTab();
            AppendTabs();
            WriteLine("}");
            Context = prev;
        }
    }
}