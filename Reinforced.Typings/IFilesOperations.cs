using Reinforced.Typings.ReferencesInspection;

namespace Reinforced.Typings
{
    /// <summary>
    /// Interface of files operator
    /// It has to be public for testing purposes
    /// </summary>
    public interface IFilesOperations
    {
        /// <summary>
        /// Export context
        /// </summary>
        ExportContext Context { set; }

        /// <summary>
        /// Writes temporary files contents to disk
        /// </summary>
        void DeployTempFiles();

        /// <summary>
        /// Cleans up temporary files registry
        /// </summary>
        void ClearTempRegistry();

        /// <summary>
        /// Exports specified syntax nodes to specified file
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="file">File to be exported</param>
        void Export(string fileName, ExportedFile file);
    }

    /// <summary>
    /// Optional extension of <see cref="IFilesOperations"/> for files that are not produced from the TypeScript AST
    /// (the Effect Schema module). Implementations that do not support it cannot emit those files.
    /// </summary>
    public interface ITextFilesOperations
    {
        /// <summary>
        /// Exports raw text to the specified file
        /// </summary>
        /// <param name="fileName">Target file</param>
        /// <param name="content">Text content</param>
        void ExportText(string fileName, string content);
    }
}
