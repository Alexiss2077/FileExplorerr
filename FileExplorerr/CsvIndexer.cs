using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  GENERADOR DE ÍNDICE CSV
    //  Recorre un directorio raíz y produce un CSV con conteos por tipo
    //  de archivo para cada carpeta encontrada.
    // ════════════════════════════════════════════════════════════════════════
    internal static class CsvIndexer
    {
        // ── Extensiones por categoría ────────────────────────────────────────
        private static readonly string[] ExtImage = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".webp", ".tiff", ".svg" };
        private static readonly string[] ExtAudio = { ".mp3", ".wav", ".wma", ".m4a", ".flac", ".aac", ".ogg", ".opus" };
        private static readonly string[] ExtVideo = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".ts" };
        private static readonly string[] ExtText = { ".txt", ".csv", ".json", ".xml", ".log", ".ini", ".config",
                                                        ".md", ".cs", ".py", ".js", ".ts", ".html", ".css", ".yaml", ".yml" };

        // ── Punto de entrada asíncrono ───────────────────────────────────────
        /// <summary>
        /// Genera el contenido CSV completo recorriendo <paramref name="rootPath"/>
        /// de forma recursiva. Reporta la carpeta en proceso mediante
        /// <paramref name="progress"/>.
        /// </summary>
        public static Task<string> GenerateAsync(string rootPath, IProgress<string>? progress = null)
        {
            return Task.Run(() =>
            {
                var sb = new StringBuilder();

                // Cabecera
                sb.AppendLine(
                    "\"Ruta Carpeta\"," +
                    "\"Nombre Carpeta\"," +
                    "\"Nombre Archivo\"," +
                    "\"Extensión\"," +
                    "\"Tamaño\"," +
                    "\"Último Acceso\"");

                ProcessDirectory(rootPath, sb, progress);
                return sb.ToString();
            });
        }

        // ── Procesamiento recursivo 
        private static void ProcessDirectory(string path, StringBuilder sb, IProgress<string>? progress)
        {
            try
            {
                var di = new DirectoryInfo(path);
                progress?.Report(di.Name);

                var files = di.GetFiles()
                              .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                              .ToArray();

                var subdirs = di.GetDirectories()
                                .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                                .ToArray();

                // Una fila por archivo
                foreach (var f in files.OrderBy(x => x.Name))
                {
                    sb.AppendLine(
                        $"\"{Esc(di.FullName)}\"," +
                        $"\"{Esc(di.Name)}\"," +
                        $"\"{Esc(f.Name)}\"," +
                        $"\"{f.Extension.TrimStart('.').ToUpper()}\"," +
                        $"\"{FormatSize(f.Length)}\"," +
                        $"\"{f.LastWriteTime:dd/MM/yyyy HH:mm}\"");
                }

                // Si la carpeta está vacía igual se registra
                if (files.Length == 0)
                {
                    sb.AppendLine(
                        $"\"{Esc(di.FullName)}\"," +
                        $"\"{Esc(di.Name)}\"," +
                        "\"(vacía)\"," +
                        "\"\",\"\",\"\"");
                }

                foreach (var sub in subdirs.OrderBy(d => d.Name))
                {
                    try { ProcessDirectory(sub.FullName, sb, progress); }
                    catch { /* Sin acceso — continuar */ }
                }
            }
            catch { /* Sin acceso al directorio raíz — ignorar */ }
        }

        // ── Clasificación de archivos 
        internal static FileStats ClassifyFiles(FileInfo[] files)
        {
            int img = 0, aud = 0, vid = 0, txt = 0;
            foreach (var f in files)
            {
                string ext = f.Extension.ToLower();
                if (ExtImage.Contains(ext)) img++;
                else if (ExtAudio.Contains(ext)) aud++;
                else if (ExtVideo.Contains(ext)) vid++;
                else if (ExtText.Contains(ext)) txt++;
            }
            return new FileStats
            {
                Images = img,
                Audio = aud,
                Video = vid,
                Text = txt,
                Other = files.Length - img - aud - vid - txt
            };
        }

        // Versión que acepta sólo extensiones (más rápida para el panel)
        internal static FileStats ClassifyByExtensions(string[] extensions)
        {
            int img = 0, aud = 0, vid = 0, txt = 0;
            foreach (var rawExt in extensions)
            {
                string ext = rawExt.ToLower();
                if (ExtImage.Contains(ext)) img++;
                else if (ExtAudio.Contains(ext)) aud++;
                else if (ExtVideo.Contains(ext)) vid++;
                else if (ExtText.Contains(ext)) txt++;
            }
            return new FileStats
            {
                Images = img,
                Audio = aud,
                Video = vid,
                Text = txt,
                Other = extensions.Length - img - aud - vid - txt
            };
        }

        // ── Helper: formatear tamaño ─────────────────────────────────────
        private static string FormatSize(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }

        // ── Helper: escapar comillas en CSV ─────────────────────────────────
        private static string Esc(string s) => s.Replace("\"", "\"\"");
    }

    // ── DTO de estadísticas ──────────────────────────────────────────────────
    internal struct FileStats
    {
        public int Images, Audio, Video, Text, Other;
        public int Total => Images + Audio + Video + Text + Other;

        /// Representación compacta para la barra de estado
        public string ToStatusString(int folders)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (folders > 0) parts.Add($"📁 {folders} carpeta{(folders != 1 ? "s" : "")}");
            if (Total > 0) parts.Add($"📄 {Total} archivo{(Total != 1 ? "s" : "")}");
            if (Images > 0) parts.Add($"🖼️ {Images}");
            if (Audio > 0) parts.Add($"🎵 {Audio}");
            if (Video > 0) parts.Add($"🎬 {Video}");
            if (Text > 0) parts.Add($"📝 {Text}");
            if (Other > 0) parts.Add($"📦 {Other}");
            return string.Join("  ·  ", parts);
        }

        /// Representación compacta para la columna "Información" de carpetas
        public string ToInfoColumn(int subfolders)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (subfolders > 0) parts.Add($"{subfolders} sub");
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