using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileExplorerr.Compression;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  COMPRESSION SERVICE  — FACHADA PÚBLICA
    //  La única clase relacionada con compresión que Form1 necesita conocer.
    //
    //  Responsabilidades:
    //    - Mostrar SaveFileDialog / ExtractOptionsDialog para recoger input.
    //    - Construir ArchiveOptions con el builder fluido.
    //    - Resolver el IArchiver correcto via ArchiverFactory.
    //    - Crear y gestionar CancellationTokenSource.
    //    - Abrir y cerrar CompressionProgressForm.
    //    - Transmitir IProgress<int> e IProgress<string> al form.
    //    - Mostrar resultado final (éxito / cancelado / error) via MessageBox.
    //    - Invocar el callback onRefresh para que Form1 refresque el ListView.
    //
    //  Lo que esta clase NO hace deliberadamente:
    //    - Ninguna lógica ZIP/archive (delegada a ZipArchiver).
    //    - Ningún dibujado de UI (delegado a CompressionProgressForm).
    //    - Ningún refresco del explorador (delegado a Form1 via onRefresh).
    // ════════════════════════════════════════════════════════════════════════
    internal static class CompressionService
    {
        // ════════════════════════════════════════════════════════════════════
        //  COMPRIMIR
        //  Punto de entrada desde el menú contextual "Comprimir selección..."
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Muestra un SaveFileDialog y comprime los
        /// <paramref name="sourcePaths"/> seleccionados en un ZIP.
        /// </summary>
        /// <param name="sourcePaths">
        /// Rutas completas de los ítems a comprimir (de la selección del ListView).
        /// </param>
        /// <param name="owner">Ventana padre para centrar los diálogos.</param>
        /// <param name="onRefresh">
        /// Callback opcional invocado tras éxito para que Form1 refresque el ListView.
        /// </param>
        public static void Compress(
            IEnumerable<string> sourcePaths,
            IWin32Window? owner,
            Action? onRefresh = null)
        {
            var paths = sourcePaths.ToList();

            if (paths.Count == 0)
            {
                MessageBox.Show(
                    "Selecciona al menos un archivo o carpeta para comprimir.",
                    "Sin selección",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Nombre sugerido para el ZIP de salida
            string suggestedName = paths.Count == 1
                ? Path.GetFileNameWithoutExtension(paths[0])
                : $"Archivos_{DateTime.Now:yyyyMMdd_HHmm}";

            using var dlg = new SaveFileDialog
            {
                Title = "Comprimir como ZIP",
                Filter = "Archivo ZIP (*.zip)|*.zip",
                FileName = $"{SafeFileName(suggestedName)}.zip",
                InitialDirectory = GetCommonFolder(paths)
            };

            if (dlg.ShowDialog(owner) != DialogResult.OK) return;

            // Fire-and-forget: mantiene la firma pública síncrona (void)
            // para que Form1 pueda llamarla desde un event handler normal,
            // igual que ExportadorOffice.ExportarConDialogo().
            _ = CompressInternalAsync(paths, dlg.FileName, owner as Form, onRefresh);
        }

        // ════════════════════════════════════════════════════════════════════
        //  EXTRAER
        //  Punto de entrada desde "Extraer aquí" y "Extraer en..."
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extrae el ZIP indicado. Si <paramref name="extractHere"/> es true
        /// extrae en la misma carpeta que contiene el ZIP aplanando la carpeta
        /// raíz única (comportamiento "Extraer aquí" de WinRAR/7-Zip).
        /// Si es false, muestra ExtractOptionsDialog para que el usuario elija.
        /// </summary>
        /// <param name="archivePath">Ruta completa del archivo .zip a extraer.</param>
        /// <param name="owner">Ventana padre.</param>
        /// <param name="extractHere">
        /// true  → extrae en la carpeta del ZIP, aplana la carpeta raíz única.<br/>
        /// false → muestra el diálogo de opciones de extracción.
        /// </param>
        /// <param name="onRefresh">
        /// Callback opcional invocado tras éxito para refrescar el ListView.
        /// </param>
        public static void Extract(
            string archivePath,
            IWin32Window? owner,
            bool extractHere = false,
            Action? onRefresh = null)
        {
            if (!File.Exists(archivePath))
            {
                MessageBox.Show(
                    $"El archivo no existe:\n{archivePath}",
                    "Archivo no encontrado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string destination;
            bool overwrite;
            bool flatten;

            if (extractHere)
            {
                // "Extraer aquí": destino = carpeta que contiene el ZIP,
                // sin sobreescribir y aplanando la carpeta raíz del ZIP.
                destination = Path.GetDirectoryName(archivePath)
                              ?? Environment.CurrentDirectory;
                overwrite = false;
                flatten = true;   // ← clave: activa FlattenSingleRootFolder
            }
            else
            {
                // "Extraer en...": el usuario elige destino y opciones.
                using var optsDlg = new ExtractOptionsDialog(archivePath);
                if (optsDlg.ShowDialog(owner) != DialogResult.OK) return;
                if (optsDlg.SelectedDestination is null) return;

                destination = optsDlg.SelectedDestination;
                overwrite = optsDlg.OverwriteExisting;
                flatten = false;  // el usuario eligió un destino explícito
            }

            _ = ExtractInternalAsync(
                    archivePath,
                    destination,
                    overwrite,
                    flatten,
                    owner as Form,
                    onRefresh);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PIPELINE INTERNO — COMPRESIÓN
        // ════════════════════════════════════════════════════════════════════

        private static async Task CompressInternalAsync(
            List<string> sourcePaths,
            string outputPath,
            Form? ownerForm,
            Action? onRefresh)
        {
            string ext = Path.GetExtension(outputPath).ToLowerInvariant();
            IArchiver? archiver = ArchiverFactory.TryResolve(ext);

            if (archiver is null)
            {
                MessageBox.Show(
                    $"No hay un compresor disponible para el formato '{ext}'.",
                    "Formato no soportado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            using var cts = new CancellationTokenSource();
            using var progress = new CompressionProgressForm(
                "Comprimiendo", archiver.DisplayName, cts);

            progress.Show(ownerForm);

            try
            {
                var options = ArchiveOptions
                    .ForCompression(sourcePaths, outputPath)
                    .WithCancellation(cts.Token)
                    .Build();

                var pctProgress = new Progress<int>(pct =>
                {
                    if (!progress.IsDisposed)
                        progress.SetStatus(null, pct);
                });

                var nameProgress = new Progress<string>(name =>
                {
                    if (!progress.IsDisposed)
                        progress.SetStatus(Path.GetFileName(name), -1);
                });

                progress.SetStatus("Analizando archivos...", 2);

                ArchiveResult result = await archiver.CompressAsync(
                    options, pctProgress, nameProgress);

                progress.SetStatus("¡Listo!", 100);
                await Task.Delay(350, CancellationToken.None);

                if (result.Success)
                {
                    progress.Invoke(() =>
                    {
                        progress.Close();

                        long sizeKb = result.BytesWritten / 1024;
                        string msg =
                            $"Comprimido correctamente.\n\n" +
                            $"Archivo: {Path.GetFileName(result.OutputPath)}\n" +
                            $"Entradas: {result.FilesAdded}\n" +
                            $"Tamaño: {sizeKb:N0} KB\n\n" +
                            "¿Abrir carpeta de destino?";

                        if (MessageBox.Show(msg, "Compresión completa",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            OpenFolderInExplorer(
                                Path.GetDirectoryName(result.OutputPath)!);
                        }
                    });

                    onRefresh?.Invoke();
                }
                else if (result.WasCancelled)
                {
                    progress.Invoke(() =>
                    {
                        progress.Close();
                        MessageBox.Show(
                            "Compresión cancelada.\n\nEl archivo parcial ha sido eliminado.",
                            "Cancelado",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    });
                }
                else
                {
                    progress.Invoke(() =>
                    {
                        progress.Close();
                        MessageBox.Show(
                            $"Error al comprimir:\n\n{result.ErrorMessage}",
                            "Error de compresión",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    });
                }
            }
            catch (Exception ex)
            {
                if (!progress.IsDisposed)
                    progress.Invoke(() =>
                    {
                        progress.Close();
                        MessageBox.Show(
                            $"Error inesperado:\n\n{ex.Message}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    });

                System.Diagnostics.Debug.WriteLine(
                    $"[CompressionService.CompressInternalAsync] {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PIPELINE INTERNO — EXTRACCIÓN
        // ════════════════════════════════════════════════════════════════════

        private static async Task ExtractInternalAsync(
            string archivePath,
            string destination,
            bool overwrite,
            bool flatten,
            Form? ownerForm,
            Action? onRefresh)
        {
            string ext = Path.GetExtension(archivePath).ToLowerInvariant();
            IArchiver? archiver = ArchiverFactory.TryResolve(ext);

            if (archiver is null)
            {
                MessageBox.Show(
                    $"No hay un extractor disponible para el formato '{ext}'.\n\n" +
                    $"Formatos soportados: " +
                    $"{string.Join(", ", ArchiverFactory.SupportedExtensions)}",
                    "Formato no soportado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            using var cts = new CancellationTokenSource();
            using var progress = new CompressionProgressForm(
                "Extrayendo", archiver.DisplayName, cts);

            progress.Show(ownerForm);

            try
            {
                // Construir opciones activando FlattenSingleRootFolder
                // cuando el modo es "Extraer aquí".
                var builder = ArchiveOptions
                    .ForExtraction(archivePath, destination)
                    .WithOverwrite(overwrite)
                    .WithCancellation(cts.Token);

                if (flatten)
                    builder = builder.WithFlattenSingleRoot();

                var options = builder.Build();

                var pctProgress = new Progress<int>(pct =>
                {
                    if (!progress.IsDisposed)
                        progress.SetStatus(null, pct);
                });

                var nameProgress = new Progress<string>(name =>
                {
                    if (!progress.IsDisposed)
                        progress.SetStatus(name, -1);
                });

                progress.SetStatus("Abriendo archivo...", 2);

                ArchiveResult result = await archiver.ExtractAsync(
                    options, pctProgress, nameProgress);

                progress.SetStatus("¡Listo!", 100);
                await Task.Delay(350, CancellationToken.None);

                if (result.Success)
                {
                    progress.Invoke(() =>
                    {
                        progress.Close();

                        string skippedInfo = result.SkippedFiles.Count > 0
                            ? $"\nOmitidos (ya existían): {result.SkippedFiles.Count}"
                            : string.Empty;

                        string msg =
                            $"Extracción completada.\n\n" +
                            $"Archivos extraídos: {result.FilesExtracted}\n" +
                            $"Destino: {result.DestinationFolder}" +
                            $"{skippedInfo}\n\n" +
                            "¿Abrir carpeta de destino?";

                        if (MessageBox.Show(msg, "Extracción completa",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            OpenFolderInExplorer(result.DestinationFolder);
                        }
                    });

                    onRefresh?.Invoke();
                }
                else if (result.WasCancelled)
                {
                    progress.Invoke(() =>
                    {
                        progress.Close();
                        MessageBox.Show(
                            "Extracción cancelada.",
                            "Cancelado",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    });
                }
                else
                {
                    progress.Invoke(() =>
                    {
                        progress.Close();
                        MessageBox.Show(
                            $"Error al extraer:\n\n{result.ErrorMessage}",
                            "Error de extracción",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    });
                }
            }
            catch (Exception ex)
            {
                if (!progress.IsDisposed)
                    progress.Invoke(() =>
                    {
                        progress.Close();
                        MessageBox.Show(
                            $"Error inesperado:\n\n{ex.Message}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    });

                System.Diagnostics.Debug.WriteLine(
                    $"[CompressionService.ExtractInternalAsync] {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Devuelve la carpeta padre común de una colección de rutas.
        /// Se usa como InitialDirectory del SaveFileDialog.
        /// </summary>
        private static string GetCommonFolder(IEnumerable<string> paths)
        {
            foreach (var p in paths)
            {
                string? dir = File.Exists(p)
                    ? Path.GetDirectoryName(p)
                    : Directory.Exists(p)
                        ? Path.GetDirectoryName(p.TrimEnd(Path.DirectorySeparatorChar))
                        : null;

                if (!string.IsNullOrEmpty(dir)) return dir;
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        /// <summary>Elimina caracteres inválidos en nombres de archivo.</summary>
        private static string SafeFileName(string s) =>
            new string(s.Select(c =>
                Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());

        /// <summary>Abre una carpeta en el Explorador de Windows.</summary>
        private static void OpenFolderInExplorer(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CompressionService.OpenFolderInExplorer] {ex.Message}");
            }
        }
    }
}