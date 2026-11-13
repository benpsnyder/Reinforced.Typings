using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Reinforced.Typings.Integrate
{
    /// <summary>
    /// Builds the key that decides whether generation can be skipped (RtSkipUnchanged).
    /// The key is conservative: anything that can change the output changes the key.
    /// </summary>
    public static class GenerationStamp
    {
        /// <summary>
        /// Computes the key.
        /// </summary>
        /// <param name="settings">Every task setting that reaches rtcli, plus the tool identity</param>
        /// <param name="contentFiles">Files hashed by content: source assemblies and the XML doc file</param>
        /// <param name="metadataFiles">Files identified by path, length and last write time: references and the tool</param>
        /// <param name="outputFiles">Generated files, hashed by content (a missing file is part of the key)</param>
        /// <param name="outputDirectories">Hierarchical target directories: every file below is hashed by content</param>
        public static string Compute(IEnumerable<string> settings, IEnumerable<string> contentFiles,
            IEnumerable<string> metadataFiles, IEnumerable<string> outputFiles, IEnumerable<string> outputDirectories)
        {
            var sb = new StringBuilder();
            sb.Append("format=1\n");
            foreach (var s in settings) sb.Append("setting=").Append(s ?? string.Empty).Append('\n');
            foreach (var f in contentFiles) sb.Append("content=").Append(f).Append('=').Append(HashFile(f)).Append('\n');
            foreach (var f in metadataFiles) sb.Append("meta=").Append(f).Append('=').Append(Metadata(f)).Append('\n');
            foreach (var f in outputFiles) sb.Append("output=").Append(f).Append('=').Append(HashFile(f)).Append('\n');
            foreach (var d in outputDirectories)
            {
                sb.Append("outdir=").Append(d).Append('\n');
                if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
                foreach (var f in Directory.GetFiles(d, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
                {
                    sb.Append("output=").Append(f).Append('=').Append(HashFile(f)).Append('\n');
                }
            }
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
            }
        }

        private static string Metadata(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "missing";
            var fi = new FileInfo(path);
            return fi.Length.ToString(CultureInfo.InvariantCulture) + ":" + fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        private static string HashFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "missing";
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                return ToHex(sha.ComputeHash(fs));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>
        /// True when the stamp file exists and holds exactly this key
        /// </summary>
        public static bool Matches(string stampFile, string key)
        {
            if (string.IsNullOrEmpty(stampFile) || !File.Exists(stampFile)) return false;
            try
            {
                return File.ReadAllText(stampFile).Trim() == key;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes the key to the stamp file
        /// </summary>
        public static void Write(string stampFile, string key)
        {
            var dir = Path.GetDirectoryName(stampFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(stampFile, key);
        }

        /// <summary>
        /// Deletes the stamp file, so that a failed or forced run never leaves a stale "unchanged" stamp
        /// </summary>
        public static void Delete(string stampFile)
        {
            if (!string.IsNullOrEmpty(stampFile) && File.Exists(stampFile)) File.Delete(stampFile);
        }
    }
}
