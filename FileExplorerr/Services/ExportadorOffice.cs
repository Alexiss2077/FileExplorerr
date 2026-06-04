// ============================================================================
//  ExportadorOffice.cs  (Phase 2 — partial migration)
//
//  Public API is UNCHANGED.  Internal routing logic:
//
//    .xlsx  → ExcelExporter    (C# / ClosedXML — Phase 2)
//    .docx  → Python pipeline  (Phase 3 will replace)
//    .pptx  → Python pipeline  (Phase 4 will replace)
//    .pdf   → Python pipeline  (Phase 5 will replace)
//
//  All call-sites (FileViewerForm, SqlViewerForm) require ZERO changes.
// ============================================================================

using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileExplorerr.Export;

namespace FileExplorerr
{
    public static class ExportadorOffice
    {
        // ── Python script path (still needed for .docx / .pptx / .pdf) ───
        private static string ScriptPath
        {
            get
            {
                const string name = "export_office.py";
                string[] candidates =
                {
                    Path.Combine(AppContext.BaseDirectory, name),
                    Path.Combine(Directory.GetCurrentDirectory(), name),
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "FileExplorerr", name)
                };
                return candidates.FirstOrDefault(File.Exists)
                    ?? Path.Combine(AppContext.BaseDirectory, name);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINT  (unchanged signature)
        // ════════════════════════════════════════════════════════════════════
        public static bool ExportarConDialogo(
            DataTable? dt,
            string titulo,
            string ext,
            IWin32Window? owner = null)
        {
            if (dt is null || dt.Rows.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string extU = ext.TrimStart('.').ToUpper();
            string filter = ext switch
            {
                ".xlsx" => "Excel (*.xlsx)|*.xlsx",
                ".docx" => "Word (*.docx)|*.docx",
                ".pptx" => "PowerPoint (*.pptx)|*.pptx",
                ".pdf" => "PDF (*.pdf)|*.pdf",
                _ => $"{extU} (*{ext})|*{ext}"
            };

            using var dlg = new SaveFileDialog
            {
                Title = $"Exportar como {extU}",
                Filter = filter + "|Todos (*.*)|*.*",
                FileName = $"{SafeFileName(titulo)}_{DateTime.Now:yyyyMMdd_HHmm}{ext}"
            };
            if (dlg.ShowDialog(owner) != DialogResult.OK) return false;

            var dtCopy = dt.Copy();
            string outPath = dlg.FileName;

            // ── Route: native C# for supported extensions ─────────────────
            if (OfficeExporterFactory.IsSupported(ext))
            {
                _ = ExportarNativoAsync(dtCopy, titulo, ext, outPath, owner as Form);
                return true;
            }

            // ── Fallback: Python pipeline for docx / pptx / pdf ───────────
            _ = ExportarPythonAsync(dtCopy, titulo, ext.TrimStart('.'), outPath, owner as Form);
            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  NATIVE C# EXPORT PIPELINE  (Phase 2+)
        // ════════════════════════════════════════════════════════════════════
        private static async Task ExportarNativoAsync(
            DataTable dt,
            string titulo,
            string ext,
            string outputPath,
            Form? ownerForm)
        {
            var cts = new CancellationTokenSource();
            var progress = new ExportProgressForm(titulo, ext.TrimStart('.').ToUpper(), cts);
            progress.Show(ownerForm);

            try
            {
                var exporter = OfficeExporterFactory.Resolve(ext);

                var opts = ExportOptions.For(outputPath, titulo)
                                        .WithCancellation(cts.Token)
                                        .Build();

                var uiProgress = new Progress<int>(pct =>
                {
                    if (!progress.IsDisposed)
                        progress.Invoke(() => progress.SetStatus(null, pct));
                });

                progress.SetStatus("Generando archivo...", 5);

                ExportResult result = await exporter.ExportAsync(dt, opts, uiProgress);

                if (result.Success)
                {
                    progress.SetStatus("¡Listo!", 100);
                    await Task.Delay(400, CancellationToken.None);

                    // Capture all values BEFORE entering Invoke to avoid
                    // closure/shadowing issues on the UI thread.
                    string finalPath = result.OutputPath;
                    string finalExt = Path.GetExtension(finalPath).ToUpperInvariant();
                    bool truncated = result.WasTruncated;
                    int rowsWritten = result.RowsWritten;

                    // Re-read size from disk — BytesWritten may be 0 if the
                    // file was still being flushed when ExportResult.Ok() ran.
                    long sizeKb = 0;
                    try { sizeKb = new FileInfo(finalPath).Length / 1024; } catch { }

                    string finalMsg = truncated
                        ? $"Exportado ({sizeKb:N0} KB) — {rowsWritten:N0} filas:\n{finalPath}\n\n" +
                          $"⚠ El dataset fue truncado a las primeras {rowsWritten:N0} filas.\n" +
                          $"Para el dataset completo exporta con Excel (.xlsx).\n\n¿Abrir {finalExt}?"
                        : $"Exportado ({sizeKb:N0} KB):\n{finalPath}\n\n¿Abrir?";

                    progress.Invoke(() =>
                    {
                        progress.Close();

                        if (MessageBox.Show(finalMsg, "Exportación completa",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo
                            { FileName = finalPath, UseShellExecute = true });
                        }
                    });
                }
                else
                {
                    progress.Invoke(() =>
                    {
                        progress.Close();
                        MessageBox.Show(result.ErrorMessage, "Error al exportar",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                progress.Invoke(() => progress.Close());
            }
            catch (Exception ex)
            {
                progress.Invoke(() =>
                {
                    progress.Close();
                    MessageBox.Show($"Error inesperado al exportar:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                cts.Dispose();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PYTHON PIPELINE  (kept intact for .docx / .pptx / .pdf)
        // ════════════════════════════════════════════════════════════════════
        private static async Task ExportarPythonAsync(
            DataTable dt,
            string titulo,
            string fmt,
            string outputPath,
            Form? ownerForm)
        {
            var cts = new CancellationTokenSource();
            var progress = new ExportProgressForm(titulo, fmt.ToUpper(), cts);
            progress.Show(ownerForm);

            string? csvPath = null;
            try
            {
                progress.SetStatus("Preparando datos...", 5);
                csvPath = Path.GetTempFileName();
                await Task.Run(() => WriteCsv(dt, csvPath, cts.Token), cts.Token);
                cts.Token.ThrowIfCancellationRequested();

                progress.SetStatus($"Generando {fmt.ToUpper()}...", 20);

                string python = FindPython();
                string script = ScriptPath;

                if (!File.Exists(script))
                    throw new FileNotFoundException(
                        $"No se encontró export_office.py\nBuscado en: {script}");

                string args =
                    $"\"{script}\" {fmt} \"{csvPath}\" \"{outputPath}\" \"{EscapeArg(titulo)}\"";

                await RunPythonAsync(python, args, progress, cts.Token);
                cts.Token.ThrowIfCancellationRequested();

                if (!File.Exists(outputPath))
                    throw new InvalidOperationException(
                        "Python terminó sin generar el archivo.");

                progress.SetStatus("¡Listo!", 100);
                await Task.Delay(400, cts.Token);

                progress.Invoke(() =>
                {
                    progress.Close();
                    long size = new FileInfo(outputPath).Length / 1024;
                    if (MessageBox.Show(
                            $"{fmt.ToUpper()} generado ({size:N0} KB):\n{outputPath}\n\n¿Abrir?",
                            "Exportación completa",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        { FileName = outputPath, UseShellExecute = true });
                    }
                });
            }
            catch (OperationCanceledException)
            {
                progress.Invoke(() => progress.Close());
                try { if (outputPath is not null) File.Delete(outputPath); } catch { }
            }
            catch (Exception ex)
            {
                progress.Invoke(() =>
                {
                    progress.Close();
                    MessageBox.Show($"Error al exportar:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                try { if (csvPath is not null) File.Delete(csvPath); } catch { }
                cts.Dispose();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DIRECT-CALL OVERLOADS (unchanged public signatures)
        // ════════════════════════════════════════════════════════════════════
        public static void ExportarExcel(DataTable dt, string titulo, string ruta) =>
            ExportarDirecto(dt, titulo, "xlsx", ruta);

        public static void ExportarWord(DataTable dt, string titulo, string ruta) =>
            ExportarDirecto(dt, titulo, "docx", ruta);

        public static void ExportarPowerPoint(DataTable dt, string titulo, string ruta) =>
            ExportarDirecto(dt, titulo, "pptx", ruta);

        public static void ExportarPdf(DataTable dt, string titulo, string ruta) =>
            ExportarDirecto(dt, titulo, "pdf", ruta);

        private static void ExportarDirecto(
            DataTable dt, string titulo, string fmt, string outputPath)
        {
            string ext = "." + fmt;

            // ── Native path for supported formats ─────────────────────────
            if (OfficeExporterFactory.IsSupported(ext))
            {
                var exporter = OfficeExporterFactory.Resolve(ext);
                var opts = ExportOptions.For(outputPath, titulo).Build();
                // Block synchronously since this overload is synchronous by contract.
                var result = exporter.ExportAsync(dt, opts, null)
                                       .GetAwaiter().GetResult();
                if (!result.Success)
                    throw new InvalidOperationException(result.ErrorMessage);
                return;
            }

            // ── Python path for remaining formats ─────────────────────────
            string? csvPath = null;
            try
            {
                csvPath = Path.GetTempFileName();
                WriteCsv(dt, csvPath, CancellationToken.None);

                string python = FindPython();
                string args =
                    $"\"{ScriptPath}\" {fmt} \"{csvPath}\" \"{outputPath}\" \"{EscapeArg(titulo)}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = args,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi)!;
                proc.WaitForExit(300_000);

                if (proc.ExitCode != 0)
                    throw new InvalidOperationException(proc.StandardError.ReadToEnd());
            }
            finally
            {
                try { if (csvPath is not null) File.Delete(csvPath); } catch { }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  RUN PYTHON  (unchanged — still used for docx/pptx/pdf)
        // ════════════════════════════════════════════════════════════════════
        private static async Task RunPythonAsync(
            string python,
            string args,
            ExportProgressForm prog,
            CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = python,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var proc = new Process
            { StartInfo = psi, EnableRaisingEvents = true };

            var stderr = new StringBuilder();
            int pct = 20;

            proc.ErrorDataReceived += (_, e) =>
            { if (e.Data is not null) stderr.AppendLine(e.Data); };
            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                if (e.Data.StartsWith("PROGRESS:") &&
                    int.TryParse(e.Data[9..], out int p))
                {
                    pct = Math.Max(pct, p);
                    try { prog.Invoke(() => prog.SetStatus(null, pct)); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ExportadorOffice] Progress invoke: {ex.Message}");
                    }
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var reg = ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ExportadorOffice] Kill: {ex.Message}");
                }
            });

            var animTask = Task.Run(async () =>
            {
                while (!proc.HasExited)
                {
                    await Task.Delay(800, CancellationToken.None);
                    if (pct < 90) pct += 2;
                    try { prog.Invoke(() => prog.SetStatus(null, Math.Min(pct, 90))); }
                    catch { /* form may have closed */ }
                }
            }, ct);

            await Task.Run(() => proc.WaitForExit(), ct);

            try { await animTask; }
            catch (OperationCanceledException) { /* expected on cancel */ }

            if (proc.ExitCode != 0)
            {
                string err = stderr.ToString().Trim();

                if (err.Contains("ModuleNotFoundError") || err.Contains("No module named"))
                {
                    string missing = string.Empty;


                    if (err.Contains("reportlab")) missing = "reportlab";

                    throw new InvalidOperationException(
                        $"Falta la librería Python: {missing}\n\n" +
                        "Ejecuta en la terminal:\n" +
                        "  pip install reportlab");
                }

                throw new InvalidOperationException(
                    $"Python error (código {proc.ExitCode}):\n{err}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  WRITE TEMPORARY CSV  (still used by Python path)
        // ════════════════════════════════════════════════════════════════════
        private static void WriteCsv(DataTable dt, string path, CancellationToken ct)
        {
            using var sw = new StreamWriter(path, false, new UTF8Encoding(true), 65536);

            for (int c = 0; c < dt.Columns.Count; c++)
            {
                if (c > 0) sw.Write(',');
                sw.Write('"');
                sw.Write(CsvHelper.EscapeField(dt.Columns[c].ColumnName));
                sw.Write('"');
            }
            sw.WriteLine();

            for (int r = 0; r < dt.Rows.Count; r++)
            {
                ct.ThrowIfCancellationRequested();
                for (int c = 0; c < dt.Columns.Count; c++)
                {
                    if (c > 0) sw.Write(',');
                    sw.Write('"');
                    sw.Write(CsvHelper.EscapeField(dt.Rows[r][c]?.ToString() ?? string.Empty));
                    sw.Write('"');
                }
                sw.WriteLine();
                if (r % 5000 == 0) sw.Flush();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  FIND PYTHON  (still needed for pptx/pdf — docx migrated in Phase 3)
        // ════════════════════════════════════════════════════════════════════
        private static string FindPython()
        {
            foreach (string cmd in new[] { "python", "python3", "py" })
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(3000);
                    if (p?.ExitCode == 0) return cmd;
                }
                catch { /* try next candidate */ }
            }

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] winPaths =
            {
                @"C:\Python313\python.exe",
                @"C:\Python312\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Python310\python.exe",
                Path.Combine(local, @"Programs\Python\Python313\python.exe"),
                Path.Combine(local, @"Programs\Python\Python312\python.exe"),
                Path.Combine(local, @"Programs\Python\Python311\python.exe"),
            };
            foreach (string p in winPaths)
                if (File.Exists(p)) return p;

            throw new InvalidOperationException(
                "No se encontró Python 3.\n\n" +
                "Python sigue siendo necesario para exportar PDF.\n" +
                "Descárgalo en https://python.org y marca 'Add Python to PATH'.\n" +
                "Luego ejecuta:\n  pip install reportlab");
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static string EscapeArg(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string SafeFileName(string s) =>
            new string(s.Select(c =>
                Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
    }
}