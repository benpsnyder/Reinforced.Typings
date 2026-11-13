using Xunit;

namespace Reinforced.Typings.Tests
{
    public class JsonNamingPolicyTests
    {
        public static readonly TheoryData<string> Names = new TheoryData<string>
        {
            "FirstName", "HTTPStatusCode", "IOStream", "Id", "ID", "URL", "url", "already_snake",
            "Value2Go", "ABCDef", "X", "MyHTTPServer2", "Name With Space", "A1B2", "_private", "IsHTTPS",
        };

        [Theory]
        [MemberData(nameof(Names))]
        public void KnownConversions(string name)
        {
            // Spot values that hold on every target framework.
            Assert.Equal(name, SystemTextJson.ConvertName(JsonNamingPolicyKind.None, name));
            Assert.NotNull(SystemTextJson.ConvertName(JsonNamingPolicyKind.SnakeCaseLower, name));
        }

        [Fact]
        public void SnakeAndKebabExamples()
        {
            Assert.Equal("first_name", SystemTextJson.ConvertName(JsonNamingPolicyKind.SnakeCaseLower, "FirstName"));
            Assert.Equal("HTTP_STATUS_CODE", SystemTextJson.ConvertName(JsonNamingPolicyKind.SnakeCaseUpper, "HTTPStatusCode"));
            Assert.Equal("my-http-server2", SystemTextJson.ConvertName(JsonNamingPolicyKind.KebabCaseLower, "MyHTTPServer2"));
            Assert.Equal("ioStream", SystemTextJson.ConvertName(JsonNamingPolicyKind.CamelCase, "IOStream"));
        }

#if NET
        [Theory]
        [MemberData(nameof(Names))]
        public void MatchesSystemTextJson(string name)
        {
            Assert.Equal(System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(name), SystemTextJson.ConvertName(JsonNamingPolicyKind.CamelCase, name));
            Assert.Equal(System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(name), SystemTextJson.ConvertName(JsonNamingPolicyKind.SnakeCaseLower, name));
            Assert.Equal(System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(name), SystemTextJson.ConvertName(JsonNamingPolicyKind.SnakeCaseUpper, name));
            Assert.Equal(System.Text.Json.JsonNamingPolicy.KebabCaseLower.ConvertName(name), SystemTextJson.ConvertName(JsonNamingPolicyKind.KebabCaseLower, name));
            Assert.Equal(System.Text.Json.JsonNamingPolicy.KebabCaseUpper.ConvertName(name), SystemTextJson.ConvertName(JsonNamingPolicyKind.KebabCaseUpper, name));
        }
#endif
    }
}
