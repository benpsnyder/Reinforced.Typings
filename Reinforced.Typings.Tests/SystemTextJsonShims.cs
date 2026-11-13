#if NETFRAMEWORK
// .NET Framework has no System.Text.Json in the box. Reinforced.Typings detects the attributes by
// full name, so these look-alikes (same names, same enum values) exercise the same code path.
using System;

namespace System.Text.Json.Serialization
{
    public enum JsonIgnoreCondition
    {
        Never = 0,
        Always = 1,
        WhenWritingDefault = 2,
        WhenWritingNull = 3
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class JsonPropertyNameAttribute : Attribute
    {
        public JsonPropertyNameAttribute(string name) { Name = name; }
        public string Name { get; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class JsonIgnoreAttribute : Attribute
    {
        public JsonIgnoreCondition Condition { get; set; } = JsonIgnoreCondition.Always;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class JsonPropertyOrderAttribute : Attribute
    {
        public JsonPropertyOrderAttribute(int order) { Order = order; }
        public int Order { get; }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class JsonConverterAttribute : Attribute
    {
        public JsonConverterAttribute(Type converterType) { ConverterType = converterType; }
        public Type ConverterType { get; }
    }

    public class JsonStringEnumConverter
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class JsonStringEnumMemberNameAttribute : Attribute
    {
        public JsonStringEnumMemberNameAttribute(string name) { Name = name; }
        public string Name { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public sealed class JsonPolymorphicAttribute : Attribute
    {
        public string TypeDiscriminatorPropertyName { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
    public sealed class JsonDerivedTypeAttribute : Attribute
    {
        public JsonDerivedTypeAttribute(Type derivedType) { DerivedType = derivedType; }
        public JsonDerivedTypeAttribute(Type derivedType, string typeDiscriminator) { DerivedType = derivedType; TypeDiscriminator = typeDiscriminator; }
        public JsonDerivedTypeAttribute(Type derivedType, int typeDiscriminator) { DerivedType = derivedType; TypeDiscriminator = typeDiscriminator; }
        public Type DerivedType { get; }
        public object TypeDiscriminator { get; }
    }
}
#endif
