using System.IO;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  FILE CLASSIFIER
    //  Categorises files by extension and produces FileStats counts.
    //  Previously scattered as private/internal methods inside CsvIndexer.cs.
    //
    //  Depends on: FileExtensions (AppHelpers.cs), FileStats (FileStats.cs)
    // ════════════════════════════════════════════════════════════════════════
    internal static class FileClassifier
    {
        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Counts files by category from an array of <see cref="FileInfo"/> objects.
        /// Used when the caller already has FileInfo instances (e.g. after GetFiles()).
        /// </summary>
        public static FileStats Classify(FileInfo[] files)
        {
            var stats = new FileStats();

            foreach (var file in files)
                Increment(ref stats, file.Extension);

            return stats;
        }

        /// <summary>
        /// Counts files by category from a raw array of extension strings.
        /// Faster when only extensions are available (avoids FileInfo allocation).
        /// </summary>
        public static FileStats ClassifyByExtensions(string[] extensions)
        {
            var stats = new FileStats();

            foreach (var ext in extensions)
                Increment(ref stats, ext);

            return stats;
        }

        // ── Internal helpers ──────────────────────────────────────────────

        private static void Increment(ref FileStats stats, string extension)
        {
            switch (FileExtensions.Categorise(extension))
            {
                case FileCategory.Image: stats.Images++; break;
                case FileCategory.Audio: stats.Audio++; break;
                case FileCategory.Video: stats.Video++; break;
                case FileCategory.Text: stats.Text++; break;
                default: stats.Other++; break;
            }
        }
    }
}