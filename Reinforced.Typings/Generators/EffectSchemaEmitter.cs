using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Reinforced.Typings.Ast;
using Reinforced.Typings.Ast.TypeNames;
using Reinforced.Typings.Exceptions;

namespace Reinforced.Typings.Generators
{
    /// <summary>
    /// Emits an ES module of Effect <c>Schema</c> definitions (effect 4.x, <c>import { Schema } from "effect"</c>)
    /// for the DTOs of an export: interfaces, classes with properties, enums and System.Text.Json
    /// discriminated unions. It works from the same AST as the TypeScript output, so names,
    /// optionality, nullability and substitutions match the generated types.
    /// </summary>
    /// <remarks>
    /// Per exported DTO <c>X</c> it writes <c>export const X = Schema.Struct({...})</c> plus
    /// <c>export type X = typeof X.Type</c> and <c>export type XEncoded = typeof X.Encoded</c>.
    /// Generic types and types in a reference cycle are not emitted (warning RTW0017);
    /// TypeScript types with no schema equivalent become <c>Schema.Unknown</c> (warning RTW0016).
    /// </remarks>
    public class EffectSchemaEmitter
    {
        private readonly ExportContext _context;
        private readonly Dictionary<string, Decl> _byFullName = new Dictionary<string, Decl>();
        private readonly Dictionary<string, List<Decl>> _byShortName = new Dictionary<string, List<Decl>>();
        private readonly List<Decl> _decls = new List<Decl>();

        private enum DeclKind
        {
            Struct,
            Enum,
            Union
        }

        private sealed class Decl
        {
            public DeclKind Kind;
            public string TsFullName;
            public string TsName;
            public string ConstName;
            public RtNode Node;
            public string Skip;
            public readonly HashSet<Decl> Dependencies = new HashSet<Decl>();
            public string Body;
            public string Doc;
        }

        /// <summary>
        /// Creates the emitter for an export context
        /// </summary>
        public EffectSchemaEmitter(ExportContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Builds the module text for the given namespaces (from every exported file)
        /// </summary>
        public string Emit(IEnumerable<RtNamespace> namespaces)
        {
            foreach (var ns in namespaces) Collect(ns);
            AssignConstNames();
            foreach (var d in _decls.Where(d => d.Skip == null)) Build(d);
            PropagateSkips();
            return Render(Order());
        }

        private void Collect(RtNamespace ns)
        {
            var prefix = ns.IsAmbientNamespace || _context.Global.UseModules && _context.Global.DiscardNamespacesWhenUsingModules
                ? string.Empty
                : ns.Name ?? string.Empty;
            foreach (var unit in ns.CompilationUnits)
            {
                Decl d = null;
                if (unit is RtInterface i)
                {
                    d = new Decl { Kind = DeclKind.Struct, TsName = i.Name.TypeName, Node = unit, Doc = i.Documentation?.Description };
                    if (i.Name.GenericArguments.Length > 0) d.Skip = "generic types are not supported";
                }
                else if (unit is RtClass c)
                {
                    if (!c.Members.OfType<RtField>().Any(f => !f.IsStatic)) continue; // not a DTO
                    d = new Decl { Kind = DeclKind.Struct, TsName = c.Name.TypeName, Node = unit, Doc = c.Documentation?.Description };
                    if (c.Name.GenericArguments.Length > 0) d.Skip = "generic types are not supported";
                }
                else if (unit is RtEnum e)
                {
                    d = new Decl { Kind = DeclKind.Enum, TsName = e.EnumName.TypeName, Node = unit, Doc = e.Documentation?.Description };
                }
                else if (unit is RtDiscriminatedUnion u)
                {
                    d = new Decl { Kind = DeclKind.Union, TsName = u.Name, Node = unit };
                }
                if (d == null) continue;
                d.TsFullName = string.IsNullOrEmpty(prefix) ? d.TsName : prefix + "." + d.TsName;
                if (_byFullName.ContainsKey(d.TsFullName)) continue;
                _byFullName[d.TsFullName] = d;
                if (!_byShortName.TryGetValue(d.TsName, out var list)) _byShortName[d.TsName] = list = new List<Decl>();
                list.Add(d);
                _decls.Add(d);
            }
        }

        private static string StripInterfacePrefix(string name)
        {
            return name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]) ? name.Substring(1) : name;
        }

        private void AssignConstNames()
        {
            var used = new Dictionary<string, Decl>(StringComparer.Ordinal);
            foreach (var d in _decls)
            {
                var name = d.Kind == DeclKind.Enum ? d.TsName : StripInterfacePrefix(d.TsName);
                if (name == "Schema") name = "SchemaDto";
                if (used.ContainsKey(name))
                {
                    if (d.Skip == null) d.Skip = "its schema name '" + name + "' is already used by " + used[name].TsFullName;
                    continue;
                }
                used[name] = d;
                d.ConstName = name;
            }
        }

