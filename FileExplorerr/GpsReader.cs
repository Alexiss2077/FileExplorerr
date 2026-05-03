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
                // Leer solo los primeros 50 MB para no cargar todo el archivo en RAM
                long fileSize = new FileInfo(filePath).Length;
                int readBytes = (int)Math.Min(fileSize, 50 * 1024 * 1024);
                byte[] data = new byte[readBytes];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    fs.Read(data, 0, readBytes);

                // ── ESTRATEGIA 1: átomo ©xyz de QuickTime (iPhone MOV/MP4) ────
                // IMPORTANTE: © en QuickTime es 0xA9 (latin-1 1 byte),
                // NO 0xC2 0xA9 (UTF-8 2 bytes). Ese era el bug.
                byte[] markerXyz = { 0xA9, 0x78, 0x79, 0x7A }; // ©xyz latin-1

                int idx = IndexOf(data, markerXyz);
                if (idx >= 0)
                {
                    // Estructura del átomo ©xyz dentro de udta:
                    //   [4 bytes: tamaño big-endian] [4 bytes: "©xyz"]
                    //   [2 bytes: longitud string]   [2 bytes: código idioma]
                    //   [N bytes: string GPS ISO 6709]
                    int afterType = idx + 4; // justo después de "©xyz"
                    if (afterType + 4 < data.Length)
                    {
                        int strLen = (data[afterType] << 8) | data[afterType + 1];
                        int strStart = afterType + 4; // saltar longitud(2) + idioma(2)

                        if (strLen > 0 && strLen < 256 && strStart + strLen <= data.Length)
                        {
                            string raw = Encoding.UTF8.GetString(data, strStart, strLen).Trim('\0', ' ');
                            var result = ParseIso6709(raw);
                            if (result != null) return result;
                        }

                        // Fallback: leer directamente sin respetar strLen (algunos encoders lo omiten)
                        int fallbackLen = Math.Min(64, data.Length - strStart);
                        if (fallbackLen > 0)
                        {
                            string raw = Encoding.UTF8.GetString(data, strStart, fallbackLen).Trim('\0', ' ');
                            var result = ParseIso6709(raw);
                            if (result != null) return result;
                        }
                    }
                }

                // ── ESTRATEGIA 2: átomo 'loci' (formato alternativo) ──────────
                byte[] markerLoci = Encoding.ASCII.GetBytes("loci");
                idx = IndexOf(data, markerLoci);
                if (idx >= 0)
                {
                    int start = idx + 4 + 4; // saltar nombre(4) + flags(4)
                    int len = Math.Min(128, data.Length - start);
                    if (len > 0)
                    {
                        string raw = Encoding.UTF8.GetString(data, start, len).Trim('\0');
                        var result = ParseIso6709(raw);
                        if (result != null) return result;
                    }
                }

                // ── ESTRATEGIA 3: buscar patrón ISO 6709 directamente en texto ─
                // Algunos iPhones y GoPros escriben la coord como texto plano
                // Ej: "+27.0579-101.5436+01234.567/"
                string fullText = Encoding.Latin1.GetString(data);
                var directMatch = System.Text.RegularExpressions.Regex.Match(
                    fullText,
                    @"([+-]\d{1,3}\.\d{4,}[+-]\d{1,3}\.\d{4,}[+-]?\d*\.?\d*/?)",
                    System.Text.RegularExpressions.RegexOptions.None);
                if (directMatch.Success)
                    return ParseIso6709(directMatch.Groups[1].Value);

                return null;
            }
            catch { return null; }
        }

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