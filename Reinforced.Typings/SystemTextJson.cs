using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Reinforced.Typings
{
    /// <summary>
    /// Property naming policies that mirror System.Text.Json's built-in JsonNamingPolicy values
    /// </summary>
    public enum JsonNamingPolicyKind
    {
        /// <summary>No policy: names are left to the other naming options</summary>
        None = 0,
        /// <summary>JsonNamingPolicy.CamelCase</summary>
        CamelCase,
        /// <summary>JsonNamingPolicy.SnakeCaseLower</summary>
        SnakeCaseLower,
        /// <summary>JsonNamingPolicy.SnakeCaseUpper</summary>
        SnakeCaseUpper,
        /// <summary>JsonNamingPolicy.KebabCaseLower</summary>
        KebabCaseLower,
        /// <summary>JsonNamingPolicy.KebabCaseUpper</summary>
        KebabCaseUpper
    }

    /// <summary>
    /// Value of System.Text.Json's JsonIgnoreCondition as seen by Reinforced.Typings
    /// </summary>
    public enum JsonIgnoreKind
    {
        /// <summary>No JsonIgnore attribute, or JsonIgnoreCondition.Never</summary>
        None = 0,
        /// <summary>JsonIgnoreCondition.Always: the member is never serialized</summary>
        Always,
        /// <summary>WhenWritingDefault or WhenWritingNull: the member may be absent from the payload</summary>
        WhenWriting
    }

    /// <summary>
    /// One derived type of a System.Text.Json polymorphic base type
    /// </summary>
    public sealed class JsonDerivedTypeInfo
    {
        internal JsonDerivedTypeInfo(Type derivedType, object discriminator)
        {
            DerivedType = derivedType;
            Discriminator = discriminator;
        }

        /// <summary>Derived type</summary>
        public Type DerivedType { get; private set; }

        /// <summary>Type discriminator (string or int), or null when the derived type has none</summary>
        public object Discriminator { get; private set; }
    }

    /// <summary>
    /// Reads System.Text.Json attributes by full name through <see cref="CustomAttributeData"/>,
    /// so no System.Text.Json reference is needed and .NET Framework consumers work unchanged.
    /// </summary>
    public static class SystemTextJson
    {
        private const string Ns = "System.Text.Json.Serialization.";

        private static IEnumerable<CustomAttributeData> Find(MemberInfo member, string name)
        {
            IList<CustomAttributeData> data;
            try
            {
                data = member.GetCustomAttributesData();
            }
            catch (Exception)
            {
                yield break;
            }
            foreach (var a in data)
            {
                if (a.AttributeType.FullName == Ns + name) yield return a;
            }
        }

        private static object Named(CustomAttributeData a, string name)
        {
            foreach (var na in a.NamedArguments)
            {
                if (na.MemberName == name) return na.TypedValue.Value;
            }
            return null;
        }

        /// <summary>Returns the JsonPropertyName of the member, or null</summary>
        public static string GetPropertyName(MemberInfo member)
        {
            var a = Find(member, "JsonPropertyNameAttribute").FirstOrDefault();
            if (a == null || a.ConstructorArguments.Count != 1) return null;
            return a.ConstructorArguments[0].Value as string;
        }

        /// <summary>Returns how JsonIgnore affects the member's shape</summary>
        public static JsonIgnoreKind GetIgnore(MemberInfo member)
        {
            var a = Find(member, "JsonIgnoreAttribute").FirstOrDefault();
            if (a == null) return JsonIgnoreKind.None;
            var condition = Named(a, "Condition");
            // JsonIgnoreCondition: Never = 0, Always = 1, WhenWritingDefault = 2, WhenWritingNull = 3, WhenWriting = 4, WhenReading = 5
            var value = condition == null ? 1 : Convert.ToInt32(condition, CultureInfo.InvariantCulture);
            switch (value)
            {
                case 0: return JsonIgnoreKind.None;
                case 1: return JsonIgnoreKind.Always;
                case 2:
                case 3:
                case 4: return JsonIgnoreKind.WhenWriting;
                default: return JsonIgnoreKind.None;
            }
        }

        /// <summary>Returns the JsonPropertyOrder of the member, or null</summary>
        public static int? GetOrder(MemberInfo member)
        {
            var a = Find(member, "JsonPropertyOrderAttribute").FirstOrDefault();
            if (a == null || a.ConstructorArguments.Count != 1) return null;
            return Convert.ToInt32(a.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>True when the type carries [JsonConverter(typeof(JsonStringEnumConverter))] or its generic form</summary>
        public static bool IsStringEnum(Type enumType)
        {
            foreach (var a in Find(enumType, "JsonConverterAttribute"))
            {
                if (a.ConstructorArguments.Count != 1) continue;
                var converter = a.ConstructorArguments[0].Value as Type;
                if (converter == null) continue;
                var name = converter.IsGenericType ? converter.GetGenericTypeDefinition().FullName : converter.FullName;
                if (name == Ns + "JsonStringEnumConverter" || name == Ns + "JsonStringEnumConverter`1") return true;
            }
            return false;
        }

        /// <summary>Returns the JsonStringEnumMemberName of an enum field, or null</summary>
        public static string GetEnumMemberName(FieldInfo field)
        {
            var a = Find(field, "JsonStringEnumMemberNameAttribute").FirstOrDefault();
            if (a == null || a.ConstructorArguments.Count != 1) return null;
            return a.ConstructorArguments[0].Value as string;
        }

        /// <summary>Returns the derived types declared with [JsonDerivedType] on the type, in declaration order</summary>
        public static List<JsonDerivedTypeInfo> GetDerivedTypes(Type baseType)
        {
            var result = new List<JsonDerivedTypeInfo>();
            foreach (var a in Find(baseType, "JsonDerivedTypeAttribute"))
            {
                if (a.ConstructorArguments.Count == 0) continue;
                var t = a.ConstructorArguments[0].Value as Type;
                if (t == null) continue;
                object discriminator = a.ConstructorArguments.Count > 1 ? a.ConstructorArguments[1].Value : null;
                result.Add(new JsonDerivedTypeInfo(t, discriminator));
            }
            return result;
        }

        /// <summary>Returns the discriminator property name of a polymorphic type ("$type" unless overridden)</summary>
        public static string GetDiscriminatorPropertyName(Type baseType)
        {
            var a = Find(baseType, "JsonPolymorphicAttribute").FirstOrDefault();
            var name = a == null ? null : Named(a, "TypeDiscriminatorPropertyName") as string;
            return string.IsNullOrEmpty(name) ? "$type" : name;
        }

        /// <summary>
        /// Converts a CLR member name with the given policy, following System.Text.Json's algorithms
        /// </summary>
        public static string ConvertName(JsonNamingPolicyKind policy, string name)
        {
            switch (policy)
            {
                case JsonNamingPolicyKind.CamelCase: return CamelCase(name);
                case JsonNamingPolicyKind.SnakeCaseLower: return Separated(name, '_', true);
                case JsonNamingPolicyKind.SnakeCaseUpper: return Separated(name, '_', false);
                case JsonNamingPolicyKind.KebabCaseLower: return Separated(name, '-', true);
                case JsonNamingPolicyKind.KebabCaseUpper: return Separated(name, '-', false);
                default: return name;
            }
        }

        private static string CamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0])) return name;
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (i == 1 && !char.IsUpper(chars[i])) break;
                var hasNext = i + 1 < chars.Length;
                if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
                {
                    if (chars[i + 1] == ' ') chars[i] = char.ToLowerInvariant(chars[i]);
                    break;
                }
                chars[i] = char.ToLowerInvariant(chars[i]);
            }
            return new string(chars);
        }

        private enum SeparatorState
        {
            NotStarted,
            UppercaseLetter,
            LowercaseLetterOrDigit,
            SpaceSeparator
        }

        private static string Separated(string name, char separator, bool lowercase)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new StringBuilder(name.Length + 8);
            var state = SeparatorState.NotStarted;
            for (var i = 0; i < name.Length; i++)
            {
                var current = name[i];
                var category = char.GetUnicodeCategory(current);
                switch (category)
                {
                    case UnicodeCategory.UppercaseLetter:
                        switch (state)
                        {
                            case SeparatorState.NotStarted:
                                break;
                            case SeparatorState.LowercaseLetterOrDigit:
                            case SeparatorState.SpaceSeparator:
                                sb.Append(separator);
                                break;
                            case SeparatorState.UppercaseLetter:
                                if (i + 1 < name.Length && char.IsLower(name[i + 1])) sb.Append(separator);
                                break;
                        }
                        if (lowercase) current = char.ToLowerInvariant(current);
                        sb.Append(current);
                        state = SeparatorState.UppercaseLetter;
                        break;
                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.DecimalDigitNumber:
                        if (state == SeparatorState.SpaceSeparator) sb.Append(separator);
                        if (!lowercase && category == UnicodeCategory.LowercaseLetter) current = char.ToUpperInvariant(current);
                        sb.Append(current);
                        state = SeparatorState.LowercaseLetterOrDigit;
                        break;
                    case UnicodeCategory.SpaceSeparator:
                        if (state != SeparatorState.NotStarted) state = SeparatorState.SpaceSeparator;
                        break;
                    default:
                        sb.Append(current);
                        state = SeparatorState.NotStarted;
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
