using System;
using System.IO;
using System.Linq;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  FILE TYPE HELPER
    //  Human-readable type labels for file extensions, and per-folder content
    //  summaries used in the ListView "Info" column.
    //
    //  Phase 5A: extracted from Form1.cs.
    //  Original methods removed from Form1:
    //    FileTypeName(string ext)   -> FileTypeHelper.TypeName(string)
    //    DirInfoDetailed(string)    -> FileTypeHelper.FolderInfoColumn(string)
    // ════════════════════════════════════════════════════════════════════════
    internal static class FileTypeHelper
    {
        // ════════════════════════════════════════════════════════════════════
        //  TYPE NAME
        //  Originally: private string FileTypeName(string ext) on Form1.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns a human-readable label for a file extension,
        /// e.g. ".mp3" returns "MP3", ".docx" returns "Word".
        /// Accepts extensions with or without the leading dot.
        /// Call-sites: replace <c>FileTypeName(f.Extension)</c>
        ///             with     <c>FileTypeHelper.TypeName(f.Extension)</c>.
        /// </summary>
        public static string TypeName(string ext)
        {
            if (!string.IsNullOrEmpty(ext) && !ext.StartsWith('.'))
                ext = "." + ext;

            return (ext ?? string.Empty).ToLowerInvariant() switch
            {
                ".txt" => "Texto",
                ".csv" => "CSV",
                ".json" => "JSON",
                ".xml" => "XML",
                ".md" => "Markdown",
                ".log" => "Log",
                ".cs" => "C#",
                ".py" => "Python",
                ".js" => "JavaScript",
                ".html" => "HTML",
                ".css" => "CSS",
                ".jpg" => "JPEG",
                ".jpeg" => "JPEG",
                ".png" => "PNG",
                ".gif" => "GIF",
                ".bmp" => "BMP",
                ".svg" => "SVG",
                ".webp" => "WebP",
                ".ico" => "Icono",
                ".mp3" => "MP3",
                ".wav" => "WAV",
                ".flac" => "FLAC",
                ".aac" => "AAC",
                ".m4a" => "M4A",
                ".ogg" => "OGG",
                ".mp4" => "MP4",
                ".avi" => "AVI",
                ".mkv" => "MKV",
                ".mov" => "MOV",
                ".wmv" => "WMV",
                ".webm" => "WebM",
                ".pdf" => "PDF",
                ".doc" => "Word",
                ".docx" => "Word",
                ".xls" => "Excel",
                ".xlsx" => "Excel",
                ".ppt" => "PowerPoint",
                ".pptx" => "PowerPoint",
                ".zip" => "ZIP",
                ".rar" => "RAR",
                ".7z" => "7-Zip",
                ".exe" => "Ejecutable",
                ".msi" => "Instalador",
                _ => "Archivo"
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  FOLDER INFO COLUMN
        //  Originally: private string DirInfoDetailed(string path) on Form1.
        //  Provides the text shown in the "Info" column for folder rows in the
        //  main ListView, e.g. "3 sub, 12 img, 5 txt".
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns a compact content summary for a directory.
        /// Delegates file classification to <see cref="CsvIndexer.ClassifyFiles"/>.
        /// Returns "Sin acceso" when the directory cannot be read.
        /// Call-sites: replace <c>DirInfoDetailed(d.FullName)</c>
        ///             with     <c>FileTypeHelper.FolderInfoColumn(d.FullName)</c>.
        /// </summary>
        public static string FolderInfoColumn(string path)
        {
            try
            {
                var di = new DirectoryInfo(path);
                var files = di.GetFiles()
                                .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                                .ToArray();
                var subdirs = di.GetDirectories()
                                .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                                .ToArray();

                return CsvIndexer.ClassifyFiles(files).ToInfoColumn(subdirs.Length);
            }
            catch (UnauthorizedAccessException)
            {
                return "Sin acceso";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FileTypeHelper.FolderInfoColumn] {ex.Message}");
                return "Error";
            }
        }
    }
}