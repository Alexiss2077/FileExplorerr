using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    public partial class EmailForm : Form
    {
        private string _filePath;

        // Controles de la UI
        private TextBox txtTo = null!;
        private TextBox txtSubject = null!;
        private Button btnSend = null!;
        private Label lblStatus = null!;

        // ── CONFIGURACIÓN DEL BOT (Cuenta de Sistema) ───────────────
        // Reemplaza esto con el correo que crees para el explorador
        private readonly string correoSistema = "nat.fileexplorer@gmail.com";
        // Reemplaza esto con la Contraseña de Aplicación de 16 caracteres
        private readonly string passwordApp = "nzojefxvcolaolsl";
        // ────────────────────────────────────────────────────────────

        public EmailForm(string filePath)
        {
            _filePath = filePath;
            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = "Enviar Archivo por Correo";
            this.Size = new Size(400, 260);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 35);
            this.ForeColor = Color.White;

            // Nombre del archivo a enviar
            var lblFile = new Label
            {
                Text = $"Archivo adjunto: {Path.GetFileName(_filePath)}",
                Location = new Point(20, 15),
                Size = new Size(340, 20),
                ForeColor = Color.FromArgb(80, 160, 220),
                AutoEllipsis = true
            };

            // Etiqueta y TextBox para Destinatario
            var lblTo = new Label { Text = "Para (Correo):", Location = new Point(20, 45), AutoSize = true };
            txtTo = new TextBox
            {
                Location = new Point(20, 65),
                Size = new Size(340, 25),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Etiqueta y TextBox para Asunto
            var lblSubject = new Label { Text = "Asunto:", Location = new Point(20, 100), AutoSize = true };
            txtSubject = new TextBox
            {
                Location = new Point(20, 120),
                Size = new Size(340, 25),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Etiqueta de estado (para mostrar "Enviando...")
            lblStatus = new Label
            {
                Text = "",
                Location = new Point(20, 175),
                Size = new Size(200, 20),
                ForeColor = Color.Yellow
            };

            // Botón Enviar
            btnSend = new Button
            {
                Text = "✉ Enviar",
                Location = new Point(260, 165),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.Click += async (s, e) => await SendEmailAsync();

            // Agregar controles al formulario
            this.Controls.Add(lblFile);
            this.Controls.Add(lblTo);
            this.Controls.Add(txtTo);
            this.Controls.Add(lblSubject);
            this.Controls.Add(txtSubject);
            this.Controls.Add(lblStatus);
            this.Controls.Add(btnSend);
        }

        private async Task SendEmailAsync()
        {
            string emailDestino = txtTo.Text.Trim();
            string asunto = txtSubject.Text.Trim();

            // 1. VALIDACIÓN DEL CORREO
            if (!IsValidEmail(emailDestino))
            {
                MessageBox.Show("Por favor, ingresa una dirección de correo válida.", "Formato Incorrecto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTo.Focus();
                return;
            }

            if (string.IsNullOrEmpty(asunto))
            {
                asunto = $"Archivo compartido: {Path.GetFileName(_filePath)}";
            }

            // Bloquear interfaz mientras envía
            btnSend.Enabled = false;
            txtTo.Enabled = false;
            txtSubject.Enabled = false;
            lblStatus.Text = "Enviando correo...";

            try
            {
                // 2. LÓGICA DE ENVÍO (SMTP)
                using var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(correoSistema, "File Explorer App");
                mailMessage.To.Add(emailDestino);
                mailMessage.Subject = asunto;
                mailMessage.Body = $"Hola,\n\nSe ha compartido un archivo contigo desde File Explorer.\n\nNombre del archivo: {Path.GetFileName(_filePath)}";

                // Adjuntar el archivo (envuelto en using para liberar el archivo después)
                using var attachment = new Attachment(_filePath);
                mailMessage.Attachments.Add(attachment);

                using var smtpClient = new SmtpClient("smtp.gmail.com", 587);
                smtpClient.Credentials = new NetworkCredential(correoSistema, passwordApp);
                smtpClient.EnableSsl = true;

                // Envío asíncrono
                await smtpClient.SendMailAsync(mailMessage);

                lblStatus.ForeColor = Color.LightGreen;
                lblStatus.Text = "¡Enviado con éxito!";
                MessageBox.Show("El archivo se envió correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.Close(); // Cerrar ventana al terminar
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error al enviar.";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"Ocurrió un error al intentar enviar el correo:\n\n{ex.Message}", "Error de SMTP", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Restaurar interfaz por si falló y quiere reintentar
                btnSend.Enabled = true;
                txtTo.Enabled = true;
                txtSubject.Enabled = true;
                if (lblStatus.Text == "Enviando correo...") lblStatus.Text = "";
            }
        }

        // Método auxiliar para validar el formato del correo
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            try
            {
                // MailAddress es la forma más segura y nativa de validar correos en C#
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}