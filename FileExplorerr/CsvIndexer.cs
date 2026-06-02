using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  CSV INDEXER
    //  Walks a root directory recursively and produces a CSV with one row
    //  per file found in each sub-folder.
    // ════════════════════════════════════════════════════════════════════════
    internal static class CsvIndexer
    {
        // ── CSV column header ────────────────────────────────────────────────
        private const string CsvHeader =
            "\"Ruta Carpeta\"," +
            "\"Nombre Carpeta\"," +
            "\"Nombre Archivo\"," +
            "\"Extensión\"," +
            "\"Tamaño\"," +
            "\"Último Acceso\"";

        // ── Async entry point ────────────────────────────────────────────────
        /// <summary>
        /// Generates the full CSV content by recursively traversing
        /// <paramref name="rootPath"/>. Reports the current folder name
        /// via <paramref name="progress"/>.
        /// </summary>
        public static Task<string> GenerateAsync(string rootPath, IProgress<string>? progress = null)
        {
            return Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine(CsvHeader);
                ProcessDirectory(rootPath, sb, progress);
                return sb.ToString();
            });
        }

        // ── Recursive traversal ──────────────────────────────────────────────
        private static void ProcessDirectory(string path, StringBuilder sb, IProgress<string>? progress)
        {
            try
            {
                var di = new DirectoryInfo(path);
                progress?.Report(di.Name);

                var files = di.GetFiles()
                              .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                              .OrderBy(f => f.Name)
                              .ToArray();

                var subdirs = di.GetDirectories()
                                .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                                .OrderBy(d => d.Name)
                                .ToArray();

                if (files.Length == 0)
                {
                    // Record empty folders explicitly so they appear in the index.
                    sb.AppendLine(
                        $"\"{Escape(di.FullName)}\"," +
                        $"\"{Escape(di.Name)}\"," +
                        "\"(vacía)\",,,");
                }
                else
                {
                    foreach (var file in files)
                    {
                        sb.AppendLine(
                            $"\"{Escape(di.FullName)}\"," +
                            $"\"{Escape(di.Name)}\"," +
                            $"\"{Escape(file.Name)}\"," +
                            $"\"{file.Extension.TrimStart('.').ToUpper()}\"," +
                            $"\"{FileSize.Format(file.Length)}\"," +
                            $"\"{file.LastWriteTime:dd/MM/yyyy HH:mm}\"");
                    }
                }

                foreach (var sub in subdirs)
                {
                    try
                    {
                        ProcessDirectory(sub.FullName, sb, progress);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip folders we cannot read; continue with siblings.
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip inaccessible root; caller decides how to handle the absence.
            }
        }

        // ── File classification ──────────────────────────────────────────────
        /// <summary>Classifies an array of <see cref="FileInfo"/> by type.</summary>
        internal static FileStats ClassifyFiles(FileInfo[] files)
        {
            int images = 0, audio = 0, video = 0, text = 0, other = 0;

            foreach (var file in files)
            {
                switch (FileExtensions.Categorise(file.Extension))
                {
                    case FileCategory.Image: images++; break;
                    case FileCategory.Audio: audio++; break;
                    case FileCategory.Video: video++; break;
                    case FileCategory.Text: text++; break;
                    default: other++; break;
                }
            }

            return new FileStats
            {
                Images = images,
                Audio = audio,
                Video = video,
                Text = text,
                Other = other
            };
        }

        /// <summary>Classifies an array of raw extensions (faster path for the info panel).</summary>
        internal static FileStats ClassifyByExtensions(string[] extensions)
        {
            int images = 0, audio = 0, video = 0, text = 0, other = 0;

            foreach (var ext in extensions)
            {
                switch (FileExtensions.Categorise(ext))
                {
                    case FileCategory.Image: images++; break;
                    case FileCategory.Audio: audio++; break;
                    case FileCategory.Video: video++; break;
                    case FileCategory.Text: text++; break;
                    default: other++; break;
                }
            }

            return new FileStats
            {
                Images = images,
                Audio = audio,
                Video = video,
                Text = text,
                Other = other
            };
        }

        // ── CSV escaping ─────────────────────────────────────────────────────
        private static string Escape(string s) => s.Replace("\"", "\"\"");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  FILE STATS — counters returned by CsvIndexer.ClassifyFiles
    // ════════════════════════════════════════════════════════════════════════
    internal struct FileStats
    {
        public int Images;
        public int Audio;
        public int Video;
        public int Text;
        public int Other;

        public readonly int Total => Images + Audio + Video + Text + Other;

        /// <summary>Compact summary for the status bar.</summary>
        public readonly string ToStatusString(int folderCount)
        {
            var parts = new List<string>();
            if (folderCount > 0) parts.Add($"📁 {folderCount} carpeta{(folderCount != 1 ? "s" : "")}");
            if (Total > 0) parts.Add($"📄 {Total} archivo{(Total != 1 ? "s" : "")}");
            if (Images > 0) parts.Add($"🖼️ {Images}");
            if (Audio > 0) parts.Add($"🎵 {Audio}");
            if (Video > 0) parts.Add($"🎬 {Video}");
            if (Text > 0) parts.Add($"📝 {Text}");
            if (Other > 0) parts.Add($"📦 {Other}");
            return string.Join("  ·  ", parts);
        }

        /// <summary>Compact summary for the "Información" column of folder rows.</summary>
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