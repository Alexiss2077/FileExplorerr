// ============================================================================
//  ExportadorOffice.cs
//  Async Office/PDF export engine.
//  Requires export_office.py next to the .exe.
//  pip install openpyxl python-docx python-pptx reportlab
//
//  Phase 4 change: ExportProgressForm extracted to ExportProgressForm.cs
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

namespace FileExplorerr
{
    public static class ExportadorOffice
    {
        // ── Python script path ────────────────────────────────────────────────
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
        //  PUBLIC ENTRY POINT — fire-and-forget, does not block the UI
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

            // Clone the DataTable so the background thread gets its own copy.
            var dtCopy = dt.Copy();
            string outputPath = dlg.FileName;
            string fmt = ext.TrimStart('.');

            _ = ExportarAsync(dtCopy, titulo, fmt, outputPath, owner as Form);
            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  ASYNC EXPORT PIPELINE
        // ════════════════════════════════════════════════════════════════════
        private static async Task ExportarAsync(
            DataTable dt,
            string titulo,
            string fmt,
            string outputPath,
            Form? ownerForm)
        {
            var cts = new CancellationTokenSource();
            // ExportProgressForm is now in ExportProgressForm.cs
            var progress = new ExportProgressForm(titulo, fmt.ToUpper(), cts);
            progress.Show(ownerForm);

            string? csvPath = null;
            try
            {
                // 1. Write temporary CSV on a background thread.
                progress.SetStatus("Preparando datos...", 5);
                csvPath = Path.GetTempFileName();
                await Task.Run(() => WriteCsv(dt, csvPath, cts.Token), cts.Token);
                cts.Token.ThrowIfCancellationRequested();

                // 2. Call the Python script.
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

                // 3. Notify on the UI thread.
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
                try { if (outputPath is not null) File.Delete(outputPath); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ExportadorOffice] Cleanup on cancel: {ex.Message}");
                }
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
                try { if (csvPath is not null) File.Delete(csvPath); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ExportadorOffice] Temp file cleanup: {ex.Message}");
                }
                cts.Dispose();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  RUN PYTHON — fully async, reads stdout/stderr without blocking
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

            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
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

            // Register cancellation to kill the process tree.
            using var reg = ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ExportadorOffice] Kill: {ex.Message}");
                }
            });

            // Animate progress while we wait.
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
                    if (err.Contains("openpyxl")) missing = "openpyxl";
                    if (err.Contains("docx")) missing = "python-docx";
                    if (err.Contains("pptx")) missing = "python-pptx";
                    if (err.Contains("reportlab")) missing = "reportlab";

                    throw new InvalidOperationException(
                        $"Falta la librería Python: {missing}\n\n" +
                        "Ejecuta en la terminal:\n" +
                        "  pip install openpyxl python-docx python-pptx reportlab");
                }

                throw new InvalidOperationException(
                    $"Python error (código {proc.ExitCode}):\n{err}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  WRITE TEMPORARY CSV
        // ════════════════════════════════════════════════════════════════════
        private static void WriteCsv(DataTable dt, string path, CancellationToken ct)
        {
            using var sw = new StreamWriter(path, false, new UTF8Encoding(true), 65536);

            // Header
            for (int c = 0; c < dt.Columns.Count; c++)
            {
                if (c > 0) sw.Write(',');
                sw.Write('"');
                sw.Write(CsvHelper.EscapeField(dt.Columns[c].ColumnName));
                sw.Write('"');
            }
            sw.WriteLine();

            // Data
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
        //  FIND PYTHON
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
                catch
                {
                    // Try next candidate.
                }
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
                "Descárgalo en https://python.org y marca 'Add Python to PATH'.\n" +
                "Luego ejecuta:\n  pip install openpyxl python-docx python-pptx reportlab");
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static string EscapeArg(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string SafeFileName(string s) =>
            new string(s.Select(c =>
                Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());

        // ── Direct-call overloads (used by modules that bypass the dialog) ──

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
                try { if (csvPath is not null) File.Delete(csvPath); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ExportadorOffice.ExportarDirecto] Temp cleanup: {ex.Message}");
                }
            }
        }
    }
}