using System;
using System.IO;
using System.Text;
using System.Xml;

namespace Reinforced.Typings.Xmldoc
{
    /// <summary>
    /// Converts the inline XML of a documentation comment (summary, remarks, param, returns text) to
    /// JSDoc text: <c>&lt;see cref="T:X"/&gt;</c> becomes <c>{@link X}</c> when the resolver maps the
    /// cref to an exported TypeScript type and plain text otherwise; <c>see langword</c>,
    /// <c>paramref</c> and <c>typeparamref</c> become their names; <c>c</c> and <c>code</c> become
    /// backtick code; <c>see href</c> becomes <c>{@link url text}</c>; other tags keep their text.
    /// Text that is not well-formed XML is returned unchanged.
    /// </summary>
    public static class DocumentationTextConverter
    {
        /// <summary>
        /// Converts documentation XML text
        /// </summary>
        /// <param name="xml">Inner XML of a documentation element</param>
        /// <param name="resolveCref">Maps a cref (for example "T:Foo.Bar") to a TypeScript type name for {@link}, or null when the target is not exported</param>
        public static string Convert(string xml, Func<string, string> resolveCref)
        {
            if (string.IsNullOrEmpty(xml) || xml.IndexOf('<') < 0 && xml.IndexOf('&') < 0) return xml;
            var sb = new StringBuilder();
            try
            {
                var settings = new XmlReaderSettings { ConformanceLevel = ConformanceLevel.Fragment, DtdProcessing = DtdProcessing.Prohibit };
                using (var reader = XmlReader.Create(new StringReader(xml), settings))
                {
                    while (reader.Read()) Append(reader, sb, resolveCref);
                }
            }
            catch (XmlException)
            {
                return xml;
            }
            return sb.ToString();
        }

        private static void Append(XmlReader reader, StringBuilder sb, Func<string, string> resolveCref)
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    sb.Append(reader.Value);
                    break;
                case XmlNodeType.Element:
                    AppendElement(reader, sb, resolveCref);
                    break;
            }
        }

        private static void AppendElement(XmlReader reader, StringBuilder sb, Func<string, string> resolveCref)
        {
            var name = reader.LocalName;
            var isEmpty = reader.IsEmptyElement;
            var cref = reader.GetAttribute("cref");
            var href = reader.GetAttribute("href");
            var langword = reader.GetAttribute("langword");
            var nameAttr = reader.GetAttribute("name");
            var inner = isEmpty ? string.Empty : ReadInner(reader, resolveCref);

            switch (name)
            {
                case "see":
                case "seealso":
                    if (!string.IsNullOrEmpty(cref))
                    {
                        var target = resolveCref == null ? null : resolveCref(cref);
                        var label = inner.Trim();
                        if (target != null) sb.Append(label.Length > 0 ? "{@link " + target + " " + label + "}" : "{@link " + target + "}");
                        else sb.Append(label.Length > 0 ? label : PlainCref(cref));
                    }
                    else if (!string.IsNullOrEmpty(href))
                    {
                        var label = inner.Trim();
                        sb.Append(label.Length > 0 ? "{@link " + href + " " + label + "}" : "{@link " + href + "}");
                    }
                    else if (!string.IsNullOrEmpty(langword))
                    {
                        sb.Append(langword);
                    }
                    else
                    {
                        sb.Append(inner);
                    }
                    break;
                case "paramref":
                case "typeparamref":
                    sb.Append(nameAttr ?? inner);
                    break;
                case "c":
                case "code":
                    sb.Append('`').Append(inner.Trim()).Append('`');
                    break;
                default:
                    sb.Append(inner);
                    break;
            }
        }

        private static string ReadInner(XmlReader reader, Func<string, string> resolveCref)
        {
            var sb = new StringBuilder();
            var depth = reader.Depth;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth) break;
                Append(reader, sb, resolveCref);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Plain-text form of a cref: "T:Foo.Bar" gives "Bar", "M:Foo.Bar.Run(System.Int32)" gives "Bar.Run",
        /// "P:Foo.Bar.Name" gives "Bar.Name"
        /// </summary>
        public static string PlainCref(string cref)
        {
            if (string.IsNullOrEmpty(cref)) return cref;
            var kind = cref.Length > 2 && cref[1] == ':' ? cref[0] : 'T';
            var body = cref.Length > 2 && cref[1] == ':' ? cref.Substring(2) : cref;
            var paren = body.IndexOf('(');
            if (paren >= 0) body = body.Substring(0, paren);
            body = StripArity(body);
            var parts = body.Split('.');
            if (kind == 'T' || kind == 'N' || parts.Length < 2) return parts[parts.Length - 1];
            return parts[parts.Length - 2] + "." + parts[parts.Length - 1];
        }

        private static string StripArity(string name)
        {
            var sb = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                if (name[i] == '`')
                {
                    while (i + 1 < name.Length && (name[i + 1] == '`' || char.IsDigit(name[i + 1]))) i++;
                    continue;
                }
                sb.Append(name[i]);
            }
            return sb.ToString();
        }
    }
}
