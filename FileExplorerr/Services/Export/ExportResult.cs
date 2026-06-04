using System;
using System.IO;

namespace FileExplorerr.Export
{
    // ════════════════════════════════════════════════════════════════════════
    //  EXPORT RESULT
    //  Returned by every IOfficeExporter implementation.
    //  Replaces the implicit exception / bool pattern used by the old
    //  Python-based ExportadorOffice so callers can inspect outcomes
    //  without catching exceptions.
    // ════════════════════════════════════════════════════════════════════════
    public sealed class ExportResult
    {
        /// <summary>True when the file was written without fatal errors.</summary>
        public bool Success { get; }

        /// <summary>Absolute path of the generated file, or empty on failure.</summary>
        public string OutputPath { get; }

        /// <summary>Size in bytes of the generated file (0 on failure).</summary>
        public long BytesWritten { get; }

        /// <summary>
        /// Number of data rows written.
        /// May be less than the source DataTable when MaxRows truncation applied.
        /// </summary>
        public int RowsWritten { get; }

        /// <summary>
        /// True when the source had more rows than MaxRows and truncation occurred.
        /// </summary>
        public bool WasTruncated { get; }

        /// <summary>
        /// Human-readable error message.  Empty on success.
        /// Intended for MessageBox display — not for exception re-throw.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// The original exception, if any.  Null on success.
        /// Useful for logging without re-throwing.
        /// </summary>
        public Exception? Exception { get; }

        // ── Private constructor — use factory methods ──────────────────────

        private ExportResult(
            bool success,
            string outputPath,
            long bytesWritten,
            int rowsWritten,
            bool wasTruncated,
            string errorMessage,
            Exception? exception)
        {
            Success = success;
            OutputPath = outputPath;
            BytesWritten = bytesWritten;
            RowsWritten = rowsWritten;
            WasTruncated = wasTruncated;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        // ── Factory methods ───────────────────────────────────────────────

        /// <summary>Creates a successful result after the file has been written.</summary>
        public static ExportResult Ok(string outputPath, int rowsWritten, bool wasTruncated = false)
        {
            long bytes = 0;
            try { bytes = new FileInfo(outputPath).Length; } catch { /* non-fatal */ }

            return new ExportResult(
                success: true,
                outputPath: outputPath,
                bytesWritten: bytes,
                rowsWritten: rowsWritten,
                wasTruncated: wasTruncated,
                errorMessage: string.Empty,
                exception: null);
        }

        /// <summary>Creates a failure result without an underlying exception.</summary>
        public static ExportResult Fail(string message) =>
            new ExportResult(
                success: false,
                outputPath: string.Empty,
                bytesWritten: 0,
                rowsWritten: 0,
                wasTruncated: false,
                errorMessage: message,
                exception: null);

        /// <summary>Creates a failure result wrapping a caught exception.</summary>
        public static ExportResult Fail(Exception ex, string? userMessage = null) =>
            new ExportResult(
                success: false,
                outputPath: string.Empty,
                bytesWritten: 0,
                rowsWritten: 0,
                wasTruncated: false,
                errorMessage: userMessage ?? ex.Message,
                exception: ex);

        /// <summary>Creates a cancelled result (user pressed Cancel).</summary>
        public static ExportResult Cancelled() =>
            new ExportResult(
                success: false,
                outputPath: string.Empty,
                bytesWritten: 0,
                rowsWritten: 0,
                wasTruncated: false,
                errorMessage: "Operación cancelada.",
                exception: null);
    }
}