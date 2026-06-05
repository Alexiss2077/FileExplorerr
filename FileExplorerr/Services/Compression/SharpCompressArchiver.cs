using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Archives.SevenZip;

namespace FileExplorerr.Compression
{
    // ════════════════════════════════════════════════════════════════════════
    //  SHARPCOMPRESS ARCHIVER
    //  Implementa IArchiver para .7z, .tar, .tar.gz, .tgz, .tar.bz2
    //  usando SharpCompress 0.40.0+ (licencia MIT, CVE-2026-44788 corregido).
    //
    //  NOTA IMPORTANTE sobre 7z:
    //    SevenZipArchive.Create() fue eliminado/cambiado en 0.38+.
    //    Usamos WriterFactory.Open() con ArchiveType.SevenZip que es la
    //    API estable y recomendada en todas las versiones modernas.
    //
    //  Compresión:  .7z · .tar · .tar.gz · .tgz · .tar.bz2
    //  Extracción:  todos los anteriores
    //  RAR:         manejado en RarArchiver.cs
    //
    //  NuGet requerido:
    //      <PackageReference Include="SharpCompress" Version="0.40.0" />
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class SharpCompressArchiver : IArchiver
    {
        // ── IArchiver identity ────────────────────────────────────────────

        public string DisplayName => "7z / TAR";

        public IReadOnlyList<string> SupportedExtensions { get; } =
            new[] { ".7z", ".tar", ".tar.gz", ".tgz", ".tar.bz2" };

        // ════════════════════════════════════════════════════════════════════
        //  COMPRESIÓN
        // ════════════════════════════════════════════════════════════════════

        public async Task<ArchiveResult> CompressAsync(
            ArchiveOptions options,
            IProgress<int>? progress,
            IProgress<string>? currentEntryName = null)
        {
            if (options is null) return ArchiveResult.Fail("ArchiveOptions es nulo.");
            if (options.SourcePaths.Count == 0)
                return ArchiveResult.Fail("No hay archivos ni carpetas que comprimir.");

            string outputLower = options.OutputPath.ToLowerInvariant();
            bool isTarGz = outputLower.EndsWith(".tar.gz") || outputLower.EndsWith(".tgz");
            bool isTarBz2 = outputLower.EndsWith(".tar.bz2");
            bool isTar = outputLower.EndsWith(".tar") && !isTarGz && !isTarBz2;
            bool is7z = outputLower.EndsWith(".7z");

            // Determinar ArchiveType y CompressionType
            ArchiveType archiveType;
            CompressionType compressionType;

            if (is7z)
            {
                archiveType = ArchiveType.SevenZip;
                compressionType = CompressionType.LZMA;
            }
            else if (isTarGz)
            {
                archiveType = ArchiveType.Tar;
                compressionType = CompressionType.GZip;
            }
            else if (isTarBz2)
            {
                archiveType = ArchiveType.Tar;
                compressionType = CompressionType.BZip2;
            }
            else if (isTar)
            {
                archiveType = ArchiveType.Tar;
                compressionType = CompressionType.None;
            }
            else
            {
                return ArchiveResult.Fail(
                    $"Formato no soportado para compresión: " +
                    $"{Path.GetExtension(options.OutputPath)}\n\n" +
                    "Formatos disponibles: .7z, .tar, .tar.gz, .tgz, .tar.bz2");
            }

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
                    using var outStream = File.Create(options.OutputPath);
                    using var writer = WriterFactory.OpenWriter(
                        outStream,
                        archiveType,
                        new WriterOptions(compressionType)
                        {
                            LeaveStreamOpen = false
                        });

                    foreach (var entry in entries.Where(e => !e.IsDirectory))
                    {
                        options.CancellationToken.ThrowIfCancellationRequested();
                        currentEntryName?.Report(entry.EntryName);

                        writer.Write(entry.EntryName, entry.SourcePath);

                        try { writtenBytes += new FileInfo(entry.SourcePath).Length; }
                        catch { /* non-fatal */ }

                        if (totalBytes > 0)
                            progress?.Report(5 + (int)(90.0 * writtenBytes / totalBytes));

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
                System.Diagnostics.Debug.WriteLine(
                    $"[SharpCompressArchiver.CompressAsync] {ex.Message}");
                return ArchiveResult.Fail(ex, BuildFriendlyCompressError(ex));
            }
        }





        // ════════════════════════════════════════════════════════════════════
        //  EXTRACCIÓN
        // ════════════════════════════════════════════════════════════════════

        public async Task<ArchiveResult> ExtractAsync(

            ArchiveOptions options,
            IProgress<int>? progress,
            IProgress<string>? currentEntryName = null)
        {
            if (options is null) return ArchiveResult.Fail("ArchiveOptions es nulo.");
            if (!File.Exists(options.ArchivePath))
                return ArchiveResult.Fail($"El archivo no existe:\n{options.ArchivePath}");

            
            string lowerPath = options.ArchivePath.ToLowerInvariant();
            if (lowerPath.EndsWith(".7z"))
                return await Extract7zAsync(options, progress, currentEntryName);


            var skipped = new List<string>();

            try
            {
                string destination = Path.GetFullPath(options.DestinationFolder);
                Directory.CreateDirectory(destination);

                int filesExtracted = 0;

                await Task.Run(() =>
                {
                    // ── Detectar prefijo raíz único (FlattenSingleRootFolder) ──
                    string rootPrefix = string.Empty;

                    if (options.FlattenSingleRootFolder)
                    {
                        using var peekStream = File.OpenRead(options.ArchivePath);
                        using var peekReader = ReaderFactory.OpenReader(peekStream);

                        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        while (peekReader.MoveToNextEntry())
                        {
                            if (peekReader.Entry.IsDirectory) continue;
                            string firstSegment = (peekReader.Entry.Key ?? string.Empty)
                                .Replace('\\', '/')
                                .Split('/')[0];
                            if (!string.IsNullOrEmpty(firstSegment))
                                roots.Add(firstSegment);
                        }

                        if (roots.Count == 1)
                            rootPrefix = roots.First() + "/";
                    }



                    // ── Extracción real ────────────────────────────────────
                    using var stream = File.OpenRead(options.ArchivePath);
                    using var reader = ReaderFactory.OpenReader(stream);

                    int processed = 0;

                    while (reader.MoveToNextEntry())
                    {
                        options.CancellationToken.ThrowIfCancellationRequested();

                        processed++;
                        progress?.Report(Math.Min(5 + processed * 2, 95));

                        string entryKey = (reader.Entry.Key ?? string.Empty)
                            .Replace('\\', '/');

                        currentEntryName?.Report(entryKey);

                        // ── Aplanar carpeta raíz si aplica ─────────────────
                        string entryRelative = entryKey;
                        if (!string.IsNullOrEmpty(rootPrefix) &&
                            entryRelative.StartsWith(rootPrefix,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            entryRelative = entryRelative.Substring(rootPrefix.Length);
                        }

                        if (string.IsNullOrEmpty(entryRelative)) continue;

                        // ── Security: path traversal prevention ────────────
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
                            skipped.Add($"[SEGURIDAD] {entryKey}");
                            System.Diagnostics.Debug.WriteLine(
                                $"[SharpCompressArchiver] Path traversal bloqueado: {entryKey}");
                            continue;
                        }

                        // ── Entrada de directorio ──────────────────────────
                        if (reader.Entry.IsDirectory)
                        {
                            Directory.CreateDirectory(entryDestPath);
                            continue;
                        }

                        // ── Directorio padre ───────────────────────────────
                        string? parentDir = Path.GetDirectoryName(entryDestPath);
                        if (!string.IsNullOrEmpty(parentDir))
                            Directory.CreateDirectory(parentDir);

                        // ── Conflicto de nombre ────────────────────────────
                        if (File.Exists(entryDestPath))
                        {
                            if (!options.OverwriteExisting)
                            {
                                skipped.Add(entryKey);
                                continue;
                            }
                            File.SetAttributes(entryDestPath, FileAttributes.Normal);
                            File.Delete(entryDestPath);
                        }

                        // ── Extraer entrada ────────────────────────────────
                        using var dstStream = new FileStream(
                            entryDestPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            65_536,
                            useAsync: false);

                        reader.WriteEntryTo(dstStream);

                        // Restaurar timestamp
                        try
                        {
                            if (reader.Entry.LastModifiedTime.HasValue)
                                File.SetLastWriteTime(
                                    entryDestPath,
                                    reader.Entry.LastModifiedTime.Value);
                        }
                        catch { /* non-fatal */ }

                        filesExtracted++;
                    }

                }, options.CancellationToken);

                progress?.Report(100);
                return ArchiveResult.ExtractOk(
                    options.DestinationFolder, filesExtracted, skipped);
            }
            catch (OperationCanceledException)
            {
                return ArchiveResult.Cancelled();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SharpCompressArchiver.ExtractAsync] {ex.Message}");
                return ArchiveResult.Fail(ex, BuildFriendlyExtractError(ex));
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ENUMERACIÓN DE ENTRADAS
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
            }

            return entries;
        }

