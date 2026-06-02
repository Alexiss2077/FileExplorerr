namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  COVER SEARCH RESULT
    //  Data returned by CoverSearchService / CoverSearcher for a single
    //  album-art candidate found in an external source.
    //  Previously defined as a public class inside CoverSearcher.cs.
    // ════════════════════════════════════════════════════════════════════════
    public sealed class CoverSearchResult
    {
        /// <summary>Direct URL to the artwork image.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Name of the source that returned this result (iTunes, Last.fm, Spotify).</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Similarity score in [0, 1] between the query and this result.
        /// Higher is better; results below 0.5 are typically discarded.
        /// </summary>
        public double Similarity { get; set; }

        /// <summary>Artist name as returned by the source.</summary>
        public string Artist { get; set; } = string.Empty;

        /// <summary>Track or album title as returned by the source.</summary>
        public string Title { get; set; } = string.Empty;
    }
}