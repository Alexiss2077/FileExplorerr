using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  LYRICS SERVICE
    //  Searches for song lyrics via lrclib.net.
    //  Previously implemented inline inside MusicPlayerForm.SearchLyrics().
    //
    //  Usage:
    //      var result = await LyricsService.SearchAsync(artist, title);
    //      if (result.Found) lyricsBox.Text = result.Lyrics;
    //      else              lyricsBox.Text = result.ErrorMessage;
    // ════════════════════════════════════════════════════════════════════════
    internal static class LyricsService
    {
        // ── HTTP client — shared, never disposed ──────────────────────────
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        static LyricsService()
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent", "FileExplorerr/1.0");
        }

        // ── Result type ───────────────────────────────────────────────────

        /// <summary>Outcome of a lyrics search.</summary>
        public sealed class LyricsResult
        {
            /// <summary>True when lyrics were found and <see cref="Lyrics"/> is populated.</summary>
            public bool Found { get; init; }

            /// <summary>Plain-text lyrics, or empty string when not found.</summary>
            public string Lyrics { get; init; } = string.Empty;

            /// <summary>Human-readable error or "not found" message.</summary>
            public string ErrorMessage { get; init; } = string.Empty;

            // ── Factory helpers ───────────────────────────────────────────
            internal static LyricsResult Ok(string lyrics) =>
                new() { Found = true, Lyrics = lyrics };

            internal static LyricsResult NotFound() =>
                new() { Found = false, ErrorMessage = "No encontrada." };

            internal static LyricsResult Error(string message) =>
                new() { Found = false, ErrorMessage = $"Error: {message}" };
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Searches lrclib.net for plain-text lyrics.
        /// Returns a <see cref="LyricsResult"/> in all cases (never throws).
        /// </summary>
        /// <param name="artist">Artist name (raw tag value).</param>
        /// <param name="title">Track title (raw tag value).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public static async Task<LyricsResult> SearchAsync(
            string artist,
            string title,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
                return LyricsResult.NotFound();

            string normalArtist = NormaliseForSearch(artist);
            string normalTitle = NormaliseForSearch(title);

            if (string.IsNullOrWhiteSpace(normalArtist) || string.IsNullOrWhiteSpace(normalTitle))
                return LyricsResult.NotFound();

            try
            {
                string url =
                    $"https://lrclib.net/api/get" +
                    $"?artist_name={Uri.EscapeDataString(normalArtist)}" +
                    $"&track_name={Uri.EscapeDataString(normalTitle)}";

                using var response = await _http.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return LyricsResult.NotFound();

                string json = await response.Content.ReadAsStringAsync(cancellationToken);

                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("plainLyrics", out var lyrProp))
                {
                    string? lyrics = lyrProp.GetString();
                    if (!string.IsNullOrWhiteSpace(lyrics))
                        return LyricsResult.Ok(lyrics);
                }

                return LyricsResult.NotFound();
            }
            catch (OperationCanceledException)
            {
                return LyricsResult.Error("Búsqueda cancelada.");
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LyricsService] HTTP error: {ex.Message}");
                return LyricsResult.Error(ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LyricsService] Unexpected: {ex.Message}");
                return LyricsResult.Error(ex.Message);
            }
        }

        // ── Normalisation (same logic that was in MusicPlayerForm) ────────

        private static string NormaliseForSearch(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            string s = raw.ToLower().Trim();

            // Strip featured artists to improve search accuracy.
            foreach (string sep in new[]
                { " feat.", " feat ", " ft.", " ft ", " featuring " })
            {
                int idx = s.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (idx > 0) { s = s[..idx]; break; }
            }

            return s.Trim();
        }
    }
}