using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Text.Json;

namespace FileExplorerr
{
    public class FileViewerForm : Form
    {
        private TextBox textBox;
        private string filePath;
        private Panel topPanel;
        private Label fileInfoLabel;

        public FileViewerForm(string path)
        {
            filePath = path;
            InitializeComponents();
            LoadFile();
        }

        private void InitializeComponents()
        {
            this.Text = $"Visor - {Path.GetFileName(filePath)}";
            this.Size = new Size(900, 700);
            this.BackColor = Color.FromArgb(240, 240, 240);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(600, 400);

            // Panel superior con información del archivo
            topPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(15)
            };

            fileInfoLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(60, 60, 60),
                TextAlign = ContentAlignment.MiddleLeft
            };

            topPanel.Controls.Add(fileInfoLabel);

            // TextBox para mostrar el contenido
            textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Consolas", 10F),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                WordWrap = false,
                Padding = new Padding(10)
            };

            // Panel para el TextBox con borde
            Panel textPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(240, 240, 240)
            };
            textPanel.Controls.Add(textBox);

            this.Controls.Add(textPanel);
            this.Controls.Add(topPanel);
        }

        
        private void LoadFile()
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                string extension = fileInfo.Extension.ToLower();

                fileInfoLabel.Text = $"Archivo: {fileInfo.Name} | Tamaño: {FormatFileSize(fileInfo.Length)} | " +
                                   $"Modificado: {fileInfo.LastWriteTime:dd/MM/yyyy HH:mm}";

                string content = File.ReadAllText(filePath);

                // Formatear según el tipo de archivo
                switch (extension)
                {
                    case ".json":
                        textBox.Text = FormatJson(content);
                        break;
                    case ".xml":
                        textBox.Text = FormatXml(content);
                        break;
                    case ".csv":
                        textBox.Text = FormatCsv(content);
                        break;
                    default:
                        textBox.Text = content;
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el archivo: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        private string FormatJson(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }
            }
            catch
            {
                return json; // Si no se puede parsear, devolver el original
            }
        }

        private string FormatXml(string xml)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                using (StringWriter stringWriter = new StringWriter())
                using (XmlTextWriter xmlTextWriter = new XmlTextWriter(stringWriter))
                {
                    xmlTextWriter.Formatting = Formatting.Indented;
                    xmlTextWriter.Indentation = 2;
                    doc.Save(xmlTextWriter);
                    return stringWriter.ToString();
                }
            }
            catch
            {
                return xml; // Si no se puede parsear, devolver el original
            }
        }

        private string FormatCsv(string csv)
        {
            try
            {
                string[] lines = csv.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                System.Text.StringBuilder formatted = new System.Text.StringBuilder();

                foreach (string line in lines)
                {
                    string[] cells = line.Split(',');
                    foreach (string cell in cells)
                    {
                        formatted.Append(cell.PadRight(25));
                    }
                    formatted.AppendLine();
                }

                return formatted.ToString();
            }
            catch
            {
                return csv;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}