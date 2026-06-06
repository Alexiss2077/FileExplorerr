using System;
using System.IO;
using System.Text.Json;

namespace FileExplorerr
{
    internal static class OAuthConfig
    {
        private static JsonElement _root;
        private static bool _loaded;

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            string path = Path.Combine(
                AppContext.BaseDirectory, "appsettings.json");

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "No se encontró appsettings.json.\n" +
                    "Copia appsettings.example.json, renómbralo a " +
                    "appsettings.json y rellena tus credenciales OAuth.");

            var json = File.ReadAllText(path);
            _root = JsonDocument.Parse(json).RootElement;
            _loaded = true;
        }

        public static string GoogleClientId
            => Get("OAuth", "Google", "ClientId");

        public static string GoogleClientSecret
            => Get("OAuth", "Google", "ClientSecret");

        public static string GitHubClientId
            => Get("OAuth", "GitHub", "ClientId");

        public static string GitHubClientSecret
            => Get("OAuth", "GitHub", "ClientSecret");

        private static string Get(params string[] keys)
        {
            EnsureLoaded();
            var el = _root;
            foreach (var key in keys)
            {
                if (!el.TryGetProperty(key, out el))
                    return string.Empty;
            }
            return el.GetString() ?? string.Empty;
        }
    }
}