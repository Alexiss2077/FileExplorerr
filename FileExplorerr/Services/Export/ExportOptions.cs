using System.Threading;

namespace FileExplorerr.Export
{
    // ════════════════════════════════════════════════════════════════════════
    //  EXPORT OPTIONS
    //  Immutable DTO that carries every configurable parameter for an export
    //  operation.  All exporters receive a single ExportOptions instance so
    //  adding a new parameter never requires changing IOfficeExporter.
    //
    //  Use the fluent builder (ExportOptions.For) to construct instances:
    //
    //      var opts = ExportOptions.For(outputPath, "Mi título")
    //                              .WithMaxRows(5_000)
    //                              .WithTimestamp(true)
    //                              .Build();
    // ════════════════════════════════════════════════════════════════════════
    public sealed class ExportOptions
    {
        // ── Required ──────────────────────────────────────────────────────

        /// <summary>Full path where the exported file will be written.</summary>
        public string OutputPath { get; }

        /// <summary>
        /// Human-readable title stamped on the cover page / document header.
        /// </summary>
        public string Title { get; }

        // ── Row limits ────────────────────────────────────────────────────

        /// <summary>
        /// Maximum number of data rows to export.  0 = unlimited.
        /// Word and PowerPoint enforce lower defaults inside their exporters.
        /// </summary>
        public int MaxRows { get; }

        // ── Metadata ──────────────────────────────────────────────────────

        /// <summary>Stamp the current date/time on cover pages.</summary>
        public bool IncludeTimestamp { get; }

        /// <summary>Name of the author embedded in document properties.</summary>
        public string Author { get; }

        // ── Theme colours (Arctic Night) ──────────────────────────────────

        /// <summary>Header background in RRGGBB hex (no leading #).</summary>
        public string HeaderBackground { get; }

        /// <summary>Header foreground in RRGGBB hex.</summary>
        public string HeaderForeground { get; }

        /// <summary>Even-row background in RRGGBB hex.</summary>
        public string RowEvenBackground { get; }

        /// <summary>Odd-row background in RRGGBB hex.</summary>
        public string RowOddBackground { get; }

        /// <summary>Accent colour used for titles and highlights.</summary>
        public string AccentColor { get; }

        // ── Cancellation ──────────────────────────────────────────────────

        /// <summary>Token that exporters should honour for cooperative cancellation.</summary>
        public CancellationToken CancellationToken { get; }

        // ── Constructor (private — use builder) ───────────────────────────

        private ExportOptions(Builder b)
        {
            OutputPath = b.OutputPath;
            Title = b.Title;
            MaxRows = b.MaxRows;
            IncludeTimestamp = b.IncludeTimestamp;
            Author = b.Author;
            HeaderBackground = b.HeaderBackground;
            HeaderForeground = b.HeaderForeground;
            RowEvenBackground = b.RowEvenBackground;
            RowOddBackground = b.RowOddBackground;
            AccentColor = b.AccentColor;
            CancellationToken = b.CancellationToken;
        }

        // ════════════════════════════════════════════════════════════════════
        //  FLUENT BUILDER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Creates a builder pre-loaded with the Arctic Night defaults.</summary>
        public static Builder For(string outputPath, string title) =>
            new Builder(outputPath, title);

        public sealed class Builder
        {
            internal string OutputPath;
            internal string Title;
            internal int MaxRows = 0;
            internal bool IncludeTimestamp = true;
            internal string Author = "FileExplorerr";
            internal string HeaderBackground = "1A3A4A";
            internal string HeaderForeground = "FFFFFF";
            internal string RowEvenBackground = "E8F4F8";
            internal string RowOddBackground = "FFFFFF";
            internal string AccentColor = "48CAB4";
            internal CancellationToken CancellationToken = CancellationToken.None;

            internal Builder(string outputPath, string title)
            {
                OutputPath = outputPath;
                Title = title;
            }

            public Builder WithMaxRows(int max) { MaxRows = max; return this; }
            public Builder WithTimestamp(bool include) { IncludeTimestamp = include; return this; }
            public Builder WithAuthor(string author) { Author = author; return this; }
            public Builder WithCancellation(CancellationToken ct) { CancellationToken = ct; return this; }

            /// <summary>
            /// Override the entire colour palette in one call.
            /// All values are RRGGBB hex strings (no leading #).
            /// </summary>
            public Builder WithPalette(
                string headerBg,
                string headerFg,
                string rowEvenBg,
                string rowOddBg,
                string accent)
            {
                HeaderBackground = headerBg;
                HeaderForeground = headerFg;
                RowEvenBackground = rowEvenBg;
                RowOddBackground = rowOddBg;
                AccentColor = accent;
                return this;
            }

            public ExportOptions Build() => new ExportOptions(this);
        }
    }
}