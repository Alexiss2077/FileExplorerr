using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileExplorerr.Compression
{
    // ════════════════════════════════════════════════════════════════════════
    //  IARCHIVER
    //  Strategy contract implemented by every concrete archiver.
    //
    //  Design rules (identical philosophy to IOfficeExporter):
    //    - Two methods only: CompressAsync and ExtractAsync.
    //    - All parameters live in ArchiveOptions.
    //    - Never throws: wrap all errors in ArchiveResult.Fail().
    //    - Report progress as integer 0–100 via IProgress<int>.
    //    - Honour CancellationToken from ArchiveOptions and return
    //      ArchiveResult.Cancelled() when triggered.
    //    - SupportedExtensions is used by ArchiverFactory to route
    //      requests; always include the leading dot (e.g. ".zip").
    //    - currentEntryName progress is updated during processing so
    //      CompressionProgressForm can show which file is being handled.
    // ════════════════════════════════════════════════════════════════════════
    internal interface IArchiver
    {
        // ── Identity ──────────────────────────────────────────────────────

        /// <summary>Human-readable format name shown in the UI (e.g. "ZIP").</summary>
        string DisplayName { get; }

        /// <summary>
        /// File extensions this archiver handles, each including the leading dot.
        /// Example: [".zip"]  or  [".tar", ".tar.gz"].
        /// ArchiverFactory registers one lookup entry per extension.
        /// </summary>
        IReadOnlyList<string> SupportedExtensions { get; }

        // ── Compression ───────────────────────────────────────────────────

        /// <summary>
        /// Compresses all source paths in <see cref="ArchiveOptions.SourcePaths"/>
        /// into the archive file at <see cref="ArchiveOptions.OutputPath"/>.
        ///
        /// Implementations MUST:
        ///   • Never throw — return ArchiveResult.Fail() instead.
        ///   • Report integer progress 0–100 via <paramref name="progress"/>.
        ///   • Report the current file name via <paramref name="currentEntryName"/>.
        ///   • Honour ArchiveOptions.CancellationToken.
        ///   • Delete the partial archive if the operation fails or is cancelled.
        /// </summary>
        Task<ArchiveResult> CompressAsync(
            ArchiveOptions options,
            IProgress<int>? progress,
            IProgress<string>? currentEntryName = null);

        // ── Extraction ────────────────────────────────────────────────────

        /// <summary>
        /// Extracts <see cref="ArchiveOptions.ArchivePath"/> into
        /// <see cref="ArchiveOptions.DestinationFolder"/>.
        ///
        /// Implementations MUST:
        ///   • Never throw — return ArchiveResult.Fail() instead.
        ///   • Validate EVERY entry path against the destination folder to prevent
        ///     path-traversal (Zip Slip) attacks BEFORE writing any byte to disk.
        ///   • Report integer progress 0–100 via <paramref name="progress"/>.
        ///   • Report the current entry name via <paramref name="currentEntryName"/>.
        ///   • Honour ArchiveOptions.CancellationToken.
        ///   • Respect ArchiveOptions.OverwriteExisting; record skipped files.
        /// </summary>
        Task<ArchiveResult> ExtractAsync(
            ArchiveOptions options,
            IProgress<int>? progress,
            IProgress<string>? currentEntryName = null);
    }
}