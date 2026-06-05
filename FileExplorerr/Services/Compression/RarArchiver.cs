using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Readers;

namespace FileExplorerr.Compression
{
    // ════════════════════════════════════════════════════════════════════════
    //  RAR ARCHIVER  —  SOLO EXTRACCIÓN
    //  Implementa IArchiver para archivos .rar y .rar5 usando SharpCompress.
    //
    //  LIMITACIÓN CONOCIDA Y ESPERADA:
    //    CompressAsync siempre retorna ArchiveResult.Fail() con un mensaje
    //    explicativo. El formato RAR es propietario de RarLab; no existe
    //    ninguna librería .NET gratuita y legal que permita CREAR archivos RAR.
    //    SharpCompress solo puede LEER RAR, no escribirlo.
    //
    //  ExtractAsync soporta:
    //    - RAR 4.x  (.rar)
    //    - RAR 5.x  (.rar5 / .rar con cabecera RAR5)
    //    - Archivos multi-volumen (si todos los volúmenes están en la misma carpeta)
    //    - FlattenSingleRootFolder (mismo comportamiento que ZipArchiver)
    //    - Zip Slip prevention
    //    - Manejo de conflictos (OverwriteExisting)
    //
    //  NuGet requerido:
    //      dotnet add package SharpCompress
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class RarArchiver : IArchiver
    {
        // ── IArchiver identity ────────────────────────────────────────────

        public string DisplayName => "RAR";

        public IReadOnlyList<string> SupportedExtensions { get; } =
            new[] { ".rar" };

        // ════════════════════════════════════════════════════════════════════
        //  COMPRESIÓN — no soportada
        // ════════════════════════════════════════════════════════════════════

        public Task<ArchiveResult> CompressAsync(
            ArchiveOptions options,
            IProgress<int>? progress,
            IProgress<string>? currentEntryName = null)
        {
            return Task.FromResult(ArchiveResult.Fail(
                "El formato RAR no permite crear archivos desde esta aplicación.\n\n" +
                "El formato RAR es propietario de RarLab y no existe una librería\n" +
                "gratuita que permita crearlo.\n\n" +
                "Alternativas disponibles:\n" +
                "  • ZIP  — compatible con todo, sin instalar nada extra\n" +
                "  • 7z   — mejor compresión que RAR, gratuito y abierto"));
        }

        // ════════════════════════════════════════════════════════════════════
        //  EXTRACCIÓN — soportada via SharpCompress
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
                        int pct = Math.Min(5 + processed * 2, 95);
                        progress?.Report(pct);

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
                                $"[RarArchiver] Path traversal bloqueado: {entryKey}");
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
            catch (InvalidOperationException ex)
                when (ex.Message.Contains("password") ||
                      ex.Message.Contains("encrypted"))
            {
                System.Diagnostics.Debug.WriteLine($"[RarArchiver.ExtractAsync] {ex.Message}");
                return ArchiveResult.Fail(
                    "El archivo RAR está protegido con contraseña.\n\n" +
                    "Esta versión no soporta archivos RAR cifrados con contraseña.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RarArchiver.ExtractAsync] {ex.Message}");
                return ArchiveResult.Fail(ex, BuildFriendlyExtractError(ex));
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  MENSAJES DE ERROR AMIGABLES
        // ════════════════════════════════════════════════════════════════════

        private static string BuildFriendlyExtractError(Exception ex) =>
            ex switch
            {
                UnauthorizedAccessException =>
                    "No tienes permisos para escribir en la carpeta de destino.\n\n" +
                    $"Detalle: {ex.Message}",

                PathTooLongException =>
                    "Una entrada dentro del RAR genera una ruta demasiado larga.\n\n" +
                    "Prueba extraer en una carpeta raíz con ruta más corta (p.ej. C:\\Temp).",

                IOException =>
                    "Error de disco al extraer los archivos.\n\n" +
                    $"Detalle: {ex.Message}",

                _ =>
                    $"Error inesperado al extraer el archivo RAR:\n\n{ex.Message}"
            };
    }
}