        private Decl Resolve(RtSimpleTypeName t)
        {
            if (t.HasPrefix)
            {
                return _byFullName.TryGetValue(t.Prefix + "." + t.TypeName, out var d) ? d : null;
            }
            if (_byFullName.TryGetValue(t.TypeName, out var bare)) return bare;
            return _byShortName.TryGetValue(t.TypeName, out var list) && list.Count == 1 ? list[0] : null;
        }

        private string Schema(RtTypeName type, Decl owner)
        {
            switch (type)
            {
                case RtArrayType a:
                    return "Schema.Array(" + Schema(a.ElementType, owner) + ")";
                case RtTuple tuple:
                    return "Schema.Tuple([" + string.Join(", ", tuple.TupleTypes.Select(x => Schema(x, owner))) + "])";
                case RtDictionaryType dict:
                    var key = dict.KeyType as RtSimpleTypeName;
                    if (!dict.IsKeyEnum && key != null && !key.HasPrefix && (key.TypeName == "string" || key.TypeName == "number"))
                    {
                        return "Schema.Record(" + (key.TypeName == "string" ? "Schema.String" : "Schema.Number") + ", " + Schema(dict.ValueType, owner) + ")";
                    }
                    return Unknown(type, owner);
                case RtSimpleTypeName s:
                    if (!s.HasPrefix && s.GenericArguments.Length == 0)
                    {
                        switch (s.TypeName)
                        {
                            case "string": return "Schema.String";
                            case "number": return "Schema.Number";
                            case "boolean": return "Schema.Boolean";
                            case "any":
                            case "unknown": return "Schema.Unknown";
                            case "null": return "Schema.Null";
                        }
                    }
                    if (s.GenericArguments.Length == 0)
                    {
                        var target = Resolve(s);
                        if (target != null && target.ConstName != null)
                        {
                            owner.Dependencies.Add(target);
                            return target.ConstName;
                        }
                        if (target != null) owner.Dependencies.Add(target);
                    }
                    return Unknown(type, owner);
                default:
                    return Unknown(type, owner);
            }
        }

        private string Unknown(RtTypeName type, Decl owner)
        {
            _context.AddWarning(ErrorMessages.RTW0016_EffectSchemaUnknownType.Warn(type, owner.TsFullName));
            return "Schema.Unknown";
        }