        private static void EnumerateDirectory(
            string directoryPath,
            string entryPrefix,
            List<ArchiveEntry> entries)
        {
          

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
            catch (UnauthorizedAccessException) { /* skip inaccessible */ }
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
                NotSupportedException =>
                    ex.Message,
                UnauthorizedAccessException =>
                    "No tienes permisos para leer uno o más archivos de origen.\n\n" +
                    $"Detalle: {ex.Message}",
                IOException =>
                    "Error de disco al crear el archivo.\n\n" +
                    $"Detalle: {ex.Message}",
                _ =>
                    $"Error inesperado al comprimir:\n\n{ex.Message}"
            };

        private static string BuildFriendlyExtractError(Exception ex) =>
            ex switch
            {
                UnauthorizedAccessException =>
                    "No tienes permisos para escribir en la carpeta de destino.\n\n" +
                    $"Detalle: {ex.Message}",
                IOException =>
                    "Error de disco al extraer los archivos.\n\n" +
                    $"Detalle: {ex.Message}",
                _ =>
                    $"Error inesperado al extraer:\n\n{ex.Message}"
            };

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { /* non-fatal */ }
        }

        private static async Task<ArchiveResult> Extract7zAsync(
    ArchiveOptions options,
    IProgress<int>? progress,
    IProgress<string>? currentEntryName)
        {
            var skipped = new List<string>();

            try
            {
                string destination = Path.GetFullPath(options.DestinationFolder);
                Directory.CreateDirectory(destination);
                int filesExtracted = 0;

                await Task.Run(() =>
                {
                    using var archive = SharpCompress.Archives.SevenZip.SevenZipArchive.OpenArchive(
                        options.ArchivePath);

                    // Detectar rootPrefix para FlattenSingleRootFolder
                    string rootPrefix = string.Empty;
                    if (options.FlattenSingleRootFolder)
                    {
                        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var e in archive.Entries.Where(e => !e.IsDirectory))
                        {
                            string first = (e.Key ?? string.Empty)
                                .Replace('\\', '/').Split('/')[0];
                            if (!string.IsNullOrEmpty(first)) roots.Add(first);
                        }
                        if (roots.Count == 1) rootPrefix = roots.First() + "/";
                    }

                    var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                    int total = entries.Count;
                    int processed = 0;

                    foreach (var entry in entries)
                    {
                        options.CancellationToken.ThrowIfCancellationRequested();

                        string entryKey = (entry.Key ?? string.Empty).Replace('\\', '/');
                        currentEntryName?.Report(entryKey);

                        string entryRelative = entryKey;
                        if (!string.IsNullOrEmpty(rootPrefix) &&
                            entryRelative.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                            entryRelative = entryRelative.Substring(rootPrefix.Length);

                        if (string.IsNullOrEmpty(entryRelative)) continue;

                        string entryDestPath = Path.GetFullPath(
                            Path.Combine(destination, entryRelative));

                        bool isInside =
                            entryDestPath.StartsWith(
                                destination + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase)
                            || entryDestPath.Equals(destination,
                                StringComparison.OrdinalIgnoreCase);

                        if (!isInside)
                        {
                            skipped.Add($"[SEGURIDAD] {entryKey}");
                            continue;
                        }

                        string? parentDir = Path.GetDirectoryName(entryDestPath);
                        if (!string.IsNullOrEmpty(parentDir))
                            Directory.CreateDirectory(parentDir);

                        if (File.Exists(entryDestPath))
                        {
                            if (!options.OverwriteExisting) { skipped.Add(entryKey); continue; }
                            File.SetAttributes(entryDestPath, FileAttributes.Normal);
                            File.Delete(entryDestPath);
                        }

                        using var dst = new FileStream(entryDestPath, FileMode.Create,
                            FileAccess.Write, FileShare.None, 65_536, useAsync: false);
                        entry.OpenEntryStream().CopyTo(dst);

                        try
                        {
                            if (entry.LastModifiedTime.HasValue)
                                File.SetLastWriteTime(entryDestPath, entry.LastModifiedTime.Value);
                        }
                        catch { /* non-fatal */ }

                        filesExtracted++;
                        processed++;
                        if (total > 0)
                            progress?.Report(5 + (int)(90.0 * processed / total));
                    }

                }, options.CancellationToken);

                progress?.Report(100);
                return ArchiveResult.ExtractOk(options.DestinationFolder, filesExtracted, skipped);
            }
            catch (OperationCanceledException) { return ArchiveResult.Cancelled(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SharpCompressArchiver.Extract7zAsync] {ex.Message}");
                return ArchiveResult.Fail(ex, BuildFriendlyExtractError(ex));
            }
        }
    }
}
