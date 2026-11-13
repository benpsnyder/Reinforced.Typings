using Reinforced.Typings.Ast;
#pragma warning disable 1591
namespace Reinforced.Typings.Visitors.TypeScript
{
    partial class TypeScriptExportVisitor
    {
        public override void Visit(RtField node)
        {
            if (node == null) return;
            Visit(node.Documentation);
            AppendTabs();
            if (Context != WriterContext.Interface)
            {
                Decorators(node);
                Modifiers(node);
            }
            if (node.IsReadonly) Write("readonly ");
            Visit(node.Identifier);
            Write(": ");
            Visit(node.Type);
            if (node.IsNullableType) Write(" | null");
            if (!string.IsNullOrEmpty(node.InitializationExpression))
            {
                Write(" = ");
                Write(node.InitializationExpression);
            }
            Write(";");
            Br();
            if (!string.IsNullOrEmpty(node.LineAfter))
            {
                AppendTabs();
                Write(node.LineAfter);
                Br();
            }
        }
    }
}
