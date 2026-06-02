using System;
using System.Windows.Forms;

namespace FileExplorerr
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Catch unhandled exceptions on the UI thread so the app can show
            // a diagnostic message instead of crashing silently.
            Application.ThreadException += (_, args) =>
                ShowFatalError(args.Exception);

            // Catch unhandled exceptions on background threads.
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    ShowFatalError(ex);
            };

            Application.Run(new Form1());
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