using System;
using System.Collections.Generic;
using System.IO;

namespace FileExplorerr.Compression
{
    // ════════════════════════════════════════════════════════════════════════
    //  ARCHIVE RESULT
    //  Returned by every IArchiver implementation (compress + extract).
    //  Replaces the implicit exception pattern so callers can inspect outcomes
    //  without try/catch — identical philosophy to ExportResult.
    //
    //  Compression result fields:
    //    OutputPath    — path of the created archive
    //    BytesWritten  — compressed size on disk
    //    FilesAdded    — number of entries written
    //
    //  Extraction result fields:
    //    DestinationFolder — folder where files were extracted
    //    FilesExtracted    — number of entries successfully written
    //    SkippedFiles      — entries skipped (overwrite=false conflict)
    //
    //  Both operations share:
    //    Success         — true when no fatal error occurred
    //    WasCancelled    — true when the CancellationToken was triggered
    //    ErrorMessage    — human-readable description for MessageBox display
    //    Exception       — original exception for logging (null on success)
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class ArchiveResult
    {
        // ── Common ────────────────────────────────────────────────────────

        /// <summary>True when the operation completed without a fatal error.</summary>
        public bool Success { get; }

        /// <summary>True when the operation was cancelled by the user.</summary>
        public bool WasCancelled { get; }

        /// <summary>
        /// Human-readable outcome message intended for MessageBox display.
        /// Empty on success.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// The original exception, if any.  Null on success.
        /// Useful for Debug.WriteLine logging without re-throwing.
        /// </summary>
        public Exception? Exception { get; }

        // ── Compression result ─────────────────────────────────────────────

        /// <summary>
        /// Full path of the archive that was created.
        /// Empty when the result is from an extraction or on failure.
        /// </summary>
        public string OutputPath { get; }

        /// <summary>Size in bytes of the created archive file.  0 on failure.</summary>
        public long BytesWritten { get; }

        /// <summary>
        /// Number of file entries added to the archive.
        /// Empty directories are not counted.
        /// </summary>
        public int FilesAdded { get; }

        // ── Extraction result ──────────────────────────────────────────────

        /// <summary>
        /// Destination folder where files were extracted.
        /// Empty when the result is from a compression or on failure.
        /// </summary>
        public string DestinationFolder { get; }

        /// <summary>Number of entries successfully extracted.</summary>
        public int FilesExtracted { get; }

        /// <summary>
        /// List of entry names that were skipped because the target already
        /// existed and <see cref="ArchiveOptions.OverwriteExisting"/> was false.
        /// </summary>
        public IReadOnlyList<string> SkippedFiles { get; }

        // ── Private constructor ────────────────────────────────────────────

        private ArchiveResult(
            bool success,
            bool wasCancelled,
            string errorMessage,
            Exception? exception,
            string outputPath,
            long bytesWritten,
            int filesAdded,
            string destinationFolder,
            int filesExtracted,
            IReadOnlyList<string> skippedFiles)
        {
            Success = success;
            WasCancelled = wasCancelled;
            ErrorMessage = errorMessage;
            Exception = exception;
            OutputPath = outputPath;
            BytesWritten = bytesWritten;
            FilesAdded = filesAdded;
            DestinationFolder = destinationFolder;
            FilesExtracted = filesExtracted;
            SkippedFiles = skippedFiles;
        }

        // ════════════════════════════════════════════════════════════════════
        //  FACTORY METHODS — COMPRESSION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a successful compression result.
        /// <paramref name="outputPath"/> must point to the archive on disk;
        /// file size is read automatically.
        /// </summary>
        public static ArchiveResult CompressOk(string outputPath, int filesAdded)
        {
            long bytes = 0;
            try { bytes = new FileInfo(outputPath).Length; } catch { /* non-fatal */ }

            return new ArchiveResult(
                success: true,
                wasCancelled: false,
                errorMessage: string.Empty,
                exception: null,
                outputPath: outputPath,
                bytesWritten: bytes,
                filesAdded: filesAdded,
                destinationFolder: string.Empty,
                filesExtracted: 0,
                skippedFiles: Array.Empty<string>());
        }

        // ════════════════════════════════════════════════════════════════════
        //  FACTORY METHODS — EXTRACTION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Creates a successful extraction result.</summary>
        public static ArchiveResult ExtractOk(
            string destinationFolder,
            int filesExtracted,
            IReadOnlyList<string>? skippedFiles = null)
        {
            return new ArchiveResult(
                success: true,
                wasCancelled: false,
                errorMessage: string.Empty,
                exception: null,
                outputPath: string.Empty,
                bytesWritten: 0,
                filesAdded: 0,
                destinationFolder: destinationFolder,
                filesExtracted: filesExtracted,
                skippedFiles: skippedFiles ?? Array.Empty<string>());
        }

        // ════════════════════════════════════════════════════════════════════
        //  FACTORY METHODS — SHARED
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Creates a failure result from a human-readable message.</summary>
        public static ArchiveResult Fail(string message) =>
            new ArchiveResult(
                success: false,
                wasCancelled: false,
                errorMessage: message,
                exception: null,
                outputPath: string.Empty,
                bytesWritten: 0,
                filesAdded: 0,
                destinationFolder: string.Empty,
                filesExtracted: 0,
                skippedFiles: Array.Empty<string>());

        /// <summary>Creates a failure result wrapping a caught exception.</summary>
        public static ArchiveResult Fail(Exception ex, string? userMessage = null) =>
            new ArchiveResult(
                success: false,
                wasCancelled: false,
                errorMessage: userMessage ?? ex.Message,
                exception: ex,
                outputPath: string.Empty,
                bytesWritten: 0,
                filesAdded: 0,
                destinationFolder: string.Empty,
                filesExtracted: 0,
                skippedFiles: Array.Empty<string>());

        /// <summary>Creates a cancelled result (user pressed Cancel).</summary>
        public static ArchiveResult Cancelled() =>
            new ArchiveResult(
                success: false,
                wasCancelled: true,
                errorMessage: "Operación cancelada.",
                exception: null,
                outputPath: string.Empty,
                bytesWritten: 0,
                filesAdded: 0,
                destinationFolder: string.Empty,
                filesExtracted: 0,
                skippedFiles: Array.Empty<string>());
    }
}