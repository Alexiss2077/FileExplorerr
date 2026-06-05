using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace FileExplorerr.Compression
{
    // ════════════════════════════════════════════════════════════════════════
    //  ZIP ARCHIVER
    //  Implements IArchiver for .zip files using System.IO.Compression from
    //  the .NET 8 BCL — no external NuGet packages required.
    //
    //  Key behaviours:
    //    Compression:
    //      - Accepts a mix of files and directories.
    //      - Directories are traversed recursively; empty directories are
    //        stored as zero-byte entries with a trailing slash.
    //      - Entry paths inside the archive use forward slashes and are
    //        always relative — no absolute paths leak into the ZIP.
    //      - Progress is reported by bytes written vs. total source bytes.
    //      - The partial output file is deleted on failure or cancellation.
    //
    //    Extraction:
    //      - SECURITY: every entry path is validated against the destination
    //        folder BEFORE any byte is written to disk (Zip Slip prevention).
    //      - FlattenSingleRootFolder: when true and the ZIP contains a single
    //        root folder, that folder is stripped so files land directly in
    //        DestinationFolder — identical to "Extract Here" in WinRAR/7-Zip.
    //      - Entries that conflict with existing files are skipped or
    //        overwritten according to ArchiveOptions.OverwriteExisting.
    //      - Encoding note: System.IO.Compression uses UTF-8 (.NET 8 default).
    //        Legacy ZIPs with CP437/Latin-1 names may display garbled chars.
    //
    //  Supported extensions: .zip
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class ZipArchiver : IArchiver
    {
        // ── IArchiver identity ────────────────────────────────────────────

        public string DisplayName => "ZIP";

        public IReadOnlyList<string> SupportedExtensions { get; } =
            new[] { ".zip" };

        // ════════════════════════════════════════════════════════════════════
        //  COMPRESSION
        // ════════════════════════════════════════════════════════════════════

        public async Task<ArchiveResult> CompressAsync(
            ArchiveOptions options,
            IProgress<int>? progress,
            IProgress<string>? currentEntryName = null)
        {
            if (options is null) return ArchiveResult.Fail("ArchiveOptions es nulo.");
            if (options.SourcePaths.Count == 0)
                return ArchiveResult.Fail("No hay archivos ni carpetas que comprimir.");

            string? outputDir = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            TryDeleteFile(options.OutputPath);

            try
            {
                progress?.Report(2);

                var entries = await Task.Run(
                    () => EnumerateEntries(options.SourcePaths, options.IncludeBaseDirectory),
                    options.CancellationToken);

                long totalBytes = entries
                    .Where(e => !e.IsDirectory)
                    .Sum(e =>
                    {
                        try { return new FileInfo(e.SourcePath).Length; }
                        catch { return 0L; }
                    });

                long writtenBytes = 0L;
                int filesAdded = 0;

                await Task.Run(() =>
                {
                    using var archive = ZipFile.Open(options.OutputPath, ZipArchiveMode.Create);

                    foreach (var entry in entries)
                    {
                        options.CancellationToken.ThrowIfCancellationRequested();
                        currentEntryName?.Report(entry.EntryName);

                        if (entry.IsDirectory)
                        {
                            string dirEntry = entry.EntryName.TrimEnd('/') + "/";
                            archive.CreateEntry(dirEntry, options.Level);
                            continue;
                        }

                        var zipEntry = archive.CreateEntry(entry.EntryName, options.Level);

                        try
                        {
                            zipEntry.LastWriteTime = new FileInfo(entry.SourcePath).LastWriteTime;
                        }
                        catch { /* non-fatal */ }

                        using var srcStream = new FileStream(
                            entry.SourcePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite,
                            bufferSize: 65_536,
                            useAsync: false);

                        using var dstStream = zipEntry.Open();

                        var buffer = new byte[65_536];
                        int bytesRead;
                        while ((bytesRead = srcStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            options.CancellationToken.ThrowIfCancellationRequested();
                            dstStream.Write(buffer, 0, bytesRead);

                            writtenBytes += bytesRead;
                            if (totalBytes > 0)
                            {
                                int pct = 5 + (int)(90.0 * writtenBytes / totalBytes);
                                progress?.Report(Math.Min(pct, 95));
                            }
                        }

                        filesAdded++;
                    }

                }, options.CancellationToken);

                progress?.Report(100);
                return ArchiveResult.CompressOk(options.OutputPath, filesAdded);
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(options.OutputPath);
                return ArchiveResult.Cancelled();
            }
            catch (Exception ex)
            {
                TryDeleteFile(options.OutputPath);
                System.Diagnostics.Debug.WriteLine($"[ZipArchiver.CompressAsync] {ex.Message}");
                return ArchiveResult.Fail(ex, BuildFriendlyCompressError(ex));
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  EXTRACTION
        // ════════════════════════════════════════════════════════════════════

        public async Task<ArchiveResult> ExtractAsync(
            ArchiveOptions options,
            IProgress<int>? progress,
            IProgress<string>? currentEntryName = null)
        {
            if (options is null) return ArchiveResult.Fail("ArchiveOptions es nulo.");
            if (!File.Exists(options.ArchivePath))
                return ArchiveResult.Fail($"El archivo no existe:\n{options.ArchivePath}");

            var skipped = new List<string>();

            try
            {
                string destination = Path.GetFullPath(options.DestinationFolder);
                Directory.CreateDirectory(destination);

                int filesExtracted = 0;

                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(options.ArchivePath);

                    // ── Detectar carpeta raíz única (FlattenSingleRootFolder) ──
                    string rootPrefix = string.Empty;

                    if (options.FlattenSingleRootFolder)
                    {
                        var roots = archive.Entries
                            .Select(e => e.FullName.Split('/')[0])
                            .Where(r => !string.IsNullOrEmpty(r))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        // Solo aplanamos cuando hay exactamente una carpeta raíz
                        // y al menos una entrada tiene contenido bajo ella.
                        if (roots.Count == 1)
                            rootPrefix = roots[0] + "/";
                    }

                    int total = archive.Entries.Count;
                    int processed = 0;

                    foreach (var entry in archive.Entries)
                    {
                        options.CancellationToken.ThrowIfCancellationRequested();

                        processed++;
                        int pct = 5 + (int)(90.0 * processed / Math.Max(total, 1));
                        progress?.Report(Math.Min(pct, 95));
                        currentEntryName?.Report(entry.FullName);

                        // ── Calcular ruta relativa (con flatten si aplica) ──────
                        string entryRelative = entry.FullName;

                        if (!string.IsNullOrEmpty(rootPrefix) &&
                            entryRelative.StartsWith(rootPrefix,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            entryRelative = entryRelative.Substring(rootPrefix.Length);
                        }

                        // Si después de quitar el prefijo no queda nada es la
                        // carpeta raíz misma → no crear nada, continuar.
                        if (string.IsNullOrEmpty(entryRelative)) continue;

                        // ── Security: Zip Slip prevention ─────────────────────
                        string entryDestPath = Path.GetFullPath(
                            Path.Combine(destination, entryRelative));

                        bool isInsideDest =
                            entryDestPath.StartsWith(
                                destination + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase)
                            || entryDestPath.Equals(
                                destination,
                                StringComparison.OrdinalIgnoreCase);

                        if (!isInsideDest)
                        {
                            skipped.Add($"[SEGURIDAD] {entry.FullName}");
                            System.Diagnostics.Debug.WriteLine(
                                $"[ZipArchiver] Zip Slip bloqueado: {entry.FullName}");
                            continue;
                        }

                        // ── Entrada de directorio ──────────────────────────────
                        bool isDirectoryEntry =
                            string.IsNullOrEmpty(entry.Name) ||
                            entry.FullName.EndsWith('/') ||
                            entry.FullName.EndsWith('\\');

                        if (isDirectoryEntry)
                        {
                            Directory.CreateDirectory(entryDestPath);
                            continue;
                        }

                        // ── Asegurar que exista el directorio padre ─────────────
                        string? parentDir = Path.GetDirectoryName(entryDestPath);
                        if (!string.IsNullOrEmpty(parentDir))
                            Directory.CreateDirectory(parentDir);

                        // ── Manejo de conflictos ───────────────────────────────
                        if (File.Exists(entryDestPath))
                        {
                            if (!options.OverwriteExisting)
                            {
                                skipped.Add(entry.FullName);
                                continue;
                            }
                            File.SetAttributes(entryDestPath, FileAttributes.Normal);
                            File.Delete(entryDestPath);
                        }

                        // ── Extraer por streaming ──────────────────────────────
                        using var srcStream = entry.Open();
                        using var dstStream = new FileStream(
                            entryDestPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            bufferSize: 65_536,
                            useAsync: false);

                        var buffer = new byte[65_536];
                        int bytesRead;
                        while ((bytesRead = srcStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            options.CancellationToken.ThrowIfCancellationRequested();
                            dstStream.Write(buffer, 0, bytesRead);
                        }

                        // Restaurar timestamp original
                        try
                        {
                            File.SetLastWriteTime(
                                entryDestPath,
                                entry.LastWriteTime.LocalDateTime);
                        }
                        catch { /* non-fatal */ }

                        filesExtracted++;
                    }

                }, options.CancellationToken);

                progress?.Report(100);
                return ArchiveResult.ExtractOk(options.DestinationFolder, filesExtracted, skipped);
            }
            catch (OperationCanceledException)
            {
                return ArchiveResult.Cancelled();
            }
            catch (InvalidDataException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZipArchiver.ExtractAsync] {ex.Message}");
                return ArchiveResult.Fail(ex, BuildFriendlyExtractError(ex));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZipArchiver.ExtractAsync] {ex.Message}");
                return ArchiveResult.Fail(ex, BuildFriendlyExtractError(ex));
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ENUMERACIÓN DE ENTRADAS (helper de compresión)
        // ════════════════════════════════════════════════════════════════════

        private static List<ArchiveEntry> EnumerateEntries(
            IReadOnlyList<string> sourcePaths,
            bool includeBaseDirectory)
        {
            var entries = new List<ArchiveEntry>();

            foreach (string sourcePath in sourcePaths)
            {
                if (File.Exists(sourcePath))
                {
                    entries.Add(new ArchiveEntry(
                        SourcePath: sourcePath,
                        EntryName: Path.GetFileName(sourcePath),
                        IsDirectory: false));
                }
                else if (Directory.Exists(sourcePath))
                {
                    string dirName = new DirectoryInfo(sourcePath).Name;
                    string prefix = includeBaseDirectory ? dirName + "/" : string.Empty;
                    EnumerateDirectory(sourcePath, prefix, entries);
                }
                // Silently skip paths that no longer exist (race condition).
            }

            return entries;
        }

        private static void EnumerateDirectory(
            string directoryPath,
            string entryPrefix,
            List<ArchiveEntry> entries)
        {
            entries.Add(new ArchiveEntry(
                SourcePath: directoryPath,
                EntryName: entryPrefix,
                IsDirectory: true));

            try
            {
                foreach (var file in Directory.EnumerateFiles(directoryPath))
                {
                    var fi = new FileInfo(file);
                    if ((fi.Attributes & FileAttributes.Hidden) != 0) continue;

                    entries.Add(new ArchiveEntry(
                        SourcePath: file,
                        EntryName: entryPrefix + fi.Name,
                        IsDirectory: false));
                }

                foreach (var sub in Directory.EnumerateDirectories(directoryPath))
                {
                    var di = new DirectoryInfo(sub);
                    if ((di.Attributes & FileAttributes.Hidden) != 0) continue;

                    EnumerateDirectory(sub, entryPrefix + di.Name + "/", entries);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip inaccesibles; continue with siblings.
            }
        }

        private readonly record struct ArchiveEntry(
            string SourcePath,
            string EntryName,
            bool IsDirectory);

        // ════════════════════════════════════════════════════════════════════
        //  MENSAJES DE ERROR AMIGABLES
        // ════════════════════════════════════════════════════════════════════

        private static string BuildFriendlyCompressError(Exception ex) =>
            ex switch
            {
                UnauthorizedAccessException =>
                    "No tienes permisos para leer uno o más archivos de origen.\n\n" +
                    $"Detalle: {ex.Message}",

                PathTooLongException =>
                    "Una ruta de archivo es demasiado larga para el sistema de archivos.\n\n" +
                    "Intenta comprimir desde una ubicación con ruta más corta.",

                IOException =>
                    "Error de disco al crear el archivo ZIP.\n\n" +
                    $"Detalle: {ex.Message}",

                _ =>
                    $"Error inesperado al comprimir:\n\n{ex.Message}"
            };

        private static string BuildFriendlyExtractError(Exception ex) =>
            ex switch
            {
                InvalidDataException =>
                    "El archivo ZIP está dañado, incompleto o protegido con contraseña.\n\n" +
                    "Nota: esta versión no soporta archivos ZIP cifrados con contraseña.\n\n" +
                    $"Detalle: {ex.Message}",

                UnauthorizedAccessException =>
                    "No tienes permisos para escribir en la carpeta de destino.\n\n" +
                    $"Detalle: {ex.Message}",

                PathTooLongException =>
                    "Una entrada dentro del ZIP genera una ruta demasiado larga.\n\n" +
                    "Prueba extraer en una carpeta raíz con ruta más corta (p.ej. C:\\Temp).",

                IOException =>
                    "Error de disco al extraer los archivos.\n\n" +
                    $"Detalle: {ex.Message}",

                _ =>
                    $"Error inesperado al extraer:\n\n{ex.Message}"
            };

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { /* non-fatal */ }
        }
    }
}