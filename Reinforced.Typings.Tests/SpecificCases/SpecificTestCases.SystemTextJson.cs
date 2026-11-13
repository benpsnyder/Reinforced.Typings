using System.Text.Json.Serialization;
using Reinforced.Typings.Fluent;
using Xunit;

namespace Reinforced.Typings.Tests.SpecificCases
{
    public class StjDto
    {
        [JsonPropertyName("full_name")]
        public string Name { get; set; }

        [JsonIgnore]
        public string Secret { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Maybe { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int Kept { get; set; }

        [JsonPropertyOrder(-1)]
        public int First { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StjColor
    {
        Red,

        [JsonStringEnumMemberName("dark-blue")]
        DarkBlue
    }

    public enum StjNumeric
    {
        A = 1,
        B = 2
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(StjCircle), "circle")]
    [JsonDerivedType(typeof(StjSquare), 2)]
    public class StjShape
    {
        public double Area { get; set; }
    }

    public class StjCircle : StjShape
    {
        public double Radius { get; set; }
    }

    public class StjSquare : StjShape
    {
        public double Side { get; set; }
    }

    public class StjNamingDto
    {
        public string FirstName { get; set; }

        public int HTTPStatusCode { get; set; }

        [JsonPropertyName("explicit")]
        public int Overridden { get; set; }
    }

    public partial class SpecificTestCases
    {
        [Fact]
        public void SystemTextJsonPropertyAttributes()
        {
            const string result = @"
namespace Reinforced.Typings.Tests.SpecificCases {
	export interface IStjDto
	{
		first: number;
		full_name: string;
		maybe?: string;
		kept: number;
	}
}";
            AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().CamelCaseForProperties().UseSystemTextJson());
                s.ExportAsInterface<StjDto>().WithPublicProperties();
            }, result);
        }

        [Fact]
        public void SystemTextJsonIsOffByDefault()
        {
            const string result = @"
namespace Reinforced.Typings.Tests.SpecificCases {
	export interface IStjDto
	{
		name: string;
		secret: string;
		maybe: string;
		kept: number;
		first: number;
	}
}";
            AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().CamelCaseForProperties());
                s.ExportAsInterface<StjDto>().WithPublicProperties();
            }, result);
        }

        [Fact]
        public void SystemTextJsonStringEnum()
        {
            const string result = @"
namespace Reinforced.Typings.Tests.SpecificCases {
	export enum StjColor {
		Red = ""Red"",
		DarkBlue = ""dark-blue""
	}
}";
            AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().UseSystemTextJson());
                s.ExportAsEnum<StjColor>();
            }, result);
        }

        [Fact]
        public void SystemTextJsonPolymorphismAsDiscriminatedUnion()
        {
            const string result = @"
namespace Reinforced.Typings.Tests.SpecificCases {
	export interface IStjShape
	{
		area: number;
	}
	export type IStjShapeUnion = ({ ""kind"": ""circle"" } & Reinforced.Typings.Tests.SpecificCases.IStjCircle) | ({ ""kind"": 2 } & Reinforced.Typings.Tests.SpecificCases.IStjSquare);
	export interface IStjCircle extends Reinforced.Typings.Tests.SpecificCases.IStjShape
	{
		radius: number;
	}
	export interface IStjSquare extends Reinforced.Typings.Tests.SpecificCases.IStjShape
	{
		side: number;
	}
}";
            AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().CamelCaseForProperties().UseSystemTextJson());
                s.ExportAsInterface<StjShape>().WithPublicProperties();
                s.ExportAsInterface<StjCircle>().WithPublicProperties();
                s.ExportAsInterface<StjSquare>().WithPublicProperties();
            }, result);
        }

        [Fact]
        public void SystemTextJsonNamingPolicy()
        {
            const string result = @"
namespace Reinforced.Typings.Tests.SpecificCases {
	export interface IStjNamingDto
	{
		first_name: string;
		http_status_code: number;
		explicit: number;
	}
}";
            AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().CamelCaseForProperties().UseSystemTextJson()
                    .JsonNamingPolicy(JsonNamingPolicyKind.SnakeCaseLower));
                s.ExportAsInterface<StjNamingDto>().WithPublicProperties();
            }, result);
        }

        [Fact]
        public void EnumsAsUnionTypes()
        {
            const string result = @"
namespace Reinforced.Typings.Tests.SpecificCases {
	export type StjColor = ""Red"" | ""dark-blue"";
	export type StjNumeric = 1 | 2;
}";
            AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment().UseSystemTextJson().EnumsAsUnionTypes());
                s.ExportAsEnum<StjColor>();
                s.ExportAsEnum<StjNumeric>();
            }, result);
        }
    }
}
