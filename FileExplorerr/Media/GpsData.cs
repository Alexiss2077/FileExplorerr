using System;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  GPS DATA
    //  Immutable record returned by GpsReader.Read().
    //  Previously defined as a nested record inside GpsReader.cs.
    // ════════════════════════════════════════════════════════════════════════
    public sealed record GpsData(
        double Latitude,
        double Longitude,
        double? Altitude,
        string LatRef,
        string LonRef,
        string? Date,
        string? CameraModel,
        string? Software)
    {
        // ── Derived properties ────────────────────────────────────────────

        /// <summary>True when coordinates are not both zero.</summary>
        public bool HasGps => Latitude != 0 || Longitude != 0;

        /// <summary>Formatted latitude string, e.g. "27° 3' 28.50\" N".</summary>
        public string LatString => FormatDms(Math.Abs(Latitude), LatRef == "S" ? "S" : "N");

        /// <summary>Formatted longitude string, e.g. "101° 32' 36.97\" W".</summary>
        public string LonString => FormatDms(Math.Abs(Longitude), LonRef == "W" ? "W" : "E");

        // ── Helpers ───────────────────────────────────────────────────────

        private static string FormatDms(double decimalDegrees, string direction)
        {
            int degrees = (int)decimalDegrees;
            double minFull = (decimalDegrees - degrees) * 60;
            int minutes = (int)minFull;
            double seconds = (minFull - minutes) * 60;
            return $"{degrees}° {minutes}' {seconds:0.00}\" {direction}";
        }
    }
}