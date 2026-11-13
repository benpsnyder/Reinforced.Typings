using System;
using System.Linq;
using Reinforced.Typings.Ast;
using Reinforced.Typings.Xmldoc;
#pragma warning disable 1591
namespace Reinforced.Typings.Visitors.TypeScript
{
    partial class TypeScriptExportVisitor
    {
        public override void Visit(RtJsdocNode node)
        {
            if (node == null) return;

            var description = node.Description;
            var tags = node.TagToDescription;
            if (ExportContext.Global.ConvertDocumentationTags)
            {
                description = ConvertDocumentation(description);
                tags = tags.Select(t => Tuple.Create(t.Item1, ConvertDocumentation(t.Item2))).ToList();
            }

            if (!string.IsNullOrEmpty(description) && !description.Contains("\n") && tags.Count == 0)
            {
                //handle single-line JSDOC
                AppendTabs(); Write("/** "); Write(description); WriteLine(" */");
            }
            else
            {
                AppendTabs(); WriteLine("/**");
                if (!string.IsNullOrEmpty(description))
                {
                    Summary(description);
                    if (tags.Count > 0) DocLine();
                }
                foreach (var tuple in tags)
                {
                    DocTag(tuple.Item1, tuple.Item2);
                }
                AppendTabs(); WriteLine("*/");
            }
        }

        /// <summary>
        /// Converts inline XML documentation tags (upstream issue #310, ConvertDocumentationTags)
        /// </summary>
        protected string ConvertDocumentation(string text)
        {
            return DocumentationTextConverter.Convert(text, ResolveCrefToTypeScript);
        }

        /// <summary>
        /// Maps a "T:" cref to the TypeScript name of the exported type, or null when it is not exported
        /// </summary>
        protected string ResolveCrefToTypeScript(string cref)
        {
            if (string.IsNullOrEmpty(cref) || !cref.StartsWith("T:", StringComparison.Ordinal)) return null;
            var type = FindType(cref.Substring(2));
            if (type == null) return null;
            var bp = ExportContext.Project.Blueprint(type, false);
            if (bp == null || bp.TypeAttribute == null) return null;
            var name = bp.GetName().TypeName;
            if (ExportContext.Global.UseModules) return name;
            var ns = bp.GetNamespace();
            return string.IsNullOrEmpty(ns) || ns == "-" ? name : ns + "." + name;
        }

        private Type FindType(string name)
        {
            // Nested types appear as "Outer.Inner" in crefs and "Outer+Inner" in reflection.
            var candidate = name;
            while (true)
            {
                foreach (var assembly in ExportContext.SourceAssemblies)
                {
                    var t = assembly.GetType(candidate, false);
                    if (t != null) return t;
                }
                var dot = candidate.LastIndexOf('.');
                if (dot < 0) return null;
                candidate = candidate.Substring(0, dot) + "+" + candidate.Substring(dot + 1);
            }
        }
    }
}
