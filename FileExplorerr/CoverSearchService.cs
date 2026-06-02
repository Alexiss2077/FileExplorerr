using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  COVER SEARCH SERVICE
    //  High-level façade for album-art retrieval.
    //  - Delegates multi-source search to CoverSearcher (iTunes, Last.fm, Spotify).
    //  - Provides a convenience method that downloads the image bytes directly,
    //    which was previously implemented inline in MusicPlayerForm.LoadCover().
    //
    //  Usage:
    //      byte[]? art = await CoverSearchService.FetchCoverBytesAsync(artist, title);
    //      if (art != null) { /* save to tag, display, etc. */ }
    // ════════════════════════════════════════════════════════════════════════
    internal static class CoverSearchService
    {
        // ── HTTP client — shared, never disposed ──────────────────────────
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        static CoverSearchService()
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent", "FileExplorerr/1.0");
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Searches for the best cover for <paramref name="artist"/> /
        /// <paramref name="title"/> across all configured sources and returns
        /// the raw image bytes, or <c>null</c> if nothing was found.
        /// </summary>
        public static async Task<byte[]?> FetchCoverBytesAsync(
            string artist,
            string title,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
                return null;

            try
            {
                var searcher = new CoverSearcher();
                CoverSearchResult? best =
                    await searcher.BuscarMejorCover(artist, title);

                if (best is null || string.IsNullOrWhiteSpace(best.Url))
                    return null;

                byte[] bytes = await _http.GetByteArrayAsync(best.Url, cancellationToken);
                return bytes.Length > 0 ? bytes : null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CoverSearchService.FetchCoverBytesAsync] {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the <see cref="CoverSearchResult"/> with the highest similarity
        /// without downloading the image. Useful when only the URL is needed.
        /// </summary>
        public static async Task<CoverSearchResult?> FindBestAsync(
            string artist,
            string title,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
                return null;

            try
            {
                var searcher = new CoverSearcher();
                return await searcher.BuscarMejorCover(artist, title);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CoverSearchService.FindBestAsync] {ex.Message}");
                return null;
            }
        }

        // ── iTunes quick-search (used directly by MusicPlayerForm) ────────

        /// <summary>
        /// Downloads cover art by querying the iTunes Search API directly.
        /// This mirrors the logic that was previously inlined in
        /// MusicPlayerForm.LoadCover() and returns raw image bytes or null.
        /// </summary>
        public static async Task<byte[]?> FetchFromITunesAsync(
            string artist,
            string title,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
                return null;

            try
            {
                string query =
                    Uri.EscapeDataString($"{artist.Trim()} {title.Trim()}");

                string json = await _http.GetStringAsync(
                    $"https://itunes.apple.com/search?term={query}&limit=3&entity=song",
                    cancellationToken);

                using var doc = JsonDocument.Parse(json);
                var results = doc.RootElement.GetProperty("results");

                if (results.GetArrayLength() == 0)
                    return null;

                string coverUrl = results[0]
                    .GetProperty("artworkUrl100")
                    .GetString()!
                    .Replace("100x100", "600x600");

                byte[] bytes = await _http.GetByteArrayAsync(coverUrl, cancellationToken);
                return bytes.Length > 0 ? bytes : null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CoverSearchService.FetchFromITunesAsync] {ex.Message}");
                return null;
            }
        }
    }
}