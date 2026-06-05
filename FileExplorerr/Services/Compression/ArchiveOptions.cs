using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading;

namespace FileExplorerr.Compression
{
    // ════════════════════════════════════════════════════════════════════════
    //  ARCHIVE OPTIONS
    //  Immutable DTO con fluent builder para todos los parámetros de una
    //  operación de compresión o extracción.
    //
    //  Uso — compresión:
    //      var opts = ArchiveOptions.ForCompression(sourcePaths, outputPath)
    //                               .WithLevel(CompressionLevel.Optimal)
    //                               .WithCancellation(cts.Token)
    //                               .Build();
    //
    //  Uso — extracción normal ("Extraer en..."):
    //      var opts = ArchiveOptions.ForExtraction(archivePath, destFolder)
    //                               .WithOverwrite(false)
    //                               .WithCancellation(cts.Token)
    //                               .Build();
    //
    //  Uso — "Extraer aquí" (aplana la carpeta raíz única del ZIP):
    //      var opts = ArchiveOptions.ForExtraction(archivePath, destFolder)
    //                               .WithFlattenSingleRoot()
    //                               .WithCancellation(cts.Token)
    //                               .Build();
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class ArchiveOptions
    {
        // ── Compartido ────────────────────────────────────────────────────

        /// <summary>Token de cancelación respetado por todos los archivers.</summary>
        public CancellationToken CancellationToken { get; }

        // ── Parámetros de compresión ───────────────────────────────────────

        /// <summary>
        /// Rutas completas de archivos y/o directorios a comprimir.
        /// Los directorios se recorren recursivamente.
        /// Vacío cuando el set de opciones es solo para extracción.
        /// </summary>
        public IReadOnlyList<string> SourcePaths { get; }

        /// <summary>
        /// Ruta completa del archivo a crear, incluyendo extensión.
        /// Vacío cuando el set de opciones es solo para extracción.
        /// </summary>
        public string OutputPath { get; }

        /// <summary>
        /// Nivel de compresión aplicado a cada entrada.
        /// Por defecto: <see cref="CompressionLevel.Optimal"/>.
        /// </summary>
        public CompressionLevel Level { get; }

        /// <summary>
        /// Cuando true y se comprime un directorio, ese directorio se incluye
        /// como entrada raíz en el ZIP para recrear la carpeta al extraer.
        /// Por defecto: true.
        /// </summary>
        public bool IncludeBaseDirectory { get; }

        // ── Parámetros de extracción ───────────────────────────────────────

        /// <summary>
        /// Ruta completa del archivo ZIP a extraer.
        /// Vacío cuando el set de opciones es solo para compresión.
        /// </summary>
        public string ArchivePath { get; }

        /// <summary>
        /// Carpeta de destino donde se extraerán los archivos.
        /// Se crea automáticamente si no existe.
        /// </summary>
        public string DestinationFolder { get; }

        /// <summary>
        /// Cuando true, los archivos existentes en el destino se sobreescriben.
        /// Cuando false, los conflictos se omiten y se registran en
        /// <see cref="ArchiveResult.SkippedFiles"/>.
        /// Por defecto: false (seguro por defecto).
        /// </summary>
        public bool OverwriteExisting { get; }

        /// <summary>
        /// Cuando true y el ZIP contiene una única carpeta raíz, esa carpeta
        /// se omite y los archivos se extraen directamente en DestinationFolder.
        /// Comportamiento de "Extraer aquí" idéntico al de WinRAR y 7-Zip.
        /// Por defecto: false.
        /// </summary>
        public bool FlattenSingleRootFolder { get; }

        // ── Constructor privado — usar el builder ──────────────────────────

        private ArchiveOptions(Builder b)
        {
            CancellationToken = b.CancellationToken;
            SourcePaths = b.SourcePaths.AsReadOnly();
            OutputPath = b.OutputPath;
            Level = b.Level;
            IncludeBaseDirectory = b.IncludeBaseDirectory;
            ArchivePath = b.ArchivePath;
            DestinationFolder = b.DestinationFolder;
            OverwriteExisting = b.OverwriteExisting;
            FlattenSingleRootFolder = b.FlattenSingleRootFolder;
        }

        // ════════════════════════════════════════════════════════════════════
        //  FLUENT BUILDER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Crea un builder preconfigurado para COMPRIMIR.</summary>
        public static Builder ForCompression(IEnumerable<string> sourcePaths, string outputPath)
        {
            if (sourcePaths is null) throw new ArgumentNullException(nameof(sourcePaths));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path must not be empty.", nameof(outputPath));

            var b = new Builder { OutputPath = outputPath };
            b.SourcePaths.AddRange(sourcePaths);
            return b;
        }

        /// <summary>Crea un builder preconfigurado para EXTRAER.</summary>
        public static Builder ForExtraction(string archivePath, string destinationFolder)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("Archive path must not be empty.", nameof(archivePath));
            if (string.IsNullOrWhiteSpace(destinationFolder))
                throw new ArgumentException("Destination folder must not be empty.", nameof(destinationFolder));

            return new Builder
            {
                ArchivePath = archivePath,
                DestinationFolder = destinationFolder
            };
        }

        // ── Clase Builder ──────────────────────────────────────────────────

        internal sealed class Builder
        {
            // Compartido
            internal CancellationToken CancellationToken = CancellationToken.None;

            // Compresión
            internal List<string> SourcePaths = new();
            internal string OutputPath = string.Empty;
            internal CompressionLevel Level = CompressionLevel.Optimal;
            internal bool IncludeBaseDirectory = true;

            // Extracción
            internal string ArchivePath = string.Empty;
            internal string DestinationFolder = string.Empty;
            internal bool OverwriteExisting = false;
            internal bool FlattenSingleRootFolder = false;

            internal Builder() { }

            public Builder WithCancellation(CancellationToken ct)
            {
                CancellationToken = ct;
                return this;
            }

            public Builder WithLevel(CompressionLevel level)
            {
                Level = level;
                return this;
            }

            /// <summary>
            /// La carpeta raíz del directorio origen NO se añade como primera
            /// entrada — todos los archivos quedan en la raíz del ZIP.
            /// </summary>
            public Builder WithoutBaseDirectory()
            {
                IncludeBaseDirectory = false;
                return this;
            }

            public Builder WithOverwrite(bool overwrite)
            {
                OverwriteExisting = overwrite;
                return this;
            }

            /// <summary>
            /// Activa el modo "Extraer aquí": si el ZIP tiene una sola carpeta
            /// raíz, su contenido se extrae directamente en DestinationFolder
            /// sin crear esa carpeta intermedia.
            /// </summary>
            public Builder WithFlattenSingleRoot()
            {
                FlattenSingleRootFolder = true;
                return this;
            }

            public ArchiveOptions Build() => new ArchiveOptions(this);
        }
    }
}