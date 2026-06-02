using System.Collections.Generic;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  FILE STATS
    //  Counters returned by FileClassifier.Classify*() methods.
    //  Previously defined as an inner struct inside CsvIndexer.cs.
    // ════════════════════════════════════════════════════════════════════════
    internal struct FileStats
    {
        public int Images;
        public int Audio;
        public int Video;
        public int Text;
        public int Other;

        public readonly int Total => Images + Audio + Video + Text + Other;

        // ── Status bar string ─────────────────────────────────────────────
        /// <summary>
        /// Compact summary for the main status bar, e.g.:
        /// "📁 3 carpetas  ·  📄 12 archivos  ·  🖼️ 8  ·  🎵 4"
        /// </summary>
        public readonly string ToStatusString(int folderCount)
        {
            var parts = new List<string>();

            if (folderCount > 0)
                parts.Add($"📁 {folderCount} carpeta{(folderCount != 1 ? "s" : "")}");
            if (Total > 0)
                parts.Add($"📄 {Total} archivo{(Total != 1 ? "s" : "")}");
            if (Images > 0) parts.Add($"🖼️ {Images}");
            if (Audio > 0) parts.Add($"🎵 {Audio}");
            if (Video > 0) parts.Add($"🎬 {Video}");
            if (Text > 0) parts.Add($"📝 {Text}");
            if (Other > 0) parts.Add($"📦 {Other}");

            return string.Join("  ·  ", parts);
        }

        // ── Folder info column string ─────────────────────────────────────
        /// <summary>
        /// Compact summary for the "Información" column of folder rows, e.g.:
        /// "2 sub, 8 img, 4 aud"
        /// </summary>
        public readonly string ToInfoColumn(int subfolderCount)
        {
            var parts = new List<string>();

            if (subfolderCount > 0) parts.Add($"{subfolderCount} sub");
            if (Images > 0) parts.Add($"{Images} img");
            if (Audio > 0) parts.Add($"{Audio} aud");
            if (Video > 0) parts.Add($"{Video} vid");
            if (Text > 0) parts.Add($"{Text} txt");
            if (Other > 0) parts.Add($"{Other} otros");

            if (parts.Count == 0) parts.Add("vacía");

            return string.Join(", ", parts);
        }
    }
}