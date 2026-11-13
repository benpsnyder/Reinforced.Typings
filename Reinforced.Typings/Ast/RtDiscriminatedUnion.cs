using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Reinforced.Typings.Ast.TypeNames;

namespace Reinforced.Typings.Ast
{
    /// <summary>
    /// A union type alias generated from System.Text.Json polymorphism ([JsonPolymorphic]/[JsonDerivedType]).
    /// It is written as raw TypeScript, so existing visitors render it without changes.
    /// </summary>
    public class RtDiscriminatedUnion : RtRaw
    {
        /// <summary>
        /// Creates the union node and renders its TypeScript text
        /// </summary>
        /// <param name="name">Union type alias name</param>
        /// <param name="discriminatorProperty">Discriminator property name (System.Text.Json default "$type")</param>
        /// <param name="members">Union members</param>
        /// <param name="export">Whether to prefix the alias with "export"</param>
        public RtDiscriminatedUnion(string name, string discriminatorProperty, IEnumerable<RtDiscriminatedUnionMember> members, bool export)
        {
            Name = name;
            DiscriminatorProperty = discriminatorProperty;
            Members = members.ToList();
            Export = export;
            RawContent = Render();
        }

        /// <summary>Union type alias name</summary>
        public string Name { get; private set; }

        /// <summary>Discriminator property name</summary>
        public string DiscriminatorProperty { get; private set; }

        /// <summary>Union members in declaration order</summary>
        public List<RtDiscriminatedUnionMember> Members { get; private set; }

        /// <summary>Whether the alias is exported</summary>
        public bool Export { get; private set; }

        /// <summary>Renders a discriminator value as a TypeScript/JavaScript literal</summary>
        public static string Literal(object value)
        {
            if (value is string s) return Quote(s);
            return System.Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Renders a string as a double-quoted JSON/TypeScript string literal</summary>
        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        private string Render()
        {
            var parts = Members.Select(m => m.Discriminator == null
                ? "(" + m.Type + ")"
                : "({ " + Quote(DiscriminatorProperty) + ": " + Literal(m.Discriminator) + " } & " + m.Type + ")");
            var body = Members.Count == 0 ? "never" : string.Join(" | ", parts);
            return (Export ? "export " : string.Empty) + "type " + Name + " = " + body + ";";
        }
    }

    /// <summary>
    /// One member of a <see cref="RtDiscriminatedUnion"/>
    /// </summary>
    public class RtDiscriminatedUnionMember
    {
        /// <summary>Creates a union member</summary>
        public RtDiscriminatedUnionMember(System.Type clrType, RtTypeName type, object discriminator)
        {
            ClrType = clrType;
            Type = type;
            Discriminator = discriminator;
        }

        /// <summary>CLR type of the member</summary>
        public System.Type ClrType { get; private set; }

        /// <summary>TypeScript type of the member</summary>
        public RtTypeName Type { get; private set; }

        /// <summary>Discriminator value (string or int), or null</summary>
        public object Discriminator { get; private set; }
    }
}
