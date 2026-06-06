using System;
using System.Windows.Forms;
using FileExplorerr.Export;
using QuestPDF.Infrastructure;
using FileExplorerr.Compression;

namespace FileExplorerr
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Auto-crear appsettings.json si no existe
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            string examplePath = Path.Combine(AppContext.BaseDirectory, "appsettings.example.json");

            if (!File.Exists(configPath) && File.Exists(examplePath))
            {
                File.Copy(examplePath, configPath);
            }

            if (!File.Exists(configPath))
            {
                MessageBox.Show(
                    "No se encontró el archivo appsettings.json.\n\n" +
                    "Crea el archivo en la carpeta del ejecutable con tus credenciales OAuth.\n\n" +
                    "Puedes usar appsettings.example.json como plantilla.",
                    "Configuración faltante",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // ── QuestPDF license (Community MIT — free under $1M revenue) ─
            QuestPDF.Settings.License = LicenseType.Community;

            // ── Register all native C# exporters ─────────────────────────
            // Phase 2: Excel  (.xlsx) — ClosedXML
            // Phase 3: Word   (.docx) — DocumentFormat.OpenXml
            // Phase 4: PPTX   (.pptx) — DocumentFormat.OpenXml
            // Phase 5: PDF    (.pdf)  — QuestPDF
            OfficeExporterFactory.RegisterNativeExporters();
            ArchiverFactory.RegisterBuiltInArchivers();

            // ── Global exception handlers ─────────────────────────────────
            Application.ThreadException += (_, args) =>
                ShowFatalError(args.Exception);

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    ShowFatalError(ex);
            };

            // 1. Intentar restaurar sesión previa
            var restoredUser = SessionManager.TryRestore();
            if (restoredUser != null)
            {
                // Hay sesión guardada: abrir directamente el explorador
                Application.Run(new Form1(restoredUser));
            }
            else
            {
                // No hay sesión: mostrar pantalla de login
                using var loginForm = new LoginForm();
                if (loginForm.ShowDialog() == DialogResult.OK && loginForm.LoggedInUser != null)
                {
                    Application.Run(new Form1(loginForm.LoggedInUser));
                }
                // Si el usuario cerró el login sin autenticarse, la app termina
            }
        }

        private static void ShowFatalError(Exception ex)
        {
            MessageBox.Show(
                $"Se produjo un error inesperado:\n\n{ex.Message}\n\n" +
                "La aplicación intentará continuar. Si el problema persiste, reiníciala.",
                "Error inesperado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}