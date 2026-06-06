using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  USER PROFILE
    //  Datos del usuario autenticado, compatibles con Google y GitHub.
    // ════════════════════════════════════════════════════════════════════════
    public sealed class UserProfile
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;  // solo GitHub
        public string AvatarUrl { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;  // "Google" | "GitHub" | "Guest"
        public string AccessToken { get; set; } = string.Empty;
        public bool IsGuest { get; set; }

        /// <summary>Nombre a mostrar: Name > Username > Email > "Invitado"</summary>
        public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name
                                   : !string.IsNullOrWhiteSpace(Username) ? Username
                                   : !string.IsNullOrWhiteSpace(Email) ? Email
                                   : "Invitado";

        /// <summary>Iniciales para el avatar de fallback.</summary>
        public string Initials
        {
            get
            {
                if (IsGuest) return "?";
                var words = DisplayName.Trim().Split(' ');
                return words.Length >= 2
                    ? $"{char.ToUpper(words[0][0])}{char.ToUpper(words[^1][0])}"
                    : DisplayName.Length > 0
                        ? char.ToUpper(DisplayName[0]).ToString()
                        : "?";
            }
        }

        public static UserProfile Guest() => new UserProfile
        {
            Id = "guest",
            Name = "Invitado",
            Provider = "Guest",
            IsGuest = true
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SESSION MANAGER
    //  Persiste la sesión en AppData/FileExplorerr/session.json
    //  para restaurarla automáticamente en el próximo inicio.
    // ════════════════════════════════════════════════════════════════════════
    public static class SessionManager
    {
        private static readonly string SessionPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FileExplorerr",
                "session.json");

        private static UserProfile? _current;

        /// <summary>Usuario actualmente autenticado. null si no hay sesión.</summary>
        public static UserProfile? Current => _current;

        /// <summary>Guarda el perfil en disco y lo establece como sesión actual.</summary>
        public static void Save(UserProfile profile)
        {
            _current = profile;
            if (profile.IsGuest) return;   // no persiste invitados

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
                // No guardar el access_token completo por seguridad;
                // solo metadatos de perfil.
                var dto = new
                {
                    profile.Id,
                    profile.Name,
                    profile.Email,
                    profile.Username,
                    profile.AvatarUrl,
                    profile.Provider,
                    SavedAt = DateTime.UtcNow
                };
                File.WriteAllText(SessionPath,
                    JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SessionManager.Save] {ex.Message}");
            }
        }

        /// <summary>
        /// Intenta restaurar la sesión desde disco.
        /// Devuelve el perfil si existe y es válido, null en caso contrario.
        /// </summary>
        public static UserProfile? TryRestore()
        {
            if (_current != null) return _current;
            try
            {
                if (!File.Exists(SessionPath)) return null;
                var json = File.ReadAllText(SessionPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Validar que no sea muy viejo (30 días)
                if (root.TryGetProperty("SavedAt", out var savedProp) &&
                    DateTime.TryParse(savedProp.GetString(), out var saved) &&
                    (DateTime.UtcNow - saved).TotalDays > 30)
                {
                    Clear();
                    return null;
                }

                _current = new UserProfile
                {
                    Id = root.TryGetProperty("Id", out var v0) ? v0.GetString() ?? "" : "",
                    Name = root.TryGetProperty("Name", out var v1) ? v1.GetString() ?? "" : "",
                    Email = root.TryGetProperty("Email", out var v2) ? v2.GetString() ?? "" : "",
                    Username = root.TryGetProperty("Username", out var v3) ? v3.GetString() ?? "" : "",
                    AvatarUrl = root.TryGetProperty("AvatarUrl", out var v4) ? v4.GetString() ?? "" : "",
                    Provider = root.TryGetProperty("Provider", out var v5) ? v5.GetString() ?? "" : "",
                };
                return _current;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Cierra sesión: elimina el archivo de sesión y limpia el estado.</summary>
        public static void Clear()
        {
            _current = null;
            try { if (File.Exists(SessionPath)) File.Delete(SessionPath); }
            catch { }
        }

        /// <summary>True si hay una sesión de usuario real (no invitado) guardada.</summary>
        public static bool HasSavedSession =>
            File.Exists(SessionPath);

        // ── Descarga del avatar ───────────────────────────────────────────
        private static readonly HttpClient _http = new HttpClient();
        private static readonly string AvatarCacheDir =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FileExplorerr", "avatars");

        /// <summary>
        /// Descarga y cachea el avatar del usuario.
        /// Devuelve el Bitmap o null si falla.
        /// </summary>
        public static async Task<Bitmap?> LoadAvatarAsync(UserProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.AvatarUrl)) return null;

            try
            {
                Directory.CreateDirectory(AvatarCacheDir);
                string cachePath = Path.Combine(AvatarCacheDir,
                    $"{profile.Provider}_{profile.Id}.png");

                // Usar caché si existe y es reciente (24h)
                if (File.Exists(cachePath) &&
                    (DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath)).TotalHours < 24)
                {
                    return new Bitmap(cachePath);
                }

                // Descargar
                var bytes = await _http.GetByteArrayAsync(profile.AvatarUrl);
                await File.WriteAllBytesAsync(cachePath, bytes);
                using var ms = new MemoryStream(bytes);
                return new Bitmap(ms);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SessionManager.LoadAvatarAsync] {ex.Message}");
                return null;
            }
        }
    }
}