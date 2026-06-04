using System;
using System.Data;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace FileExplorerr.Export
{
    // ════════════════════════════════════════════════════════════════════════
    //  IOFFICE EXPORTER
    //  Strategy contract implemented by every concrete Office exporter.
    //
    //  Design rules:
    //    - One method only.  All parameters live in ExportOptions.
    //    - Never throws:  wrap all errors in ExportResult.Fail().
    //    - Report progress as integer 0-100 via IProgress<int>.
    //    - Honour CancellationToken from ExportOptions and return
    //      ExportResult.Cancelled() when triggered.
    //    - SupportedExtension is used by OfficeExporterFactory to route
    //      requests; always include the leading dot (e.g. ".xlsx").
    // ════════════════════════════════════════════════════════════════════════
    public interface IOfficeExporter
    {
        /// <summary>
        /// File extension this exporter handles, including the leading dot.
        /// Example: ".xlsx", ".docx", ".pptx", ".pdf"
        /// </summary>
        string SupportedExtension { get; }

        /// <summary>
        /// Exports <paramref name="data"/> to the file specified in
        /// <paramref name="options"/>.
        ///
        /// Implementations must:
        ///   • Never throw — return ExportResult.Fail() instead.
        ///   • Report progress from 0 to 100 via <paramref name="progress"/>
        ///     (null-safe: always check before calling).
        ///   • Honour <c>options.CancellationToken</c> and return
        ///     ExportResult.Cancelled() when signalled.
        ///   • Delete the partial output file if export fails or is cancelled.
        /// </summary>
        Task<ExportResult> ExportAsync(
            DataTable data,
            ExportOptions options,
            IProgress<int>? progress);
    }
}