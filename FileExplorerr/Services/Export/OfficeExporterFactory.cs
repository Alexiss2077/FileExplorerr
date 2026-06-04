using System;
using System.Collections.Generic;
using System.Linq;

namespace FileExplorerr.Export
{
    // ════════════════════════════════════════════════════════════════════════
    //  OFFICE EXPORTER FACTORY
    //  Resolves the correct IOfficeExporter implementation for a given
    //  file extension.  Registration is done once at startup via Register().
    //
    //  Usage:
    //      // In application startup (or lazily on first use):
    //      OfficeExporterFactory.Register(new ExcelExporter());
    //      OfficeExporterFactory.Register(new WordExporter());
    //      OfficeExporterFactory.Register(new PowerPointExporter()); // Phase 4
    //      OfficeExporterFactory.Register(new PdfExporter());        // Phase 5
    //
    //      // In call-sites:
    //      var exporter = OfficeExporterFactory.Resolve(".xlsx");
    //      var result   = await exporter.ExportAsync(dt, opts, progress);
    //
    //  Thread-safety: Register() is not thread-safe and should only be called
    //  during application startup, before any exports begin.
    // ════════════════════════════════════════════════════════════════════════
    public static class OfficeExporterFactory
    {
        private static readonly Dictionary<string, IOfficeExporter> _exporters =
            new(StringComparer.OrdinalIgnoreCase);

        // ── Registration ──────────────────────────────────────────────────

        /// <summary>
        /// Registers an exporter.  If an exporter for the same extension is
        /// already registered it is replaced (allows overriding in tests).
        /// </summary>
        public static void Register(IOfficeExporter exporter)
        {
            if (exporter is null) throw new ArgumentNullException(nameof(exporter));
            _exporters[exporter.SupportedExtension] = exporter;
        }

        /// <summary>
        /// Registers all exporters in the provided collection.
        /// Convenience overload for bulk startup registration.
        /// </summary>
        public static void RegisterAll(IEnumerable<IOfficeExporter> exporters)
        {
            foreach (var e in exporters)
                Register(e);
        }

        // ── Resolution ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the exporter registered for <paramref name="extension"/>
        /// (e.g. ".xlsx").  Returns null when no exporter is registered for
        /// that extension, allowing the caller to decide whether to fall back
        /// to the Python pipeline or show an error.
        /// </summary>
        public static IOfficeExporter? TryResolve(string extension)
        {
            _exporters.TryGetValue(extension, out var exporter);
            return exporter;
        }

        /// <summary>
        /// Returns the exporter for <paramref name="extension"/> or throws
        /// <see cref="NotSupportedException"/> when none is registered.
        /// </summary>
        public static IOfficeExporter Resolve(string extension)
        {
            return TryResolve(extension)
                ?? throw new NotSupportedException(
                    $"No hay un exportador registrado para la extension '{extension}'. " +
                    $"Extensiones disponibles: {string.Join(", ", SupportedExtensions)}");
        }

        /// <summary>True when an exporter is registered for the given extension.</summary>
        public static bool IsSupported(string extension) =>
            _exporters.ContainsKey(extension);

        /// <summary>Snapshot of all currently registered extensions.</summary>
        public static IReadOnlyList<string> SupportedExtensions =>
            _exporters.Keys.ToList().AsReadOnly();

        // ── Startup helper ────────────────────────────────────────────────

        /// <summary>
        /// Registers all native C# exporters implemented so far.
        /// Called once from Program.Main() before Application.Run().
        ///
        /// Phases:
        ///   Phase 2 (done) — ExcelExporter (.xlsx)
        ///   Phase 3 (done) — WordExporter  (.docx)
        ///   Phase 4 (done) — PowerPointExporter (.pptx)
        ///   Phase 5 (TODO) — PdfExporter (.pdf)  + delete export_office.py
        /// </summary>
        public static void RegisterNativeExporters()
        {
            Register(new ExcelExporter());
            Register(new WordExporter());
            Register(new PowerPointExporter());
            // Phase 5: Register(new PdfExporter());
        }
    }
}