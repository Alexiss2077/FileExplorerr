using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileExplorerr
{
    public class CoverSearchResult
    {
        public string Url { get; set; }
        public string Fuente { get; set; }
        public double Similitud { get; set; }
        public string Artista { get; set; }
        public string Titulo { get; set; }
    }

    public class CoverSearcher
    {
        private static readonly Dictionary<string, CoverSearchResult> _cache =
            new Dictionary<string, CoverSearchResult>();

        private readonly HttpClient _httpClient;

        public CoverSearcher()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FileExplorerr/1.0");
        }

        /// <summary>
        /// Busca cover con múltiples fuentes y retorna la mejor opción
        /// </summary>
        public async Task<CoverSearchResult> BuscarMejorCover(string artista, string titulo)
        {
            string cacheKey = NormalizarTexto($"{artista}_{titulo}");

            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            string artistaLimpio = ExtraerArtistaPrincipal(artista);
            artistaLimpio = NormalizarTexto(artistaLimpio);
            string tituloLimpio = NormalizarTexto(titulo);

            var resultados = new List<CoverSearchResult>();

            var itunesResults = await BuscarEnITunes(artistaLimpio, tituloLimpio, artista, titulo);
            if (itunesResults != null) resultados.AddRange(itunesResults);

            var lastFmResults = await BuscarEnLastFm(artistaLimpio, tituloLimpio, artista, titulo);
            if (lastFmResults != null) resultados.AddRange(lastFmResults);

            var spotifyResults = await BuscarEnSpotify(artistaLimpio, tituloLimpio, artista, titulo);
            if (spotifyResults != null) resultados.AddRange(spotifyResults);

            CoverSearchResult mejor = resultados
                .OrderByDescending(r => r.Similitud)
                .FirstOrDefault();

            if (mejor != null && mejor.Similitud > 0.5)
            {
                _cache[cacheKey] = mejor;
                return mejor;
            }

            return null;
        }

        /// <summary>
        /// Busca en iTunes API
        /// </summary>
        private async Task<List<CoverSearchResult>> BuscarEnITunes(string artistaLimpio, string tituloLimpio,
            string artistaOriginal, string tituloOriginal)
        {
            try
            {
                string query = Uri.EscapeDataString($"{artistaLimpio} {tituloLimpio}");
                string url = $"https://itunes.apple.com/search?term={query}&limit=20&entity=song";

                string json = await _httpClient.GetStringAsync(url);

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var results = doc.RootElement.GetProperty("results");
                    var covers = new List<CoverSearchResult>();

                    for (int i = 0; i < Math.Min(results.GetArrayLength(), 20); i++)
                    {
                        var item = results[i];

                        string artistaAPI = NormalizarTexto(item.GetProperty("artistName").GetString());
                        string tituloAPI = NormalizarTexto(item.GetProperty("trackName").GetString());
                        string coverUrl = item.GetProperty("artworkUrl100").GetString();

                        coverUrl = coverUrl.Replace("100x100", "600x600");

                        double similitud = CalcularSimilitudAvanzada(artistaLimpio, tituloLimpio,
                            artistaAPI, tituloAPI);

                        if (similitud > 0.4)
                        {
                            covers.Add(new CoverSearchResult
                            {
                                Url = coverUrl,
                                Fuente = "iTunes",
                                Similitud = similitud,
                                Artista = artistaAPI,
                                Titulo = tituloAPI
                            });
                        }
                    }

                    return covers.Count > 0 ? covers : null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en iTunes: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Busca en Last.fm API
        /// </summary>
        private async Task<List<CoverSearchResult>> BuscarEnLastFm(string artistaLimpio, string tituloLimpio,
            string artistaOriginal, string tituloOriginal)
        {
            try
            {
                string url = $"http://ws.audioscrobbler.com/2.0/?method=track.search&track={Uri.EscapeDataString(tituloLimpio)}" +
                    $"&artist={Uri.EscapeDataString(artistaLimpio)}&limit=10&format=json";

                string json = await _httpClient.GetStringAsync(url);

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (!doc.RootElement.TryGetProperty("results", out var results))
                        return null;

                    if (!results.TryGetProperty("trackmatches", out var trackMatches))
                        return null;

                    if (!trackMatches.TryGetProperty("track", out var tracks))
                        return null;

                    var covers = new List<CoverSearchResult>();
                    var tracksArray = tracks.ValueKind == JsonValueKind.Array
                        ? tracks.EnumerateArray().ToList()
                        : new List<JsonElement> { tracks };

                    foreach (var track in tracksArray)
                    {
                        try
                        {
                            string artistaLfm = NormalizarTexto(track.GetProperty("artist").GetString());
                            string tituloLfm = NormalizarTexto(track.GetProperty("name").GetString());

                            if (track.TryGetProperty("image", out var imageArray))
                            {
                                var images = imageArray.EnumerateArray().ToList();
                                var largeImage = images.LastOrDefault();

                                if (largeImage.ValueKind != JsonValueKind.Undefined &&
                                    largeImage.TryGetProperty("#text", out var textProp))
                                {
                                    string coverUrl = textProp.GetString();

                                    if (!string.IsNullOrWhiteSpace(coverUrl) && !coverUrl.EndsWith("back"))
                                    {
                                        double similitud = CalcularSimilitudAvanzada(artistaLimpio, tituloLimpio,
                                            artistaLfm, tituloLfm);

                                        if (similitud > 0.4)
                                        {
                                            covers.Add(new CoverSearchResult
                                            {
                                                Url = coverUrl,
                                                Fuente = "Last.fm",
                                                Similitud = similitud,
                                                Artista = artistaLfm,
                                                Titulo = tituloLfm
                                            });
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    return covers.Count > 0 ? covers : null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en Last.fm: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Busca en Spotify API
        /// </summary>
        private async Task<List<CoverSearchResult>> BuscarEnSpotify(string artistaLimpio, string tituloLimpio,
            string artistaOriginal, string tituloOriginal)
        {
            try
            {
                string query = Uri.EscapeDataString($"track:{tituloLimpio} artist:{artistaLimpio}");
                string url = $"https://api.spotify.com/v1/search?q={query}&type=track&limit=10";

                var request = new HttpRequestMessage(HttpMethod.Get, url);

                try
                {
                    var response = await _httpClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                        return null;

                    string json = await response.Content.ReadAsStringAsync();

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        if (!doc.RootElement.TryGetProperty("tracks", out var tracksObj))
                            return null;

                        if (!tracksObj.TryGetProperty("items", out var items))
                            return null;

                        var covers = new List<CoverSearchResult>();

                        foreach (var track in items.EnumerateArray())
                        {
                            try
                            {
                                var artists = track.GetProperty("artists").EnumerateArray().FirstOrDefault();
                                string artistaSpotify = NormalizarTexto(artists.GetProperty("name").GetString());
                                string tituloSpotify = NormalizarTexto(track.GetProperty("name").GetString());

                                if (track.TryGetProperty("album", out var album) &&
                                    album.TryGetProperty("images", out var images))
                                {
                                    var imageArray = images.EnumerateArray().FirstOrDefault();

                                    if (imageArray.TryGetProperty("url", out var urlProp))
                                    {
                                        string coverUrl = urlProp.GetString();

                                        double similitud = CalcularSimilitudAvanzada(artistaLimpio, tituloLimpio,
                                            artistaSpotify, tituloSpotify);

                                        if (similitud > 0.4)
                                        {
                                            covers.Add(new CoverSearchResult
                                            {
                                                Url = coverUrl,
                                                Fuente = "Spotify",
                                                Similitud = similitud,
                                                Artista = artistaSpotify,
                                                Titulo = tituloSpotify
                                            });
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        return covers.Count > 0 ? covers : null;
                    }
                }
                catch { return null; }
            }
            catch { return null; }
        }

        /// <summary>
        /// Algoritmo mejorado de similitud que considera múltiples factores
        /// </summary>
        public double CalcularSimilitudAvanzada(string artistaBuscado, string tituloBuscado,
            string artistaEncontrado, string tituloEncontrado)
        {
            if (string.IsNullOrEmpty(artistaBuscado) || string.IsNullOrEmpty(tituloBuscado) ||
                string.IsNullOrEmpty(artistaEncontrado) || string.IsNullOrEmpty(tituloEncontrado))
                return 0;

            if (artistaBuscado == artistaEncontrado && tituloBuscado == tituloEncontrado)
                return 1.0;

            double similitudArtista = CalcularSimilitudFuzzy(artistaBuscado, artistaEncontrado);
            double similitudTitulo = CalcularSimilitudFuzzy(tituloBuscado, tituloEncontrado);
            double similitudPalabras = CalcularSimilitudPalabras(artistaBuscado, artistaEncontrado) * 0.5 +
                                      CalcularSimilitudPalabras(tituloBuscado, tituloEncontrado) * 0.5;

            double similitudFinal = (similitudArtista * 0.35) + (similitudTitulo * 0.50) + (similitudPalabras * 0.15);

            return Math.Min(1.0, similitudFinal);
        }

        private double CalcularSimilitudFuzzy(string s1, string s2)
        {
            if (s1 == s2) return 1.0;

            int distancia = DistanciaLevenshtein(s1, s2);
            int maxLongitud = Math.Max(s1.Length, s2.Length);

            if (maxLongitud == 0) return 1.0;

            double similitud = 1.0 - ((double)distancia / maxLongitud);

            return Math.Pow(similitud, 0.8);
        }

        private double CalcularSimilitudPalabras(string s1, string s2)
        {
            var palabras1 = s1.Split(new[] { ' ', '-', '&' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.Length > 2)
                .ToList();

            var palabras2 = s2.Split(new[] { ' ', '-', '&' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.Length > 2)
                .ToList();

            if (palabras1.Count == 0 || palabras2.Count == 0)
                return 0;

            var palabrasComunes = palabras1.Where(p => palabras2.Any(p2 => p2.StartsWith(p) || p.StartsWith(p2))).Count();

            return (double)palabrasComunes / Math.Max(palabras1.Count, palabras2.Count);
        }

        private int DistanciaLevenshtein(string s1, string s2)
        {
            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;

            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;

            for (int j = 1; j <= s2.Length; j++)
            {
                for (int i = 1; i <= s1.Length; i++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }

            return d[s1.Length, s2.Length];
        }

        private string ExtraerArtistaPrincipal(string artista)
        {
            if (string.IsNullOrWhiteSpace(artista))
                return "";

            string[] separadores = { " feat.", " feat ", " ft.", " ft ", " featuring ", " & ", " and ", "," };

            string resultado = artista;

            foreach (var sep in separadores)
            {
                int index = resultado.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    resultado = resultado.Substring(0, index);
                    break;
                }
            }

            return resultado.Trim();
        }

        public string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "";

            texto = texto.Replace("\r", "").Replace("\n", "").Trim().ToLower();

            string normalized = texto.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            texto = sb.ToString().Normalize(NormalizationForm.FormC);

            string[] stopwords =
            {
                "feat.", "ft.", "featuring", "remix", "official", "video", "audio",
                "lyrics", "letra", "hq", "4k", "prod.", "producer", "album version",
                "version", "edit", "extended", "radio edit", "remaster", "remastered",
                "live", "acoustic", "unplugged", "cover", "instrumental", "explicit",
                "clean", "deluxe", "edition", "ep", "single"
            };

            foreach (var word in stopwords)
            {
                int index = texto.IndexOf(word);
                if (index != -1)
                    texto = texto.Substring(0, index);
            }

            while (texto.Contains("(") && texto.Contains(")"))
            {
                int a = texto.IndexOf("(");
                int b = texto.IndexOf(")");

                if (b > a)
                    texto = texto.Remove(a, b - a + 1);
                else break;
            }

            while (texto.Contains("[") && texto.Contains("]"))
            {
                int a = texto.IndexOf("[");
                int b = texto.IndexOf("]");

                if (b > a)
                    texto = texto.Remove(a, b - a + 1);
                else break;
            }

            texto = texto.Replace("&", "and");
            texto = Regex.Replace(texto, @"[^\w\s]", " ");

            while (texto.Contains("  "))
                texto = texto.Replace("  ", " ");

            return texto.Trim();
        }

        public void LimpiarCache()
        {
            _cache.Clear();
        }
    }
}