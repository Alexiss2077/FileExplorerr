using System;
using System.Collections.Generic;
using System.Linq;

namespace FileExplorerr.Compression
{
    // ════════════════════════════════════════════════════════════════════════
    //  ARCHIVER FACTORY
    //  Resolves the correct IArchiver implementation for a given file
    //  extension.  Registration is done once at startup via Register().
    //
    //  Mirrors the design of OfficeExporterFactory in Services/Export/.
    //
    //  Usage:
    //      // In Program.Main() — alongside OfficeExporterFactory:
    //      ArchiverFactory.RegisterBuiltInArchivers();
    //
    //      // In call-sites (e.g. CompressionService):
    //      var archiver = ArchiverFactory.Resolve(".zip");
    //      var result   = await archiver.CompressAsync(opts, progress);
    //
    //  Thread-safety:
    //      Register() is NOT thread-safe and must only be called during
    //      application startup before any compression operation begins.
    //
    //  Extending for new formats (e.g. 7-Zip):
    //      1. Create SevenZipArchiver : IArchiver in Services/Compression/
    //      2. Add one line to RegisterBuiltInArchivers():
    //             Register(new SevenZipArchiver());
    //      That is the ONLY change required.
    // ════════════════════════════════════════════════════════════════════════
    internal static class ArchiverFactory
    {
        // ── Internal registry ─────────────────────────────────────────────
        //  Key   = extension in lower-case with leading dot (e.g. ".zip")
        //  Value = IArchiver instance (archivers are stateless; one instance is fine)
        private static readonly Dictionary<string, IArchiver> _archivers =
            new(StringComparer.OrdinalIgnoreCase);

        // ════════════════════════════════════════════════════════════════════
        //  REGISTRATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Registers <paramref name="archiver"/> for all its
        /// <see cref="IArchiver.SupportedExtensions"/>.
        /// If an archiver for the same extension is already registered it is
        /// replaced (allows overriding in tests or future upgrades).
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="archiver"/> is null.
        /// </exception>
        public static void Register(IArchiver archiver)
        {
            if (archiver is null) throw new ArgumentNullException(nameof(archiver));

            foreach (string ext in archiver.SupportedExtensions)
                _archivers[ext] = archiver;
        }

        /// <summary>
        /// Convenience overload — registers every archiver in the collection.
        /// </summary>
        public static void RegisterAll(IEnumerable<IArchiver> archivers)
        {
            foreach (var a in archivers) Register(a);
        }

        // ════════════════════════════════════════════════════════════════════
        //  RESOLUTION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the archiver registered for <paramref name="extension"/>
        /// (e.g. ".zip"), or <c>null</c> when none is registered.
        /// Allows the caller to decide whether to fall back gracefully or
        /// show an informative error.
        /// </summary>
        public static IArchiver? TryResolve(string extension)
        {
            _archivers.TryGetValue(extension, out var archiver);
            return archiver;
        }

        /// <summary>
        /// Returns the archiver for <paramref name="extension"/> or throws
        /// <see cref="NotSupportedException"/> when none is registered.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown when no archiver is registered for the given extension.
        /// </exception>
        public static IArchiver Resolve(string extension)
        {
            return TryResolve(extension)
                ?? throw new NotSupportedException(
                    $"No hay un compresor registrado para la extensión '{extension}'. " +
                    $"Formatos disponibles: {string.Join(", ", SupportedExtensions)}");
        }

        /// <summary>True when an archiver is registered for the given extension.</summary>
        public static bool IsSupported(string extension) =>
            _archivers.ContainsKey(extension);

        /// <summary>Read-only snapshot of all currently supported extensions.</summary>
        public static IReadOnlyList<string> SupportedExtensions =>
            _archivers.Keys.OrderBy(k => k).ToList().AsReadOnly();

        /// <summary>Read-only snapshot of all registered display names.</summary>
        public static IReadOnlyList<string> DisplayNames =>
            _archivers.Values
                      .Select(a => a.DisplayName)
                      .Distinct()
                      .OrderBy(n => n)
                      .ToList()
                      .AsReadOnly();

        // ════════════════════════════════════════════════════════════════════
        //  STARTUP HELPER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Registers all built-in archivers implemented so far.
        /// Call once from Program.Main() alongside
        /// OfficeExporterFactory.RegisterNativeExporters().
        ///
        ///  Phase A (implemented):
        ///    ZipArchiver  → .zip
        ///
        ///  Future phases — add one line each:
        ///    Register(new SevenZipArchiver());   // .7z
        ///    Register(new RarArchiver());        // .rar  (extract-only)
        ///    Register(new TarArchiver());        // .tar, .tar.gz, .tgz
        /// </summary>
        public static void RegisterBuiltInArchivers()
        {
            Register(new ZipArchiver());
            Register(new SharpCompressArchiver());   // .7z, .tar, .tar.gz, .tgz, .tar.bz2
            Register(new RarArchiver());             // .rar  (solo extracción)
        }
    }
}