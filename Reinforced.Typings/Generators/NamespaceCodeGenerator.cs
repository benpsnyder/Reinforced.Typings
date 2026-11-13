using System;
using System.Collections.Generic;
using Reinforced.Typings.Ast;

namespace Reinforced.Typings.Generators
{
    /// <summary>
    ///     Default code generator for namespace
    /// </summary>
    public class NamespaceCodeGenerator
    {
        /// <summary>
        ///     Export settings
        /// </summary>
        public ExportContext Context { get; set; }

        /// <summary>
        ///     Generates namespace source code
        /// </summary>
        /// <param name="types">Types list</param>
        /// <param name="namespaceName">Namespace name</param>
        /// <param name="resolver">Type resolver</param>
        /// <summary>
        /// Generates the discriminated union type alias for a System.Text.Json polymorphic base type
        /// </summary>
        /// <param name="type">Base type</param>
        /// <param name="resolver">Type resolver</param>
        /// <param name="export">Whether the alias is exported</param>
        /// <returns>Union node, or null when the type declares no derived types</returns>
        protected virtual RtDiscriminatedUnion GeneratePolymorphicUnion(Type type, TypeResolver resolver, bool export)
        {
            var derived = SystemTextJson.GetDerivedTypes(type);
            if (derived.Count == 0) return null;
            var members = new List<RtDiscriminatedUnionMember>();
            foreach (var d in derived)
            {
                var bp = Context.Project.Blueprint(d.DerivedType, false);
                if (bp == null || (bp.TypeAttribute == null && bp.ThirdParty == null))
                {
                    Context.AddWarning(Exceptions.ErrorMessages.RTW0015_DerivedTypeNotExported.Warn(d.DerivedType.FullName, type.FullName));
                    continue;
                }
                members.Add(new RtDiscriminatedUnionMember(d.DerivedType, resolver.ResolveTypeName(d.DerivedType), d.Discriminator));
            }
            var name = Context.Project.Blueprint(type).GetName().TypeName + "Union";
            var result = new RtDiscriminatedUnion(name, SystemTextJson.GetDiscriminatorPropertyName(type), members, export);
            result.Order = (Context.Project.Blueprint(type).GetOrder());
            return result;
        }

        public virtual RtNamespace Generate(IEnumerable<Type> types, string namespaceName, TypeResolver resolver)
        {
            RtNamespace ns = new RtNamespace();
            var needToDiscard = Context.Global.UseModules && Context.Global.DiscardNamespacesWhenUsingModules;

            if (string.IsNullOrEmpty(namespaceName) || needToDiscard) ns.IsAmbientNamespace = true;
            ns.Name = namespaceName;

            Context.Location.SetLocation(ns);
            foreach (var type in types)
            {
                var typeGenerator = Context.Generators.GeneratorFor(type);
                if (typeGenerator == null) continue;
                var member = typeGenerator.Generate(type, resolver);
                var m = member as RtCompilationUnit;
                if (m != null)
                {
                    if (Context.Global.UseModules)
                    {
                        m.Export = true;
                    }
                    else
                    {
                        m.Export = !ns.IsAmbientNamespace;
                    }
                }


                if (member!=null) ns.CompilationUnits.Add(member);
                if (member != null && Context.Global.UseSystemTextJson)
                {
                    var union = GeneratePolymorphicUnion(type, resolver, Context.Global.UseModules || !ns.IsAmbientNamespace);
                    if (union != null) ns.CompilationUnits.Add(union);
                }
#if DEBUG
                Console.WriteLine("Exported {0}", type);
#endif
            }

            if (Context.Global.UseModules) ns.GenerationMode = NamespaceGenerationMode.Namespace;

            Context.Location.ResetLocation(ns);
            return ns;
        }
    }
}