using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  CSV INDEXER
    //  Walks a root directory recursively and produces one CSV row per file.
    //
    //  Depends on:
    //    - FileClassifier  (FileClassifier.cs)  — categorises files by type
    //    - FileStats       (FileStats.cs)        — counters returned to callers
    //    - FileSize        (AppHelpers.cs)        — human-readable size strings
    //    - CsvHelper       (AppHelpers.cs)        — CSV field escaping
    // ════════════════════════════════════════════════════════════════════════
    internal static class CsvIndexer
    {
        // ── CSV header ───────────────────────────────────────────────────────
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
        public static Task<string> GenerateAsync(
            string rootPath,
            IProgress<string>? progress = null)
        {
            return Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine(CsvHeader);
                ProcessDirectory(rootPath, sb, progress);
                return sb.ToString();
            });
        }

        // ── Public classification helpers (called by Form1) ──────────────────

        /// <summary>Classifies an array of FileInfo objects by category.</summary>
        internal static FileStats ClassifyFiles(FileInfo[] files) =>
            FileClassifier.Classify(files);

        /// <summary>Classifies raw extension strings by category.</summary>
        internal static FileStats ClassifyByExtensions(string[] extensions) =>
            FileClassifier.ClassifyByExtensions(extensions);

        // ── Recursive traversal ──────────────────────────────────────────────

        private static void ProcessDirectory(
            string path,
            StringBuilder sb,
            IProgress<string>? progress)
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
                    // Record empty folders explicitly.
                    sb.AppendLine(
                        $"\"{CsvHelper.EscapeField(di.FullName)}\"," +
                        $"\"{CsvHelper.EscapeField(di.Name)}\"," +
                        "\"(vacía)\",,,");
                }
                else
                {
                    foreach (var file in files)
                    {
                        sb.AppendLine(
                            $"\"{CsvHelper.EscapeField(di.FullName)}\"," +
                            $"\"{CsvHelper.EscapeField(di.Name)}\"," +
                            $"\"{CsvHelper.EscapeField(file.Name)}\"," +
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
                        // Skip inaccessible sub-folders; continue with siblings.
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip inaccessible root; caller decides how to handle absence.
            }
        }
    }
}