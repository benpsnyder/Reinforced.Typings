using Reinforced.Typings.Integrate;
using Xunit;

namespace Reinforced.Typings.Tests
{
    public class ToolSelectionTests
    {
        [Theory]
        // Short names only (identifier/version not supplied)
        [InlineData("net481", "", "", "", "net481", true, false)]
        [InlineData("net10.0", "", "", "", "net10.0", false, false)]
        [InlineData("net11.0", "", "", "", "net11.0", false, false)]
        [InlineData("net12.0", "", "", "", "net11.0", false, true)]
        [InlineData("net13.0-windows", "", "", "", "net11.0", false, true)]
        [InlineData("net10.0-windows10.0.19041.0", "", "", "", "net10.0", false, false)]
        // MSBuild-supplied identifier/version win over the short name
        [InlineData("anything", ".NETCoreApp", "v11.0", "", "net11.0", false, false)]
        [InlineData("anything", ".NETFramework", "v4.8.1", "", "net481", true, false)]
        // netstandard follows the SDK's runtime
        [InlineData("netstandard2.0", ".NETStandard", "v2.0", "10.0", "net10.0", false, false)]
        [InlineData("netstandard2.1", ".NETStandard", "v2.1", "11.0", "net11.0", false, false)]
        [InlineData("netstandard2.0", ".NETStandard", "v2.0", "12.0", "net11.0", false, true)]
        [InlineData("netstandard2.0", ".NETStandard", "v2.0", "", "net10.0", false, false)]
        public void SelectsTool(string tf, string tfi, string tfv, string sdk, string folder, bool isNetFramework, bool rollForward)
        {
            var choice = ToolSelection.Select(tf, tfi, tfv, sdk, null);
            Assert.Null(choice.Error);
            Assert.Equal(folder, choice.Folder);
            Assert.Equal(isNetFramework, choice.IsNetFramework);
            Assert.Equal(rollForward, choice.RollForward);
        }

        [Theory]
        [InlineData("net9.0")]
        [InlineData("net8.0")]
        [InlineData("net5.0")]
        [InlineData("netcoreapp3.1")]
        [InlineData("net48")]
        [InlineData("net472")]
        [InlineData("net461")]
        [InlineData("netstandard1.6")]
        [InlineData("")]
        [InlineData("garbage")]
        public void RejectsUnsupportedFrameworks(string tf)
        {
            var choice = ToolSelection.Select(tf, "", "", "11.0", null);
            Assert.NotNull(choice.Error);
            Assert.Contains("net481, net10.0, net11.0", choice.Error);
            Assert.Null(choice.Folder);
        }

        [Theory]
        [InlineData("net481", "net481", true)]
        [InlineData("NET10.0", "net10.0", false)]
        [InlineData(" net11.0 ", "net11.0", false)]
        public void ForcedFrameworkOverridesTheRule(string forced, string folder, bool isNetFramework)
        {
            var choice = ToolSelection.Select("net9.0", "", "", "", forced);
            Assert.Null(choice.Error);
            Assert.Equal(folder, choice.Folder);
            Assert.Equal(isNetFramework, choice.IsNetFramework);
            Assert.False(choice.RollForward);
        }

        [Theory]
        [InlineData("net461")]
        [InlineData("netcoreapp2.0")]
        [InlineData("net12.0")]
        public void RejectsForcedFrameworksThatAreNotShipped(string forced)
        {
            var choice = ToolSelection.Select("net10.0", "", "", "", forced);
            Assert.NotNull(choice.Error);
            Assert.Contains(forced, choice.Error);
        }
    }
}
