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
    // ════════════════════════════════════════════════════════════════════════
    //  COVER SEARCHER
    //  Multi-source album-art finder (iTunes, Last.fm, Spotify).
    //  CoverSearchResult has been extracted to CoverSearchResult.cs.
    //
    //  For high-level usage (fetch bytes directly) see CoverSearchService.cs.
    // ════════════════════════════════════════════════════════════════════════
    public class CoverSearcher
    {
        // ── Cache ─────────────────────────────────────────────────────────
        private static readonly Dictionary<string, CoverSearchResult> _cache =
            new(StringComparer.Ordinal);

        // ── HTTP client — static, never disposed ──────────────────────────
        private static readonly HttpClient _httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent", "FileExplorerr/1.0");
            return client;
        }

        // ── Similarity threshold ──────────────────────────────────────────
        private const double MinSimilarityThreshold = 0.5;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Searches all sources in parallel and returns the best cover, or
        /// <c>null</c> if no result exceeds the similarity threshold.
        /// </summary>
        public async Task<CoverSearchResult?> BuscarMejorCover(string artista, string titulo)
        {
            string cacheKey = NormalizarTexto($"{artista}_{titulo}");

            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            string artistaLimpio = NormalizarTexto(ExtraerArtistaPrincipal(artista));
            string tituloLimpio = NormalizarTexto(titulo);

            var tasks = new[]
            {
                BuscarEnITunes(artistaLimpio, tituloLimpio),
                BuscarEnLastFm(artistaLimpio, tituloLimpio),
                BuscarEnSpotify(artistaLimpio, tituloLimpio)
            };

            var allResults = await Task.WhenAll(tasks);

            var resultados = allResults
                .Where(r => r is not null)
                .SelectMany(r => r!)
                .ToList();

            CoverSearchResult? best = resultados
                .OrderByDescending(r => r.Similarity)
                .FirstOrDefault();

            if (best is not null && best.Similarity > MinSimilarityThreshold)
            {
                _cache[cacheKey] = best;
                return best;
            }

            return null;
        }

        // ── Source: iTunes ────────────────────────────────────────────────

        private static async Task<List<CoverSearchResult>?> BuscarEnITunes(
            string artistaLimpio, string tituloLimpio)
        {
            try
            {
                string query = Uri.EscapeDataString($"{artistaLimpio} {tituloLimpio}");
                string url = $"https://itunes.apple.com/search?term={query}&limit=20&entity=song";
                string json = await _httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var items = doc.RootElement.GetProperty("results");
                var covers = new List<CoverSearchResult>();
                int count = Math.Min(items.GetArrayLength(), 20);

                for (int i = 0; i < count; i++)
                {
                    var item = items[i];
                    string apiArt = NormalizarTexto(item.GetProperty("artistName").GetString() ?? string.Empty);
                    string apiTit = NormalizarTexto(item.GetProperty("trackName").GetString() ?? string.Empty);
                    string coverUrl = (item.GetProperty("artworkUrl100").GetString() ?? string.Empty)
                        .Replace("100x100", "600x600");

                    double sim = CalcularSimilitudAvanzada(artistaLimpio, tituloLimpio, apiArt, apiTit);
                    if (sim > 0.4)
                        covers.Add(new CoverSearchResult
                        {
                            Url = coverUrl,
                            Source = "iTunes",
                            Similarity = sim,
                            Artist = apiArt,
                            Title = apiTit
                        });
                }

                return covers.Count > 0 ? covers : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoverSearcher.iTunes] {ex.Message}");
                return null;
            }
        }

        // ── Source: Last.fm ───────────────────────────────────────────────

        private static async Task<List<CoverSearchResult>?> BuscarEnLastFm(
            string artistaLimpio, string tituloLimpio)
        {
            try
            {
                string url =
                    $"http://ws.audioscrobbler.com/2.0/?method=track.search" +
                    $"&track={Uri.EscapeDataString(tituloLimpio)}" +
                    $"&artist={Uri.EscapeDataString(artistaLimpio)}" +
                    $"&limit=10&format=json";

                string json = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("results", out var results)) return null;
                if (!results.TryGetProperty("trackmatches", out var trackMatches)) return null;
                if (!trackMatches.TryGetProperty("track", out var tracks)) return null;

                var tracksArray = tracks.ValueKind == JsonValueKind.Array
                    ? tracks.EnumerateArray().ToList()
                    : new List<JsonElement> { tracks };

                var covers = new List<CoverSearchResult>();

                foreach (var track in tracksArray)
                {
                    try
                    {
                        string lfmArt = NormalizarTexto(track.GetProperty("artist").GetString() ?? string.Empty);
                        string lfmTit = NormalizarTexto(track.GetProperty("name").GetString() ?? string.Empty);

                        if (!track.TryGetProperty("image", out var imageArray)) continue;

                        var images = imageArray.EnumerateArray().ToList();
                        var largeImage = images.LastOrDefault();

                        if (largeImage.ValueKind == JsonValueKind.Undefined ||
                            !largeImage.TryGetProperty("#text", out var textProp)) continue;

                        string coverUrl = textProp.GetString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(coverUrl) || coverUrl.EndsWith("back"))
                            continue;

                        double sim = CalcularSimilitudAvanzada(artistaLimpio, tituloLimpio, lfmArt, lfmTit);
                        if (sim > 0.4)
                            covers.Add(new CoverSearchResult
                            {
                                Url = coverUrl,
                                Source = "Last.fm",
                                Similarity = sim,
                                Artist = lfmArt,
                                Title = lfmTit
                            });
                    }
                    catch (Exception innerEx)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[CoverSearcher.LastFm] track parse: {innerEx.Message}");
                    }
                }

                return covers.Count > 0 ? covers : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoverSearcher.LastFm] {ex.Message}");
                return null;
            }
        }

        // ── Source: Spotify (unauthenticated — best-effort) ───────────────

        private static async Task<List<CoverSearchResult>?> BuscarEnSpotify(
            string artistaLimpio, string tituloLimpio)
        {
            try
            {
                string query = Uri.EscapeDataString($"track:{tituloLimpio} artist:{artistaLimpio}");
                string url = $"https://api.spotify.com/v1/search?q={query}&type=track&limit=10";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("tracks", out var tracksObj)) return null;
                if (!tracksObj.TryGetProperty("items", out var items)) return null;

                var covers = new List<CoverSearchResult>();

                foreach (var track in items.EnumerateArray())
                {
                    try
                    {
                        var firstArtist = track.GetProperty("artists").EnumerateArray().FirstOrDefault();
                        string spArt = NormalizarTexto(firstArtist.GetProperty("name").GetString() ?? string.Empty);
                        string spTit = NormalizarTexto(track.GetProperty("name").GetString() ?? string.Empty);

                        if (!track.TryGetProperty("album", out var album)) continue;
                        if (!album.TryGetProperty("images", out var images)) continue;

                        var firstImage = images.EnumerateArray().FirstOrDefault();
                        if (!firstImage.TryGetProperty("url", out var urlProp)) continue;

                        string coverUrl = urlProp.GetString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(coverUrl)) continue;

                        double sim = CalcularSimilitudAvanzada(artistaLimpio, tituloLimpio, spArt, spTit);
                        if (sim > 0.4)
                            covers.Add(new CoverSearchResult
                            {
                                Url = coverUrl,
                                Source = "Spotify",
                                Similarity = sim,
                                Artist = spArt,
                                Title = spTit
                            });
                    }
                    catch (Exception innerEx)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[CoverSearcher.Spotify] track parse: {innerEx.Message}");
                    }
                }

                return covers.Count > 0 ? covers : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoverSearcher.Spotify] {ex.Message}");
                return null;
            }
        }

        // ── Similarity algorithm ──────────────────────────────────────────

        private static double CalcularSimilitudAvanzada(
            string artistaBuscado, string tituloBuscado,
            string artistaEncontrado, string tituloEncontrado)
        {
            if (string.IsNullOrEmpty(artistaBuscado) || string.IsNullOrEmpty(tituloBuscado) ||
                string.IsNullOrEmpty(artistaEncontrado) || string.IsNullOrEmpty(tituloEncontrado))
                return 0;

            if (artistaBuscado == artistaEncontrado && tituloBuscado == tituloEncontrado)
                return 1.0;

            double simArtist = SimilitudFuzzy(artistaBuscado, artistaEncontrado);
            double simTitle = SimilitudFuzzy(tituloBuscado, tituloEncontrado);
            double simWords = SimilitudPalabras(artistaBuscado, artistaEncontrado) * 0.5
                              + SimilitudPalabras(tituloBuscado, tituloEncontrado) * 0.5;

            return Math.Min(1.0,
                simArtist * 0.35 +
                simTitle * 0.50 +
                simWords * 0.15);
        }

        private static double SimilitudFuzzy(string s1, string s2)
        {
            if (s1 == s2) return 1.0;
            int distance = LevenshteinDistance(s1, s2);
            int maxLength = Math.Max(s1.Length, s2.Length);
            if (maxLength == 0) return 1.0;
            return Math.Pow(1.0 - ((double)distance / maxLength), 0.8);
        }

        private static double SimilitudPalabras(string s1, string s2)
        {
            var words1 = s1.Split(new[] { ' ', '-', '&' }, StringSplitOptions.RemoveEmptyEntries)
                           .Where(p => p.Length > 2).ToList();
            var words2 = s2.Split(new[] { ' ', '-', '&' }, StringSplitOptions.RemoveEmptyEntries)
                           .Where(p => p.Length > 2).ToList();

            if (words1.Count == 0 || words2.Count == 0) return 0;

            int common = words1.Count(p => words2.Any(p2 => p2.StartsWith(p) || p.StartsWith(p2)));
            return (double)common / Math.Max(words1.Count, words2.Count);
        }

        private static int LevenshteinDistance(string s1, string s2)
        {
            int[,] d = new int[s1.Length + 1, s2.Length + 1];
            for (int i = 0; i <= s1.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++) d[0, j] = j;

            for (int j = 1; j <= s2.Length; j++)
                for (int i = 1; i <= s1.Length; i++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }

            return d[s1.Length, s2.Length];
        }

        // ── Text normalisation ────────────────────────────────────────────

        private static readonly string[] _stopwords =
        {
            "feat.", "ft.", "featuring", "remix", "official", "video", "audio",
            "lyrics", "letra", "hq", "4k", "prod.", "producer", "album version",
            "version", "edit", "extended", "radio edit", "remaster", "remastered",
            "live", "acoustic", "unplugged", "cover", "instrumental", "explicit",
            "clean", "deluxe", "edition", "ep", "single"
        };

        public static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            texto = texto.Replace("\r", string.Empty)
                         .Replace("\n", string.Empty)
                         .Trim()
                         .ToLower();

            // Strip diacritics.
            string normalized = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalized)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            texto = sb.ToString().Normalize(NormalizationForm.FormC);

            // Strip stopwords.
            foreach (string word in _stopwords)
            {
                int idx = texto.IndexOf(word, StringComparison.Ordinal);
                if (idx != -1) texto = texto[..idx];
            }

            // Strip parentheses and brackets.
            while (texto.Contains('(') && texto.Contains(')'))
            {
                int a = texto.IndexOf('('), b = texto.IndexOf(')');
                if (b > a) texto = texto.Remove(a, b - a + 1); else break;
            }
            while (texto.Contains('[') && texto.Contains(']'))
            {
                int a = texto.IndexOf('['), b = texto.IndexOf(']');
                if (b > a) texto = texto.Remove(a, b - a + 1); else break;
            }

            texto = texto.Replace("&", "and");
            texto = Regex.Replace(texto, @"[^\w\s]", " ");

            // Collapse multiple spaces.
            while (texto.Contains("  "))
                texto = texto.Replace("  ", " ");

            return texto.Trim();
        }

        private static string ExtraerArtistaPrincipal(string artista)
        {
            if (string.IsNullOrWhiteSpace(artista)) return string.Empty;

            string[] separadores =
                { " feat.", " feat ", " ft.", " ft ", " featuring ", " & ", " and ", "," };

            string resultado = artista;
            foreach (string sep in separadores)
            {
                int idx = resultado.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (idx > 0) { resultado = resultado[..idx]; break; }
            }

            return resultado.Trim();
        }

        // ── Cache management ──────────────────────────────────────────────

        public void LimpiarCache() => _cache.Clear();
    }
}