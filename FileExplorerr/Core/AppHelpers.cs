using System;
using System.Collections.Generic;
using System.Text;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  FILE EXTENSIONS — single source of truth for every categorised extension
    //  Previously duplicated across: CsvIndexer, Form1, FilePropertiesForm,
    //  MusicPlayerForm, VideoPlayerForm, ImageViewerForm, FileViewerForm.
    // ════════════════════════════════════════════════════════════════════════
    internal static class FileExtensions
    {
        public static readonly HashSet<string> Image = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".jfif", ".jpe", ".png", ".gif", ".bmp", ".dib",
            ".tiff", ".tif", ".ico", ".webp", ".avif", ".heic", ".heif",
            ".emf", ".wmf", ".svg", ".ppm", ".pgm", ".pbm", ".tga", ".exr",
            ".raw", ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".srf", ".sr2",
            ".orf", ".rw2", ".dng", ".pef", ".raf", ".3fr"
        };

        public static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".wma", ".m4a", ".flac", ".aac", ".ogg", ".opus", ".aiff"
        };

        public static readonly HashSet<string> Video = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm",
            ".m4v", ".ts", ".3gp", ".mpg", ".mpeg", ".vob", ".divx", ".ogv"
        };

        public static readonly HashSet<string> Text = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".csv", ".json", ".xml", ".log", ".ini", ".config",
            ".md", ".cs", ".py", ".js", ".ts", ".html", ".css", ".yaml", ".yml"
        };

        public static readonly HashSet<string> Document = new(StringComparer.OrdinalIgnoreCase)
        {
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf"
        };

        public static readonly HashSet<string> Archive =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip",
                   // Future: ".7z", ".rar", ".tar", ".gz", ".tgz"
        };

        /// <summary>Returns the broad category for a given extension.</summary>
        public static FileCategory Categorise(string extension)
        {
            if (Image.Contains(extension)) return FileCategory.Image;
            if (Audio.Contains(extension)) return FileCategory.Audio;
            if (Video.Contains(extension)) return FileCategory.Video;
            if (Text.Contains(extension)) return FileCategory.Text;
            if (Document.Contains(extension)) return FileCategory.Document;
            if (Archive.Contains(extension)) return FileCategory.Archive;
            return FileCategory.Other;
        }
    }
    internal enum FileCategory { Image, Audio, Video, Text, Document, Archive, Other }

    // ════════════════════════════════════════════════════════════════════════
    //  FORMAT HELPERS — eliminate the five identical FormatSize implementations
    //  and the two identical FormatDuration implementations.
    // ════════════════════════════════════════════════════════════════════════
    internal static class FileSize
    {
        private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

        /// <summary>Human-readable file size, e.g. "3.4 MB".</summary>
        public static string Format(long bytes)
        {
            double value = bytes;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < Units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }
            return $"{value:0.##} {Units[unitIndex]}";
        }
    }

    internal static class TimeSpanFormat
    {
        /// <summary>Human-readable duration, e.g. "1:23:45" or "3:07".</summary>
        public static string Format(TimeSpan duration)
        {
            return duration.Hours > 0
                ? $"{duration.Hours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                : $"{duration.Minutes}:{duration.Seconds:D2}";
        }

        /// <summary>Format total seconds as a duration string.</summary>
        public static string Format(double totalSeconds) =>
            Format(TimeSpan.FromSeconds(Math.Max(0, totalSeconds)));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CSV HELPERS — single implementation replacing duplicates in
    //  FileViewerForm and SqlViewerForm.
    // ════════════════════════════════════════════════════════════════════════
    internal static class CsvHelper
    {
        /// <summary>
        /// Splits one CSV line respecting quoted fields and escaped double-quotes.
        /// </summary>
        public static List<string> SplitLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // Escaped double-quote inside a quoted field ("").
                    if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuote = !inQuote;
                    }
                }
                else if (c == ',' && !inQuote)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result;
        }

        /// <summary>Escapes a value for embedding inside a quoted CSV field.</summary>
        public static string EscapeField(string value) =>
            value.Replace("\"", "\"\"");

        /// <summary>Splits content into non-empty lines regardless of line ending.</summary>
        public static List<string> SplitLines(string content) =>
            new List<string>(
                content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                       .Where(static l => !string.IsNullOrEmpty(l)));

        private static IEnumerable<string> Where(string[] source, Func<string, bool> predicate)
        {
            foreach (var item in source)
                if (predicate(item)) yield return item;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BROWSER EMULATION HELPER — removes the duplicate method that existed
    //  in both ImageViewerForm and VideoPlayerForm.
    // ════════════════════════════════════════════════════════════════════════
    internal static class BrowserHelper
    {
        private const int EmulationEdge = 11001;

        /// <summary>
        /// Registers IE-edge emulation in the registry so that embedded
        /// WebBrowser controls render modern HTML (e.g. Leaflet maps).
        /// </summary>
        public static void SetEdgeEmulation()
        {
            try
            {
                string processName =
                    System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe";

                const string keyPath =
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION";

                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, writable: true)
                             ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath);

                key?.SetValue(processName, EmulationEdge,
                              Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch
            {
                // Non-fatal: the map will degrade gracefully if the key cannot be written.
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SMTP CONFIGURATION — replaces hardcoded credentials in EmailForm.
    //  Credentials are persisted in the user's AppData folder, never in source.
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class SmtpConfig
    {
        public string SenderAddress { get; set; } = string.Empty;
        public string AppPassword { get; set; } = string.Empty;
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(SenderAddress) &&
            !string.IsNullOrWhiteSpace(AppPassword);

        // ── Persistence ───────────────────────────────────────────────────

        private static string ConfigPath =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FileExplorerr",
                "smtp.cfg");

        public static SmtpConfig Load()
        {
            try
            {
                if (!System.IO.File.Exists(ConfigPath))
                    return new SmtpConfig();

                var lines = System.IO.File.ReadAllLines(ConfigPath);
                var cfg = new SmtpConfig();

                foreach (var line in lines)
                {
                    var idx = line.IndexOf('=');
                    if (idx < 0) continue;
                    var key = line[..idx].Trim();
                    var value = line[(idx + 1)..].Trim();

                    switch (key)
                    {
                        case "SenderAddress": cfg.SenderAddress = value; break;
                        case "AppPassword": cfg.AppPassword = value; break;
                        case "Host": cfg.Host = value; break;
                        case "Port":
                            if (int.TryParse(value, out int p)) cfg.Port = p;
                            break;
                    }
                }
                return cfg;
            }
            catch
            {
                return new SmtpConfig();
            }
        }

        public void Save()
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(ConfigPath)!;
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllLines(ConfigPath, new[]
                {
                    $"SenderAddress={SenderAddress}",
                    $"AppPassword={AppPassword}",
                    $"Host={Host}",
                    $"Port={Port}"
                });
            }
            catch
            {
                // Non-fatal: the user will simply need to re-enter credentials next time.
            }
        }
    }
}