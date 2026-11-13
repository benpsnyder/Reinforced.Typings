using System;
using System.IO;
using System.Reflection;
using Reinforced.Typings.Exceptions;
using Reinforced.Typings.Fluent;
using Xunit;

namespace Reinforced.Typings.Tests
{
    public class FileDeploymentTests : IDisposable
    {
        public interface IDeployedDto
        {
            int Value { get; }
        }

        private readonly string _dir;

        public FileDeploymentTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rt-deploy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private string Target => Path.Combine(_dir, "out.ts");

        private void Export(bool checkMode, Action<ConfigurationBuilder> extra = null)
        {
            var ec = new ExportContext(new Assembly[] { Assembly.GetExecutingAssembly() }, new FilesOperations())
            {
                Hierarchical = false,
                TargetFile = Target,
                CheckMode = checkMode,
                ConfigurationMethod = b =>
                {
                    b.Global(g => g.DontWriteWarningComment());
                    b.ExportAsInterface<IDeployedDto>().WithPublicProperties();
                    extra?.Invoke(b);
                }
            };
            new TsExporter(ec).Export();
        }

        [Fact]
        public void UnchangedOutputIsNotRewritten()
        {
            Export(false);
            var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(Target, old);

            Export(false);

            Assert.Equal(old, File.GetLastWriteTimeUtc(Target));
            Assert.False(File.Exists(Target + ".tmp"));
        }

        [Fact]
        public void ChangedOutputIsRewritten()
        {
            Export(false);
            var expected = File.ReadAllText(Target);
            File.WriteAllText(Target, "stale");
            File.SetLastWriteTimeUtc(Target, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Export(false);

            Assert.Equal(expected, File.ReadAllText(Target));
        }

        [Fact]
        public void CheckModePassesWhenOutputIsCurrent()
        {
            Export(false);
            var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(Target, old);

            Export(true);

            Assert.Equal(old, File.GetLastWriteTimeUtc(Target));
            Assert.False(File.Exists(Target + ".tmp"));
        }

        [Fact]
        public void CheckModeFailsOnDriftAndLeavesFileUntouched()
        {
            Export(false);
            File.WriteAllText(Target, "stale");

            var ex = Assert.Throws<RtException>(() => Export(true));

            Assert.Equal(19, ex.Code);
            Assert.Contains(Target, ex.Message);
            Assert.Equal("stale", File.ReadAllText(Target));
            Assert.False(File.Exists(Target + ".tmp"));
        }

        [Fact]
        public void CheckModeFailsWhenOutputIsMissing()
        {
            var ex = Assert.Throws<RtException>(() => Export(true));
            Assert.Equal(19, ex.Code);
            Assert.False(File.Exists(Target));
        }
    }
}
