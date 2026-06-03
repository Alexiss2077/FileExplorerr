using System.Collections.Generic;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  QUALITY REPORT
    //  DTO returned by DataQualityAnalyzer.Analyze().
    //  Replaces the seven private collection fields that previously lived
    //  directly on FileViewerForm:
    //
    //    List<int>                         duplicateRows
    //    List<(int,int,string,string)>     dateIssues
    //    List<(int,int)>                   emptyFields
    //    List<(int,int,string,string)>     phoneIssues
    //    List<(int,int,string)>            emailIssues
    //    List<int>                         columnMismatchRows
    //    List<(int,int,int)>               columnMismatchDetails
    //
    //  FileViewerForm holds a single field:
    //    private QualityReport _report = new();
    //  which is replaced after every call to DataQualityAnalyzer.Analyze().
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class QualityReport
    {
        // ── Duplicate rows ────────────────────────────────────────────────
        /// <summary>
        /// Zero-based row indices that are exact duplicates of another row.
        /// Both the original and the duplicate are included.
        /// </summary>
        public List<int> DuplicateRows { get; init; } = new();

        // ── Date issues ───────────────────────────────────────────────────
        /// <summary>
        /// Cells whose date string can be normalised to ISO yyyy-MM-dd.
        /// Tuple: (RowIndex, ColumnIndex, OriginalValue, SuggestedFixedValue)
        /// </summary>
        public List<(int Row, int Col, string Original, string Fixed)>
            DateIssues
        { get; init; } = new();

        // ── Empty fields ──────────────────────────────────────────────────
        /// <summary>Cells that are null or whitespace.</summary>
        public List<(int Row, int Col)> EmptyFields { get; init; } = new();

        // ── Phone issues ──────────────────────────────────────────────────
        /// <summary>
        /// Cells in detected phone columns that contain a malformed number.
        /// Tuple: (RowIndex, ColumnIndex, OriginalValue, SuggestedFixedValue)
        /// </summary>
        public List<(int Row, int Col, string Original, string Fixed)>
            PhoneIssues
        { get; init; } = new();

        // ── Email issues ──────────────────────────────────────────────────
        /// <summary>Cells in detected email columns that fail validation.</summary>
        public List<(int Row, int Col, string Original)>
            EmailIssues
        { get; init; } = new();

        // ── Column-mismatch rows ──────────────────────────────────────────
        // These two are populated by DataParsers.ParseCsv() (not by the
        // analyser) and carried through CsvParseResult into this report so
        // that FileViewerForm only needs to hold a single QualityReport field.

        /// <summary>
        /// Zero-based row indices whose column count differs from the header.
        /// Populated by the CSV parser, not the quality analyser.
        /// </summary>
        public List<int> ColumnMismatchRows { get; init; } = new();

        /// <summary>
        /// Detailed mismatch info for each misaligned row.
        /// Tuple: (RowIndex, ExpectedColumnCount, ActualColumnCount)
        /// </summary>
        public List<(int Row, int ExpectedCols, int ActualCols)>
            ColumnMismatchDetails
        { get; init; } = new();

        // ── Convenience ───────────────────────────────────────────────────

        /// <summary>
        /// True when at least one issue of any kind was detected.
        /// Used in FileViewerForm.LoadFileAsync() to decide whether to show
        /// the analysis popup.
        /// </summary>
        public bool HasIssues =>
            DuplicateRows.Count > 0 ||
            DateIssues.Count > 0 ||
            EmptyFields.Count > 0 ||
            PhoneIssues.Count > 0 ||
            EmailIssues.Count > 0 ||
            ColumnMismatchRows.Count > 0;
    }
}