        private static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do", "else",
            "export", "extends", "false", "finally", "for", "function", "if", "import", "in", "instanceof", "new",
            "null", "return", "super", "switch", "this", "throw", "true", "try", "typeof", "var", "void", "while", "with"
        };

        private static string Key(string name)
        {
            var ok = name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_' || name[0] == '$')
                     && name.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '$');
            return ok ? name : RtDiscriminatedUnion.Quote(name);
        }

        private IEnumerable<RtField> OrderedFields(List<RtNode> members)
        {
            var fields = members.OfType<RtField>().Where(f => !f.IsStatic).ToList();
            if (_context.Global.ReorderMembers) return fields.OrderBy(f => f.Identifier.IdentifierName, StringComparer.Ordinal);
            if (fields.Any(f => f.Order != 0)) return fields.OrderBy(f => f.Order);
            return fields;
        }

        private void Build(Decl d)
        {
            switch (d.Kind)
            {
                case DeclKind.Enum:
                    var values = ((RtEnum)d.Node).Values.Select(v => v.EnumValue).Distinct().ToList();
                    d.Body = values.Count == 0 ? "Schema.Never" : "Schema.Literals([" + string.Join(", ", values) + "])";
                    break;
                case DeclKind.Union:
                    var u = (RtDiscriminatedUnion)d.Node;
                    var members = new List<string>();
                    foreach (var m in u.Members)
                    {
                        var target = m.Type is RtSimpleTypeName sn ? Resolve(sn) : null;
                        if (target == null || target.Kind != DeclKind.Struct || target.ConstName == null)
                        {
                            d.Skip = "union member " + m.Type + " has no schema";
                            return;
                        }
                        d.Dependencies.Add(target);
                        members.Add(m.Discriminator == null
                            ? target.ConstName
                            : "Schema.Struct({ ..." + target.ConstName + ".fields, " + Key(u.DiscriminatorProperty) + ": Schema.Literal(" + RtDiscriminatedUnion.Literal(m.Discriminator) + ") })");
                    }
                    d.Body = members.Count == 0 ? "Schema.Never" : "Schema.Union([" + string.Join(", ", members) + "])";
                    break;
                default:
                    BuildStruct(d);
                    break;
            }
        }

        private void BuildStruct(Decl d)
        {
            var bases = new List<RtTypeName>();
            List<RtNode> members;
            if (d.Node is RtInterface i)
            {
                bases.AddRange(i.Implementees);
                members = i.Members;
            }
            else
            {
                var c = (RtClass)d.Node;
                if (c.Extendee != null) bases.Add(c.Extendee);
                bases.AddRange(c.Implementees);
                members = c.Members;
            }

            var nl = _context.Global.NewLine;
            var tab = _context.Global.TabSymbol;
            var parts = new List<string>();
            foreach (var b in bases)
            {
                var target = b is RtSimpleTypeName sb && sb.GenericArguments.Length == 0 ? Resolve(sb) : null;
                if (target == null || target.Kind != DeclKind.Struct || target.ConstName == null)
                {
                    d.Skip = "base type " + b + " has no schema";
                    return;
                }
                d.Dependencies.Add(target);
                parts.Add(tab + "..." + target.ConstName + ".fields");
            }
            foreach (var f in OrderedFields(members))
            {
                var schema = Schema(f.Type, d);
                if (f.IsNullableType) schema = "Schema.NullOr(" + schema + ")";
                if (f.Identifier.IsNullable) schema = "Schema.optionalKey(" + schema + ")";
                parts.Add(tab + Key(f.Identifier.IdentifierName) + ": " + schema);
            }
            d.Body = parts.Count == 0 ? "Schema.Struct({})" : "Schema.Struct({" + nl + string.Join("," + nl, parts) + nl + "})";
        }

        private void PropagateSkips()
        {
            bool changed;
            do
            {
                changed = false;
                foreach (var d in _decls.Where(x => x.Skip == null))
                {
                    var bad = d.Dependencies.FirstOrDefault(x => x.Skip != null);
                    if (bad != null)
                    {
                        d.Skip = "it depends on " + bad.TsFullName + ", which is not emitted";
                        changed = true;
                    }
                }
            } while (changed);
        }

        private List<Decl> Order()
        {
            var result = new List<Decl>();
            var state = new Dictionary<Decl, int>();
            foreach (var d in _decls) Visit(d, state, result, new Stack<Decl>());
            return result;
        }

        private void Visit(Decl d, Dictionary<Decl, int> state, List<Decl> result, Stack<Decl> path)
        {
            if (d.Skip != null) return;
            state.TryGetValue(d, out var s);
            if (s == 2) return;
            if (s == 1)
            {
                // Reference cycle: Effect schemas would need Schema.suspend with hand-written types.
                foreach (var member in path.TakeWhile(x => x != d).Concat(new[] { d }))
                {
                    if (member.Skip == null) member.Skip = "it is part of a reference cycle (recursive types are not supported)";
                }
                return;
            }
            state[d] = 1;
            path.Push(d);
            foreach (var dep in d.Dependencies.OrderBy(x => _decls.IndexOf(x))) Visit(dep, state, result, path);
            path.Pop();
            state[d] = 2;
            if (d.Skip == null) result.Add(d);
        }

        private string Render(List<Decl> ordered)
        {
            // A cycle found while ordering can invalidate types that were already placed.
            PropagateSkips();
            ordered = ordered.Where(d => d.Skip == null).ToList();
            foreach (var d in _decls.Where(x => x.Skip != null))
            {
                _context.AddWarning(ErrorMessages.RTW0017_EffectSchemaTypeSkipped.Warn(d.TsFullName, d.Skip));
            }

            var nl = _context.Global.NewLine;
            var sb = new StringBuilder();
            if (_context.Global.WriteWarningComment)
            {
                sb.Append("//     This code was generated by a Reinforced.Typings tool.").Append(nl);
                sb.Append("//     Changes to this file may cause incorrect behavior and will be lost if").Append(nl);
                sb.Append("//     the code is regenerated.").Append(nl).Append(nl);
            }
            sb.Append("import { Schema } from \"effect\";").Append(nl);
            foreach (var d in ordered)
            {
                sb.Append(nl);
                if (_context.Global.GenerateDocumentation && !string.IsNullOrEmpty(d.Doc))
                {
                    sb.Append("/** ").Append(d.Doc.Replace("*/", "* /").Replace("\r", string.Empty).Replace("\n", " ")).Append(" */").Append(nl);
                }
                sb.Append("export const ").Append(d.ConstName).Append(" = ").Append(d.Body).Append(";").Append(nl);
                sb.Append("export type ").Append(d.ConstName).Append(" = typeof ").Append(d.ConstName).Append(".Type;").Append(nl);
                sb.Append("export type ").Append(d.ConstName).Append("Encoded = typeof ").Append(d.ConstName).Append(".Encoded;").Append(nl);
            }
            return sb.ToString();
        }
    }
}
