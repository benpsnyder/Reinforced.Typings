using System;
using System.IO;
using Reinforced.Typings.Integrate;
using Xunit;

namespace Reinforced.Typings.Tests
{
    public class GenerationStampTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _source;
        private readonly string _reference;
        private readonly string _output;
        private static readonly DateTime Fixed = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public GenerationStampTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rt-stamp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_dir, "out"));
            _source = Path.Combine(_dir, "a.dll");
            _reference = Path.Combine(_dir, "ref.dll");
            _output = Path.Combine(_dir, "out", "x.ts");
            File.WriteAllText(_source, "source-1");
            File.WriteAllText(_reference, "reference");
            File.WriteAllText(_output, "output");
            File.SetLastWriteTimeUtc(_source, Fixed);
            File.SetLastWriteTimeUtc(_reference, Fixed);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private string Key(string setting = "s", bool hierarchical = false)
        {
            return hierarchical
                ? GenerationStamp.Compute(new[] { setting }, new[] { _source }, new[] { _reference }, new string[0], new[] { Path.Combine(_dir, "out") })
                : GenerationStamp.Compute(new[] { setting }, new[] { _source }, new[] { _reference }, new[] { _output }, new string[0]);
        }

        [Fact]
        public void SameInputsGiveSameKey()
        {
            Assert.Equal(Key(), Key());
            Assert.Equal(Key(hierarchical: true), Key(hierarchical: true));
        }

        [Fact]
        public void SourceContentIsHashedEvenWithSameSizeAndTimestamp()
        {
            var before = Key();
            File.WriteAllText(_source, "source-2");
            File.SetLastWriteTimeUtc(_source, Fixed);
            Assert.NotEqual(before, Key());
        }

        [Fact]
        public void ReferenceTimestampChangesKey()
        {
            var before = Key();
            File.SetLastWriteTimeUtc(_reference, Fixed.AddSeconds(1));
            Assert.NotEqual(before, Key());
        }

        [Fact]
        public void EditedOrDeletedOutputChangesKey()
        {
            var before = Key();
            File.WriteAllText(_output, "edited");
            var edited = Key();
            Assert.NotEqual(before, edited);
            File.Delete(_output);
            Assert.NotEqual(edited, Key());
        }

        [Fact]
        public void NewFileInHierarchicalOutputChangesKey()
        {
            var before = Key(hierarchical: true);
            File.WriteAllText(Path.Combine(_dir, "out", "y.ts"), "y");
            Assert.NotEqual(before, Key(hierarchical: true));
        }

        [Fact]
        public void SettingChangesKey()
        {
            Assert.NotEqual(Key("a"), Key("b"));
        }

        [Fact]
        public void StampRoundTrip()
        {
            var stamp = Path.Combine(_dir, "obj", "rt.stamp");
            Assert.False(GenerationStamp.Matches(stamp, "k"));
            GenerationStamp.Write(stamp, "k");
            Assert.True(GenerationStamp.Matches(stamp, "k"));
            Assert.False(GenerationStamp.Matches(stamp, "other"));
            GenerationStamp.Delete(stamp);
            Assert.False(GenerationStamp.Matches(stamp, "k"));
        }
    }
}
