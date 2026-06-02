using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  GPS READER
    //  Extracts GPS coordinates from images (EXIF) and videos (QuickTime atoms).
    //  The GpsData record has been moved to GpsData.cs.
    // ════════════════════════════════════════════════════════════════════════
    internal static class GpsReader
    {
        // ── EXIF Property IDs ────────────────────────────────────────────────
        private const int TagGpsLatRef = 0x0001;
        private const int TagGpsLat = 0x0002;
        private const int TagGpsLonRef = 0x0003;
        private const int TagGpsLon = 0x0004;
        private const int TagGpsAltRef = 0x0005;
        private const int TagGpsAlt = 0x0006;
        private const int TagGpsDate = 0x001D;
        private const int TagMake = 0x010F;
        private const int TagModel = 0x0110;
        private const int TagDateOrig = 0x9003;
        private const int TagSoftware = 0x0131;

        // ── Max video bytes to scan for GPS atoms ────────────────────────────
        private const int MaxVideoScanBytes = 50 * 1024 * 1024; // 50 MB

        // ════════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Attempts to read GPS data from <paramref name="filePath"/>.
        /// Returns <c>null</c> when no GPS data is present or the format is
        /// unsupported.
        /// </summary>
        public static GpsData? Read(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (IsImageExtension(ext)) return ReadFromImage(filePath);
            if (IsVideoExtension(ext)) return ReadFromVideo(filePath);
            return null;
        }

        // ════════════════════════════════════════════════════════════════════
        //  IMAGE — EXIF via System.Drawing
        // ════════════════════════════════════════════════════════════════════

        private static GpsData? ReadFromImage(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open,
                                               FileAccess.Read, FileShare.ReadWrite);
                using var img = Image.FromStream(fs,
                    useEmbeddedColorManagement: false,
                    validateImageData: false);

                var props = img.PropertyItems;
                if (props is null || props.Length == 0) return null;

                PropertyItem? GetProp(int id)
                {
                    foreach (var p in props)
                        if (p.Id == id) return p;
                    return null;
                }

                var latProp = GetProp(TagGpsLat);
                var lonProp = GetProp(TagGpsLon);
                if (latProp is null || lonProp is null) return null;

                double lat = RationalToDecimalDegrees(latProp.Value!);
                double lon = RationalToDecimalDegrees(lonProp.Value!);
                if (lat == 0 && lon == 0) return null;

                var latRefProp = GetProp(TagGpsLatRef);
                var lonRefProp = GetProp(TagGpsLonRef);
                string latRef = latRefProp?.Value is not null
                    ? Encoding.ASCII.GetString(latRefProp.Value).Trim('\0') : "N";
                string lonRef = lonRefProp?.Value is not null
                    ? Encoding.ASCII.GetString(lonRefProp.Value).Trim('\0') : "E";

                if (latRef == "S") lat = -lat;
                if (lonRef == "W") lon = -lon;

                // Altitude.
                double? alt = null;
                var altProp = GetProp(TagGpsAlt);
                if (altProp?.Value is not null)
                {
                    double av = RationalToDouble(altProp.Value, 0);
                    var altRefProp = GetProp(TagGpsAltRef);
                    if (altRefProp?.Value is not null && altRefProp.Value[0] == 1) av = -av;
                    alt = av;
                }

                // Date.
                string? date = null;
                var dateProp = GetProp(TagGpsDate) ?? GetProp(TagDateOrig);
                if (dateProp?.Value is not null)
                    date = Encoding.ASCII.GetString(dateProp.Value).Trim('\0').Replace(':', '/');

                // Camera model.
                string? camera = null;
                var makeProp = GetProp(TagMake);
                var modelProp = GetProp(TagModel);
                string? make = makeProp?.Value is not null
                    ? Encoding.ASCII.GetString(makeProp.Value).Trim('\0', ' ') : null;
                string? model = modelProp?.Value is not null
                    ? Encoding.ASCII.GetString(modelProp.Value).Trim('\0', ' ') : null;
                if (make is not null || model is not null)
                    camera = string.Join(" ",
                        new[] { make, model }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));

                // Software.
                string? software = null;
                var swProp = GetProp(TagSoftware);
                if (swProp?.Value is not null)
                    software = Encoding.ASCII.GetString(swProp.Value).Trim('\0', ' ');

                return new GpsData(lat, lon, alt, latRef, lonRef, date, camera, software);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GpsReader.Image] {ex.Message}");
                return null;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  VIDEO — QuickTime / MP4 atom scanning
        // ════════════════════════════════════════════════════════════════════

        private static GpsData? ReadFromVideo(string filePath)
        {
            try
            {
                return ReadMp4GpsAtom(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GpsReader.Video] {ex.Message}");
                return null;
            }
        }

        private static GpsData? ReadMp4GpsAtom(string filePath)
        {
            long fileSize = new FileInfo(filePath).Length;
            int readBytes = (int)Math.Min(fileSize, MaxVideoScanBytes);
            byte[] data = new byte[readBytes];

            using (var fs = new FileStream(filePath, FileMode.Open,
                                           FileAccess.Read, FileShare.ReadWrite))
                fs.Read(data, 0, readBytes);

            GpsData? gps = null;

            // Strategy 1: ©xyz atom (QuickTime / iPhone MOV/MP4).
            byte[] markerXyz = { 0xA9, 0x78, 0x79, 0x7A }; // ©xyz
            int idxXyz = IndexOf(data, markerXyz);

            if (idxXyz >= 0)
            {
                int afterType = idxXyz + 4;
                if (afterType + 4 < data.Length)
                {
                    int strLen = (data[afterType] << 8) | data[afterType + 1];
                    int strStart = afterType + 4; // skip length(2) + language(2)

                    if (strLen > 0 && strLen < 256 && strStart + strLen <= data.Length)
                        gps = ParseIso6709(Encoding.UTF8.GetString(data, strStart, strLen).Trim('\0', ' '));

                    if (gps is null)
                    {
                        int fallbackLen = Math.Min(64, data.Length - strStart);
                        if (fallbackLen > 0)
                            gps = ParseIso6709(
                                Encoding.UTF8.GetString(data, strStart, fallbackLen).Trim('\0', ' '));
                    }
                }
            }

            // Strategy 2: 'loci' atom.
            if (gps is null)
            {
                int idxLoci = IndexOf(data, Encoding.ASCII.GetBytes("loci"));
                if (idxLoci >= 0)
                {
                    int start = idxLoci + 8;
                    int len = Math.Min(128, data.Length - start);
                    if (len > 0)
                        gps = ParseIso6709(
                            Encoding.UTF8.GetString(data, start, len).Trim('\0'));
                }
            }

            // Strategy 3: ISO 6709 pattern scan.
            if (gps is null)
            {
                string fullText = Encoding.Latin1.GetString(data);
                var m = Regex.Match(fullText,
                    @"([+-]\d{1,3}\.\d{4,}[+-]\d{1,3}\.\d{4,}[+-]?\d*\.?\d*/?)");
                if (m.Success) gps = ParseIso6709(m.Groups[1].Value);
            }

            if (gps is null) return null;

            // Date: ©day atom.
            string? date = null;
            byte[] markerDay = { 0xA9, 0x64, 0x61, 0x79 }; // ©day
            int idxDay = IndexOf(data, markerDay);

            if (idxDay >= 0)
            {
                int afterDay = idxDay + 4;
                int dayStrLen = afterDay + 2 < data.Length
                    ? (data[afterDay] << 8) | data[afterDay + 1]
                    : 0;
                int dayStart = afterDay + 4;
                int readLen = (dayStrLen > 0 && dayStrLen < 64) ? dayStrLen
                              : Math.Min(30, data.Length - dayStart);

                if (readLen > 0 && dayStart + readLen <= data.Length)
                    date = NormalizeVideoDate(
                        Encoding.UTF8.GetString(data, dayStart, readLen).Trim('\0', ' '));
            }

            // Fallback date: mvhd creation timestamp.
            if (date is null)
            {
                byte[] mvhd = Encoding.ASCII.GetBytes("mvhd");
                int idxMvhd = IndexOf(data, mvhd);
                if (idxMvhd >= 0)
                {
                    int pos = idxMvhd + 4;
                    if (pos < data.Length)
                    {
                        byte version = data[pos]; pos += 4;
                        if (pos + (version == 1 ? 8 : 4) <= data.Length)
                        {
                            long secs = version == 1
                                ? (long)ReadUInt64BE(data, pos)
                                : ReadUInt32BE(data, pos);

                            var epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            var dt = epoch.AddSeconds(secs);
                            if (dt.Year >= 2000 && dt.Year <= 2100)
                                date = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                        }
                    }
                }
            }

            return new GpsData(gps.Latitude, gps.Longitude, gps.Altitude,
                               gps.LatRef, gps.LonRef, date,
                               gps.CameraModel, gps.Software);
        }

        // ── Parsing helpers ───────────────────────────────────────────────

        private static GpsData? ParseIso6709(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();

            var m = Regex.Match(s,
                @"([+-]\d{1,3}(?:\.\d+)?)([+-]\d{1,3}(?:\.\d+)?)([+-]\d+(?:\.\d+)?)?");
            if (!m.Success) return null;

            if (!double.TryParse(m.Groups[1].Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double lat))
                return null;
            if (!double.TryParse(m.Groups[2].Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double lon))
                return null;

            if (Math.Abs(lat) > 90 || Math.Abs(lon) > 180) return null;
            if (lat == 0 && lon == 0) return null;

            double? alt = null;
            if (m.Groups[3].Success && double.TryParse(m.Groups[3].Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double a))
                alt = a;

            return new GpsData(lat, lon, alt,
                               lat >= 0 ? "N" : "S",
                               lon >= 0 ? "E" : "W",
                               null, null, null);
        }

        private static string? NormalizeVideoDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (DateTime.TryParse(raw,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out DateTime dt))
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            if (raw.Length >= 10 && raw[4] == '-') return raw[..10];
            return raw.Length > 0 ? raw : null;
        }

        // ── EXIF rational conversion ──────────────────────────────────────

        private static double RationalToDecimalDegrees(byte[] data)
        {
            if (data is null || data.Length < 24) return 0;
            double deg = RationalToDouble(data, 0);
            double min = RationalToDouble(data, 8);
            double sec = RationalToDouble(data, 16);
            return deg + min / 60.0 + sec / 3600.0;
        }

        private static double RationalToDouble(byte[] data, int offset)
        {
            if (data.Length < offset + 8) return 0;
            uint num = BitConverter.ToUInt32(data, offset);
            uint den = BitConverter.ToUInt32(data, offset + 4);
            return den == 0 ? 0 : (double)num / den;
        }

        // ── Extension checks ──────────────────────────────────────────────

        private static bool IsImageExtension(string ext) =>
            ext is ".jpg" or ".jpeg" or ".tiff" or ".tif"
                or ".png" or ".heic" or ".heif" or ".webp";

        private static bool IsVideoExtension(string ext) =>
            ext is ".mp4" or ".mov" or ".avi" or ".mkv"
                or ".wmv" or ".3gp" or ".m4v";

        // ── Binary search helpers ─────────────────────────────────────────

        private static int IndexOf(byte[] source, byte[] pattern)
        {
            int limit = source.Length - pattern.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                    if (source[i + j] != pattern[j]) { found = false; break; }
                if (found) return i;
            }
            return -1;
        }

        private static uint ReadUInt32BE(byte[] d, int o) =>
            (uint)(d[o] << 24 | d[o + 1] << 16 | d[o + 2] << 8 | d[o + 3]);

        private static ulong ReadUInt64BE(byte[] d, int o) =>
            ((ulong)ReadUInt32BE(d, o) << 32) | ReadUInt32BE(d, o + 4);
    }
}