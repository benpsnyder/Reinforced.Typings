using System;
using System.Collections.Generic;
using System.IO;
using Reinforced.Typings.Ast.Dependency;
using Reinforced.Typings.Exceptions;
using Reinforced.Typings.ReferencesInspection;
using Reinforced.Typings.Visitors;
using Reinforced.Typings.Visitors.TypeScript;
using Reinforced.Typings.Visitors.Typings;

namespace Reinforced.Typings
{
    /// <summary>
    /// Implementation of file operations abstraction
    /// </summary>
    public class FilesOperations : IFilesOperations, ITextFilesOperations
    {
        private readonly List<string> _tmpFiles = new List<string>();

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public ExportContext Context { get; set; }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public void DeployTempFiles()
        {
            var checkMode = Context != null && Context.CheckMode;
            var drift = new List<string>();
            foreach (var tmpFile in _tmpFiles)
            {
                var origFile = Path.GetFileNameWithoutExtension(tmpFile);
                var origDir = Path.GetDirectoryName(tmpFile);
                origFile = Path.Combine(origDir, origFile);
                try
                {
                    var unchanged = HasSameContent(tmpFile, origFile);
                    if (checkMode)
                    {
                        // Check mode never touches the committed output: compare, then discard.
                        if (!unchanged) drift.Add(origFile);
                        File.Delete(tmpFile);
                        continue;
                    }

                    if (unchanged)
                    {
                        // Keep the existing file (and its timestamp) when the content is identical,
                        // so that downstream tooling watching the output is not invalidated.
                        File.Delete(tmpFile);
                        continue;
                    }

                    if (File.Exists(origFile)) File.Delete(origFile);
                    File.Move(tmpFile, origFile);
#if DEBUG
                    Console.WriteLine("File replaced: {0} -> {1}", tmpFile, origFile);
#endif
                }
                catch (Exception ex)
                {
                    ErrorMessages.RTE0002_DeployingFilesError.Throw(origFile, ex.Message);
                }
            }

            if (drift.Count > 0)
            {
                ErrorMessages.RTE0019_CheckModeDrift.Throw(drift.Count, string.Join("; ", drift));
            }
        }

        /// <summary>
        /// Returns true when both files exist and have byte-identical content
        /// </summary>
        /// <param name="left">First file</param>
        /// <param name="right">Second file</param>
        public static bool HasSameContent(string left, string right)
        {
            if (!File.Exists(left) || !File.Exists(right)) return false;
            if (new FileInfo(left).Length != new FileInfo(right).Length) return false;
            var a = File.ReadAllBytes(left);
            var b = File.ReadAllBytes(right);
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// Writes raw text content (for example the Effect Schema module) through the same
        /// temporary-file and deployment path as generated TypeScript files.
        /// </summary>
        /// <param name="fileName">Target file</param>
        /// <param name="content">File content</param>
        public void ExportText(string fileName, string content)
        {
            using (var fs = GetTmpFile(fileName))
            {
                using (var tw = new StreamWriter(fs))
                {
                    tw.NewLine = Context.Global.NewLine;
                    tw.Write(content);
                }
            }
        }

        /// <summary>
        /// Internal implementation of file Export operation
        /// </summary>
        /// <param name="tw">Target stream</param>
        /// <param name="file">Exported file</param>
        protected virtual void ExportCore(StreamWriter tw, ExportedFile file)
        {

            var visitor =
                Context.Global.VisitorType == null
                    ? Context.Global.ExportPureTypings
                        ? new TypingsExportVisitor(tw, Context)
                        : new TypeScriptExportVisitor(tw, Context)
                    : (TextExportingVisitor) Activator.CreateInstance(Context.Global.VisitorType, new object[] { tw, Context });

            WriteWarning(tw);

            visitor.VisitFile(file);
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public void Export(string fileName, ExportedFile file)
        {
            using (var fs = GetTmpFile(fileName))
            {
                using (var tw = new StreamWriter(fs))
                {
                    tw.NewLine = Context.Global.NewLine;
                    ExportCore(tw, file);
                }
            }
        }

        private void WriteWarning(TextWriter tw)
        {
            if (Context.Global.WriteWarningComment)
            {
                tw.WriteLine("//     This code was generated by a Reinforced.Typings tool.");
                tw.WriteLine("//     Changes to this file may cause incorrect behavior and will be lost if");
                tw.WriteLine("//     the code is regenerated.");
                tw.WriteLine();
            }
        }

        private Stream GetTmpFile(string fileName)
        {
            fileName = fileName + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(fileName);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
#if DEBUG
                Console.WriteLine("Temp file aquired: {0}", fileName);
#endif
                _tmpFiles.Add(fileName);
            }
            catch (Exception ex)
            {
                ErrorMessages.RTE0001_TempFileError.Throw(fileName, ex.Message);
            }

            return File.OpenWrite(fileName);
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public void ClearTempRegistry()
        {
            _tmpFiles.Clear();
        }
    }

    internal static class ArrayExtensions
    {
        public static bool PartialCompare(string[] array1, string[] array2, int idx)
        {
            var minLen = array1.Length > array2.Length ? array2.Length : array1.Length;
            if (idx > minLen) return false;
            for (int i = 0; i < idx; i++)
            {
                if (array1[i] != array2[i]) return false;
            }

            return true;
        }
    }
}