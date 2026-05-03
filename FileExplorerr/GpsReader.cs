using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  GPS READER — extrae coordenadas GPS de imágenes (EXIF) y vídeos (Shell)
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
        private const int TagGpsSpeed = 0x000D;
        private const int TagGpsImgDir = 0x0011;
        private const int TagGpsDate = 0x001D;
        private const int TagMake = 0x010F;
        private const int TagModel = 0x0110;
        private const int TagDateOrig = 0x9003;
        private const int TagSoftware = 0x0131;

        // ── Resultado ────────────────────────────────────────────────────────
        public record GpsData(
            double Latitude,
            double Longitude,
            double? Altitude,
            string LatRef,
            string LonRef,
            string? Date,
            string? CameraModel,
            string? Software
        )
        {
            public bool HasGps => Latitude != 0 || Longitude != 0;

            public string LatString => FormatDMS(Math.Abs(Latitude), LatRef == "S" ? "S" : "N");
            public string LonString => FormatDMS(Math.Abs(Longitude), LonRef == "W" ? "W" : "E");

            private static string FormatDMS(double dd, string dir)
            {
                int deg = (int)dd;
                double minFull = (dd - deg) * 60;
                int min = (int)minFull;
                double sec = (minFull - min) * 60;
                return $"{deg}° {min}' {sec:0.00}\" {dir}";
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PUNTO DE ENTRADA
        // ════════════════════════════════════════════════════════════════════
        public static GpsData? Read(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();

            // Imágenes: leer EXIF directamente
            if (IsImage(ext))
                return ReadFromImage(filePath);

            // Video: intentar leer metadatos vía Windows Shell
            if (IsVideo(ext))
                return ReadFromVideo(filePath);

            return null;
        }

        private static bool IsImage(string ext) =>
            ext is ".jpg" or ".jpeg" or ".tiff" or ".tif" or ".png" or ".heic" or ".heif" or ".webp";

        private static bool IsVideo(string ext) =>
            ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" or ".3gp" or ".m4v";

        // ════════════════════════════════════════════════════════════════════
        //  IMAGEN — EXIF via System.Drawing
        // ════════════════════════════════════════════════════════════════════
        private static GpsData? ReadFromImage(string filePath)
        {
            try
            {
                // Abrir en modo solo lectura para no bloquear el archivo
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var img = Image.FromStream(fs, false, false);

                var props = img.PropertyItems;
                if (props == null || props.Length == 0) return null;

                PropertyItem? GetProp(int id)
                {
                    foreach (var p in props)
                        if (p.Id == id) return p;
                    return null;
                }

                // GPS Lat / Lon
                var latRef = GetProp(TagGpsLatRef);
                var latProp = GetProp(TagGpsLat);
                var lonRef = GetProp(TagGpsLonRef);
                var lonProp = GetProp(TagGpsLon);

                if (latProp == null || lonProp == null) return null;

                double lat = RationalToDecimalDeg(latProp.Value!);
                double lon = RationalToDecimalDeg(lonProp.Value!);

                if (lat == 0 && lon == 0) return null;

                string lref = latRef?.Value != null ? Encoding.ASCII.GetString(latRef.Value).Trim('\0') : "N";
                string loref = lonRef?.Value != null ? Encoding.ASCII.GetString(lonRef.Value).Trim('\0') : "E";

                if (lref == "S") lat = -lat;
                if (loref == "W") lon = -lon;

                // Altitud
                double? alt = null;
                var altProp = GetProp(TagGpsAlt);
                if (altProp?.Value != null)
                {
                    double av = RationalToDouble(altProp.Value, 0);
                    var altRef = GetProp(TagGpsAltRef);
                    if (altRef?.Value != null && altRef.Value[0] == 1) av = -av;
                    alt = av;
                }

                // Fecha
                string? date = null;
                var dateProp = GetProp(TagGpsDate) ?? GetProp(TagDateOrig);
                if (dateProp?.Value != null)
                    date = Encoding.ASCII.GetString(dateProp.Value).Trim('\0').Replace(':', '/');

                // Cámara
                string? camera = null;
                var makeProp = GetProp(TagMake);
                var modelProp = GetProp(TagModel);
                string? make = makeProp?.Value != null ? Encoding.ASCII.GetString(makeProp.Value).Trim('\0', ' ') : null;
                string? model = modelProp?.Value != null ? Encoding.ASCII.GetString(modelProp.Value).Trim('\0', ' ') : null;
                if (make != null || model != null)
                    camera = string.Join(" ", new[] { make, model }.Where(x => !string.IsNullOrWhiteSpace(x)));

                // Software
                string? software = null;
                var swProp = GetProp(TagSoftware);
                if (swProp?.Value != null)
                    software = Encoding.ASCII.GetString(swProp.Value).Trim('\0', ' ');

                return new GpsData(lat, lon, alt, lref, loref, date, camera, software);
            }
            catch
            {
                return null;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  VIDEO — Windows Shell IShellFolder2 / propsys
        // ════════════════════════════════════════════════════════════════════
        private static GpsData? ReadFromVideo(string filePath)
        {
            // Los videos raramente tienen GPS en su metadata accesible sin ffprobe.
            // Intentamos via Shell32 / Windows property system (funciona en MP4/MOV con GPS track).
            try
            {
                // Usamos ShellFile via COM si está disponible
                return ReadVideoViaShell(filePath);
            }
            catch
            {
                return null;
            }
        }

        [DllImport("propsys.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int PSGetPropertyKeyFromName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
            out PROPERTYKEY propertyKey);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        private static GpsData? ReadVideoViaShell(string filePath)
        {
            // Para videos usamos WMPLib o Shell extended properties.
            // En .NET 8 puro lo más portable es leer el archivo MP4 buscando el átomo 'moov/udta/©xyz'
            // que contiene las coordenadas GPS en texto plano en iPhone/Android.
            return ReadMp4GpsAtom(filePath);
        }

        // ── Leer átomo GPS de MP4/MOV (iPhone, GoPro, Android) ──────────────
        private static GpsData? ReadMp4GpsAtom(string filePath)
        {
            try
            {
                long fileSize = new FileInfo(filePath).Length;
                int readBytes = (int)Math.Min(fileSize, 50 * 1024 * 1024);
                byte[] data = new byte[readBytes];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    fs.Read(data, 0, readBytes);

                GpsData? gps = null;

                // ── ESTRATEGIA 1: átomo ©xyz de QuickTime (iPhone MOV/MP4) ────
                // © en QuickTime es 0xA9 (latin-1), NO 0xC2 0xA9 (UTF-8)
                byte[] markerXyz = { 0xA9, 0x78, 0x79, 0x7A }; // ©xyz
                int idx = IndexOf(data, markerXyz);
                if (idx >= 0)
                {
                    int afterType = idx + 4;
                    if (afterType + 4 < data.Length)
                    {
                        int strLen = (data[afterType] << 8) | data[afterType + 1];
                        int strStart = afterType + 4; // saltar longitud(2) + idioma(2)

                        if (strLen > 0 && strLen < 256 && strStart + strLen <= data.Length)
                            gps = ParseIso6709(Encoding.UTF8.GetString(data, strStart, strLen).Trim('\0', ' '));

                        if (gps == null)
                        {
                            int fallbackLen = Math.Min(64, data.Length - strStart);
                            if (fallbackLen > 0)
                                gps = ParseIso6709(Encoding.UTF8.GetString(data, strStart, fallbackLen).Trim('\0', ' '));
                        }
                    }
                }

                // ── ESTRATEGIA 2: átomo 'loci' ────────────────────────────────
                if (gps == null)
                {
                    int idxLoci = IndexOf(data, Encoding.ASCII.GetBytes("loci"));
                    if (idxLoci >= 0)
                    {
                        int start = idxLoci + 8;
                        int len = Math.Min(128, data.Length - start);
                        if (len > 0)
                            gps = ParseIso6709(Encoding.UTF8.GetString(data, start, len).Trim('\0'));
                    }
                }

                // ── ESTRATEGIA 3: patrón ISO 6709 directo en bytes ───────────
                if (gps == null)
                {
                    string fullText = Encoding.Latin1.GetString(data);
                    var m = System.Text.RegularExpressions.Regex.Match(fullText,
                        @"([+-]\d{1,3}\.\d{4,}[+-]\d{1,3}\.\d{4,}[+-]?\d*\.?\d*/?)");
                    if (m.Success) gps = ParseIso6709(m.Groups[1].Value);
                }

                if (gps == null) return null;

                // ── FECHA: átomo ©day (0xA9 64 61 79) ────────────────────────
                // iPhone almacena la fecha de grabación aquí: "2024-03-15T10:22:01+0600"
                string? date = null;
                byte[] markerDay = { 0xA9, 0x64, 0x61, 0x79 }; // ©day
                int idxDay = IndexOf(data, markerDay);
                if (idxDay >= 0)
                {
                    int afterDay = idxDay + 4;
                    int dayStrLen = afterDay + 2 < data.Length ? (data[afterDay] << 8) | data[afterDay + 1] : 0;
                    int dayStart = afterDay + 4; // saltar longitud(2) + idioma(2)

                    int readLen = (dayStrLen > 0 && dayStrLen < 64) ? dayStrLen
                                                                     : Math.Min(30, data.Length - dayStart);
                    if (readLen > 0 && dayStart + readLen <= data.Length)
                    {
                        string raw = Encoding.UTF8.GetString(data, dayStart, readLen).Trim('\0', ' ');
                        date = NormalizeVideoDate(raw);
                    }
                }

                // ── FECHA fallback: mvhd creation time (segundos desde 1904) ──
                if (date == null)
                {
                    byte[] mvhd = Encoding.ASCII.GetBytes("mvhd");
                    int idxMvhd = IndexOf(data, mvhd);
                    if (idxMvhd >= 0)
                    {
                        int pos = idxMvhd + 4; // justo después de "mvhd"
                        // version byte: 0 = 32-bit timestamps, 1 = 64-bit
                        if (pos < data.Length)
                        {
                            byte version = data[pos]; pos += 4; // saltar version(1)+flags(3)
                            if (pos + (version == 1 ? 8 : 4) <= data.Length)
                            {
                                long secs = version == 1
                                    ? (long)ReadUInt64BE(data, pos)
                                    : ReadUInt32BE(data, pos);
                                // QuickTime epoch: 1 enero 1904
                                var epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                                var dt = epoch.AddSeconds(secs);
                                if (dt.Year >= 2000 && dt.Year <= 2100)
                                    date = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                            }
                        }
                    }
                }

                // Devolver GPS con fecha incorporada
                return new GpsData(gps.Latitude, gps.Longitude, gps.Altitude,
                    gps.LatRef, gps.LonRef, date, gps.CameraModel, gps.Software);
            }
            catch { return null; }
        }

        // Normaliza fechas de iPhone: "2024-03-15T10:22:01+0600" → "2024-03-15 10:22"
        private static string? NormalizeVideoDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            // Intentar parsear ISO 8601
            if (DateTime.TryParse(raw,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt))
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            // Fallback: devolver los primeros 10 chars si tiene pinta de fecha
            if (raw.Length >= 10 && raw[4] == '-') return raw.Substring(0, 10);
            return raw.Length > 0 ? raw : null;
        }

        private static uint ReadUInt32BE(byte[] d, int o) =>
            (uint)(d[o] << 24 | d[o + 1] << 16 | d[o + 2] << 8 | d[o + 3]);

        private static ulong ReadUInt64BE(byte[] d, int o) =>
            ((ulong)ReadUInt32BE(d, o) << 32) | ReadUInt32BE(d, o + 4);

        private static GpsData? ParseIso6709(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();

            // Formato ISO 6709: +DD.DDDD+DDD.DDDD+ALT/ o -DD.DDDD+DDD.DDDD/
            // iPhone escribe algo como: "+27.057918-101.543602+1234.00/"
            var match = System.Text.RegularExpressions.Regex.Match(s,
                @"([+-]\d{1,3}(?:\.\d+)?)([+-]\d{1,3}(?:\.\d+)?)([+-]\d+(?:\.\d+)?)?");
            if (!match.Success) return null;

            if (!double.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double lat)) return null;
            if (!double.TryParse(match.Groups[2].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double lon)) return null;

            // Validar rangos
            if (Math.Abs(lat) > 90 || Math.Abs(lon) > 180) return null;
            if (lat == 0 && lon == 0) return null;

            double? alt = null;
            if (match.Groups[3].Success && double.TryParse(match.Groups[3].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double a))
                alt = a;

            return new GpsData(lat, lon, alt, lat >= 0 ? "N" : "S", lon >= 0 ? "E" : "W", null, null, null);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS EXIF
        // ════════════════════════════════════════════════════════════════════

        // Convierte 3 racionales (deg, min, sec) a decimal
        private static double RationalToDecimalDeg(byte[] data)
        {
            if (data == null || data.Length < 24) return 0;
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
    }
}