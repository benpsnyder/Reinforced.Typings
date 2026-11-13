using System;
using System.IO;
using Reinforced.Typings.Cli;
using Xunit;

namespace Reinforced.Typings.Tests
{
    // Upstream issue #226: "rtcli profile" with no path threw IndexOutOfRangeException.
    // Bootstrapper.Main uses Console and Environment.ExitCode, so these tests run sequentially.
    [Collection("Console")]
    public class BootstrapperProfileArgumentTests
    {
        private static (string Output, int ExitCode) RunMain(params string[] args)
        {
            var previousOut = Console.Out;
            var previousExit = Environment.ExitCode;
            var sw = new StringWriter();
            try
            {
                Console.SetOut(sw);
                Environment.ExitCode = 0;
                Bootstrapper.Main(args);
                return (sw.ToString(), Environment.ExitCode);
            }
            finally
            {
                Console.SetOut(previousOut);
                Environment.ExitCode = previousExit;
            }
        }

        [Fact]
        public void ProfileWithoutPathPrintsUsageAndFails()
        {
            var (output, exit) = RunMain("profile");
            Assert.Equal(1, exit);
            Assert.Contains("Usage: rtcli profile <path to profile file>", output);
            Assert.Contains("SourceAssemblies", output); // the parameter help follows
            Assert.DoesNotContain("Index was outside the bounds", output);
        }

        [Fact]
        public void ProfileWithBlankPathPrintsUsageAndFails()
        {
            var (output, exit) = RunMain("profile", " ");
            Assert.Equal(1, exit);
            Assert.Contains("Usage: rtcli profile", output);
        }

        [Fact]
        public void MissingProfileFileFails()
        {
            var (output, exit) = RunMain("profile", Path.Combine(Path.GetTempPath(), "rt-no-such-profile-" + Guid.NewGuid().ToString("N")));
            Assert.Equal(1, exit);
            Assert.Contains("Cannot find profile", output);
        }
    }
}
