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
            SetupComponents();
            LoadFile();
        }

        private void SetupComponents()
        {
            this.Text = $"Visor — {Path.GetFileName(filePath)}";
            this.Size = new Size(900, 700);
            this.BackColor = Color.FromArgb(10, 14, 20);
            this.ForeColor = Color.FromArgb(220, 232, 248);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(600, 400);

            topPanel = new Panel
            {
                Height = 48,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(17, 23, 33),
                Padding = new Padding(14, 0, 0, 0)
            };
            topPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)),
                    0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);

            fileInfoLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(110, 140, 180),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            topPanel.Controls.Add(fileInfoLabel);

            textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                BackColor = Color.FromArgb(13, 18, 26),
                ForeColor = Color.FromArgb(200, 220, 245),
                Font = new Font("Cascadia Code", 10F),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                WordWrap = false
            };

            Panel textPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(10, 14, 20)
            };
            textPanel.Controls.Add(textBox);

            this.Controls.Add(textPanel);
            this.Controls.Add(topPanel);
        }

        private void LoadFile()
        {
            try
            {
                var fi = new FileInfo(filePath);
                string ext = fi.Extension.ToLower();

                fileInfoLabel.Text =
                    $"  {fi.Name}   ·   {FormatSize(fi.Length)}   ·   {fi.LastWriteTime:dd/MM/yyyy HH:mm}";

                string content = File.ReadAllText(filePath);
                textBox.Text = ext switch
                {
                    ".json" => FormatJson(content),
                    ".xml" => FormatXml(content),
                    ".csv" => FormatCsv(content),
                    _ => content
                };
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
                using var doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc.RootElement,
                    new JsonSerializerOptions { WriteIndented = true });
            }
            catch { return json; }
        }

        private string FormatXml(string xml)
        {
            try
            {
                var doc = new XmlDocument(); doc.LoadXml(xml);
                using var sw = new System.IO.StringWriter();
                using var xw = new XmlTextWriter(sw) { Formatting = Formatting.Indented, Indentation = 2 };
                doc.Save(xw);
                return sw.ToString();
            }
            catch { return xml; }
        }

        private string FormatCsv(string csv)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (string line in csv.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                {
                    foreach (string cell in line.Split(',')) sb.Append(cell.PadRight(25));
                    sb.AppendLine();
                }
                return sb.ToString();
            }
            catch { return csv; }
        }

        private string FormatSize(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }
    }
}