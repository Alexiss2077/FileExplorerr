// ============================================================================
//  ExportadorOffice.cs  — v6 async + progress window no-blocking
//  Requiere export_office.py junto al .exe
//  pip install openpyxl python-docx python-pptx reportlab
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
        // ── Ruta del script Python ────────────────────────────────────────────
        private static string ScriptPath
        {
            get
            {
                string name = "export_office.py";
                string[] candidates =
                {
                    Path.Combine(AppContext.BaseDirectory, name),
                    Path.Combine(Directory.GetCurrentDirectory(), name),
                    Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData), "FileExplorerr", name)
                };
                return candidates.FirstOrDefault(File.Exists)
                    ?? Path.Combine(AppContext.BaseDirectory, name);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PUNTO DE ENTRADA — fire-and-forget, no bloquea la UI
        // ════════════════════════════════════════════════════════════════════
        public static bool ExportarConDialogo(DataTable? dt,
            string titulo, string ext, IWin32Window? owner = null)
        {
            if (dt == null || dt.Rows.Count == 0)
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
                FileName = $"{NombreSeguro(titulo)}_{DateTime.Now:yyyyMMdd_HHmm}{ext}"
            };
            if (dlg.ShowDialog(owner) != DialogResult.OK) return false;

            // Clonar el DataTable para no tener problemas de hilo
            var dtCopy = dt.Copy();
            string outputPath = dlg.FileName;
            string fmt = ext.TrimStart('.');

            // Lanzar sin bloquear — la ventana de progreso es independiente
            _ = ExportarAsync(dtCopy, titulo, fmt, outputPath, owner as Form);
            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TAREA ASYNC PRINCIPAL
        // ════════════════════════════════════════════════════════════════════
        private static async Task ExportarAsync(DataTable dt, string titulo,
            string fmt, string outputPath, Form? ownerForm)
        {
            var cts = new CancellationTokenSource();
            var progress = new ExportProgressForm(titulo, fmt.ToUpper(), cts);
            progress.Show(ownerForm);

            string? csvPath = null;
            try
            {
                // ── 1. Escribir CSV temporal (async, en hilo de fondo) ────────
                progress.SetStatus("Preparando datos...", 5);
                csvPath = Path.GetTempFileName();

                await Task.Run(() => EscribirCsv(dt, csvPath, cts.Token), cts.Token);
                cts.Token.ThrowIfCancellationRequested();

                // ── 2. Llamar a Python (async) ────────────────────────────────
                progress.SetStatus($"Generando {fmt.ToUpper()}...", 20);

                string python = EncontrarPython();
                string script = ScriptPath;

                if (!File.Exists(script))
                    throw new FileNotFoundException(
                        $"No se encontró export_office.py\nBuscado en: {script}");

                string args = $"\"{script}\" {fmt} \"{csvPath}\" \"{outputPath}\" \"{EscapeArg(titulo)}\"";

                await RunPythonAsync(python, args, progress, cts.Token);
                cts.Token.ThrowIfCancellationRequested();

                if (!File.Exists(outputPath))
                    throw new Exception("Python terminó sin generar el archivo.");

                progress.SetStatus("¡Listo!", 100);
                await Task.Delay(400, cts.Token);

                // ── 3. Notificar en UI thread ─────────────────────────────────
                progress.Invoke(() =>
                {
                    progress.Close();
                    string extU = fmt.ToUpper();
                    long size = new FileInfo(outputPath).Length / 1024;
                    if (MessageBox.Show(
                            $"{extU} generado ({size:N0} KB):\n{outputPath}\n\n¿Abrir?",
                            "Exportación completa",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Information)
                        == DialogResult.Yes)
                        Process.Start(new ProcessStartInfo
                        { FileName = outputPath, UseShellExecute = true });
                });
            }
            catch (OperationCanceledException)
            {
                // Usuario canceló — eliminar archivo parcial
                progress.Invoke(() => progress.Close());
                try { if (outputPath != null) File.Delete(outputPath); } catch { }
            }
            catch (Exception ex)
            {
                progress.Invoke(() =>
                {
                    progress.Close();
                    MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                try { if (csvPath != null) File.Delete(csvPath); } catch { }
                cts.Dispose();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  CORRER PYTHON DE FORMA COMPLETAMENTE ASÍNCRONA
        //  Lee stdout/stderr sin bloquear y actualiza el progreso
        // ════════════════════════════════════════════════════════════════════
        private static async Task RunPythonAsync(string python, string args,
            ExportProgressForm prog, CancellationToken ct)
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

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var stderr = new StringBuilder();
            int pct = 20;

            proc.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) stderr.AppendLine(e.Data);
            };
            proc.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                // El script Python escribe "PROGRESS:XX" para reportar avance
                if (e.Data.StartsWith("PROGRESS:") &&
                    int.TryParse(e.Data[9..], out int p))
                {
                    pct = Math.Max(pct, p);
                    try { prog.Invoke(() => prog.SetStatus(null, pct)); } catch { }
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // Registrar cancelación para matar el proceso
            using var reg = ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            });

            // Animar progreso mientras esperamos (pseudo-progreso si Python no reporta)
            var animTask = Task.Run(async () =>
            {
                while (!proc.HasExited)
                {
                    await Task.Delay(800);
                    if (pct < 90) pct += 2;
                    try { prog.Invoke(() => prog.SetStatus(null, Math.Min(pct, 90))); } catch { }
                }
            }, ct);

            await Task.Run(() => proc.WaitForExit(), ct);

            try { await animTask; } catch { }

            if (proc.ExitCode != 0)
            {
                string err = stderr.ToString().Trim();

                // Detectar librerías faltantes y dar mensaje claro
                if (err.Contains("ModuleNotFoundError") || err.Contains("No module named"))
                {
                    string missing = "";
                    if (err.Contains("openpyxl")) missing = "openpyxl";
                    if (err.Contains("docx")) missing = "python-docx";
                    if (err.Contains("pptx")) missing = "python-pptx";
                    if (err.Contains("reportlab")) missing = "reportlab";
                    throw new Exception(
                        $"Falta la librería Python: {missing}\n\n" +
                        $"Ejecuta en la terminal:\n" +
                        $"  pip install openpyxl python-docx python-pptx reportlab");
                }
                throw new Exception($"Python error (código {proc.ExitCode}):\n{err}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ESCRIBIR CSV TEMPORAL (con soporte de cancelación)
        // ════════════════════════════════════════════════════════════════════
        private static void EscribirCsv(DataTable dt, string path, CancellationToken ct)
        {
            using var sw = new StreamWriter(path, false, new UTF8Encoding(true), 65536);

            // Cabecera
            for (int c = 0; c < dt.Columns.Count; c++)
            {
                if (c > 0) sw.Write(',');
                sw.Write('"');
                sw.Write(dt.Columns[c].ColumnName.Replace("\"", "\"\""));
                sw.Write('"');
            }
            sw.WriteLine();

            // Datos
            for (int r = 0; r < dt.Rows.Count; r++)
            {
                ct.ThrowIfCancellationRequested();

                for (int c = 0; c < dt.Columns.Count; c++)
                {
                    if (c > 0) sw.Write(',');
                    string val = dt.Rows[r][c]?.ToString() ?? "";
                    sw.Write('"');
                    sw.Write(val.Replace("\"", "\"\""));
                    sw.Write('"');
                }
                sw.WriteLine();

                if (r % 5000 == 0) sw.Flush();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static string EncontrarPython()
        {
            string[] candidates = { "python", "python3", "py" };
            foreach (string cmd in candidates)
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
                catch { }
            }

            string[] winPaths =
            {
                @"C:\Python313\python.exe", @"C:\Python312\python.exe",
                @"C:\Python311\python.exe", @"C:\Python310\python.exe",
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    @"Programs\Python\Python313\python.exe"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    @"Programs\Python\Python312\python.exe"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    @"Programs\Python\Python311\python.exe"),
            };
            foreach (string p in winPaths)
                if (File.Exists(p)) return p;

            throw new Exception(
                "No se encontró Python 3.\n\n" +
                "Descárgalo en https://python.org y marca 'Add Python to PATH'.\n" +
                "Luego ejecuta:\n  pip install openpyxl python-docx python-pptx reportlab");
        }

        private static string EscapeArg(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string NombreSeguro(string s) =>
            new string(s.Select(c =>
                Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());

        // Métodos públicos para llamadas directas desde otros módulos
        public static void ExportarExcel(DataTable dt, string titulo, string ruta) =>
            ExportarConPython(dt, titulo, "xlsx", ruta);
        public static void ExportarWord(DataTable dt, string titulo, string ruta) =>
            ExportarConPython(dt, titulo, "docx", ruta);
        public static void ExportarPowerPoint(DataTable dt, string titulo, string ruta) =>
            ExportarConPython(dt, titulo, "pptx", ruta);
        public static void ExportarPdf(DataTable dt, string titulo, string ruta) =>
            ExportarConPython(dt, titulo, "pdf", ruta);

        private static void ExportarConPython(DataTable dt, string titulo,
            string fmt, string outputPath)
        {
            string? csvPath = null;
            try
            {
                csvPath = Path.GetTempFileName();
                EscribirCsv(dt, csvPath, CancellationToken.None);
                string python = EncontrarPython();
                string args = $"\"{ScriptPath}\" {fmt} \"{csvPath}\" \"{outputPath}\" \"{EscapeArg(titulo)}\"";
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
                    throw new Exception(proc.StandardError.ReadToEnd());
            }
            finally { try { if (csvPath != null) File.Delete(csvPath); } catch { } }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  VENTANA DE PROGRESO — no modal, cancelable, barra animada
    // ════════════════════════════════════════════════════════════════════════
    internal class ExportProgressForm : Form
    {
        private readonly Label _lblTitulo;
        private readonly Label _lblStatus;
        private readonly ProgressBar _bar;
        private readonly Button _btnCancel;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly CancellationTokenSource _cts;
        private int _animPct;

        public ExportProgressForm(string titulo, string fmt, CancellationTokenSource cts)
        {
            _cts = cts;

            Text = $"Exportando {fmt}...";
            Size = new System.Drawing.Size(440, 170);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            TopMost = true;
            BackColor = System.Drawing.Color.FromArgb(26, 26, 32);

            _lblTitulo = new Label
            {
                Text = $"Exportando: {titulo}",
                Left = 16,
                Top = 14,
                Width = 400,
                Height = 20,
                ForeColor = System.Drawing.Color.FromArgb(220, 220, 236),
                Font = new System.Drawing.Font("Segoe UI", 9F,
                    System.Drawing.FontStyle.Bold),
                AutoEllipsis = true
            };

            _lblStatus = new Label
            {
                Text = "Iniciando...",
                Left = 16,
                Top = 38,
                Width = 400,
                Height = 18,
                ForeColor = System.Drawing.Color.FromArgb(72, 202, 188),
                Font = new System.Drawing.Font("Segoe UI", 8.5F)
            };

            _bar = new ProgressBar
            {
                Left = 16,
                Top = 62,
                Width = 400,
                Height = 22,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            _btnCancel = new Button
            {
                Text = "Cancelar",
                Left = 160,
                Top = 96,
                Width = 110,
                Height = 32,
                BackColor = System.Drawing.Color.FromArgb(80, 30, 30),
                ForeColor = System.Drawing.Color.FromArgb(220, 95, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderColor =
                System.Drawing.Color.FromArgb(220, 95, 85);
            _btnCancel.Click += (s, e) =>
            {
                _btnCancel.Enabled = false;
                _btnCancel.Text = "Cancelando...";
                _cts.Cancel();
            };

            Controls.AddRange(new Control[]
                { _lblTitulo, _lblStatus, _bar, _btnCancel });

            // Timer para smooth animation de la barra
            _timer = new System.Windows.Forms.Timer { Interval = 40 };
            _timer.Tick += (s, e) =>
            {
                if (_bar.Value < _animPct)
                {
                    int next = Math.Min(_bar.Value + 2, _animPct);
                    _bar.Value = next;
                }
            };
            _timer.Start();
        }

        public void SetStatus(string? msg, int pct = -1)
        {
            if (msg != null) _lblStatus.Text = msg;
            if (pct >= 0) _animPct = Math.Min(pct, 100);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer?.Dispose();
            base.Dispose(disposing);
        }
    }
}