using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  GPS WRITER — writes GPS coordinates into EXIF metadata of JPEG/TIFF files.
    //  Supports JPEG and TIFF only (System.Drawing limitation).
    // ════════════════════════════════════════════════════════════════════════
    internal static class GpsWriter
    {
        // ── EXIF Property IDs ────────────────────────────────────────────────
        private const int TagGpsVersionId = 0x0000;
        private const int TagGpsLatRef = 0x0001;
        private const int TagGpsLat = 0x0002;
        private const int TagGpsLonRef = 0x0003;
        private const int TagGpsLon = 0x0004;
        private const int TagGpsAltRef = 0x0005;
        private const int TagGpsAlt = 0x0006;

        // ── EXIF type codes ──────────────────────────────────────────────────
        private const short TypeByte = 1;
        private const short TypeAscii = 2;
        private const short TypeRational = 5;

        // ── Precision denominator for seconds ────────────────────────────────
        private const uint SecondsDenominator = 10_000;

        // ── Supported extensions ─────────────────────────────────────────────
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".tiff", ".tif" };

        /// <summary>
        /// Writes GPS coordinates into the EXIF metadata of a JPEG or TIFF file.
        /// The original file is overwritten atomically via a temporary file.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Latitude or longitude out of valid range.</exception>
        /// <exception cref="NotSupportedException">File format is not JPEG or TIFF.</exception>
        public static void WriteGps(string filePath, double latitude, double longitude,
                                    double? altitude = null)
        {
            ValidateCoordinates(latitude, longitude);
            ValidateExtension(filePath);

            // Load the whole file into memory so we can safely overwrite it.
            byte[] fileBytes = File.ReadAllBytes(filePath);

            Image image;
            using (var ms = new System.IO.MemoryStream(fileBytes))
                image = Image.FromStream(ms, useEmbeddedColorManagement: true, validateImageData: true);

            string? tempPath = null;
            try
            {
                SetGpsVersion(image);
                SetLatitude(image, latitude);
                SetLongitude(image, longitude);

                if (altitude.HasValue)
                    SetAltitude(image, altitude.Value);

                tempPath = filePath + ".tmp_gps";
                SaveImage(image, tempPath, Path.GetExtension(filePath));
                image.Dispose();

                // Atomic replace: delete original then rename temp.
                File.Delete(filePath);
                File.Move(tempPath, filePath);
            }
            catch
            {
                image.Dispose();
                CleanupTemp(tempPath);
                throw;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRIVATE — tag writers
        // ════════════════════════════════════════════════════════════════════

        private static void SetGpsVersion(Image image)
        {
            // GPS IFD version 2.3.0.0
            SetRational(image, TagGpsVersionId, new byte[] { 2, 3, 0, 0 }, TypeByte);
        }

        private static void SetLatitude(Image image, double latitude)
        {
            SetAscii(image, TagGpsLatRef, latitude >= 0 ? "N" : "S");
            SetRational(image, TagGpsLat, DegreesToExif(Math.Abs(latitude)));
        }

        private static void SetLongitude(Image image, double longitude)
        {
            SetAscii(image, TagGpsLonRef, longitude >= 0 ? "E" : "W");
            SetRational(image, TagGpsLon, DegreesToExif(Math.Abs(longitude)));
        }

        private static void SetAltitude(Image image, double altitude)
        {
            SetByte(image, TagGpsAltRef, altitude < 0 ? (byte)1 : (byte)0);
            SetRational(image, TagGpsAlt, DoubleToExifRational(Math.Abs(altitude)));
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRIVATE — EXIF encoding helpers
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Converts decimal degrees to three EXIF rationals (deg, min, sec).
        /// Each rational is a pair of uint32 values = 24 bytes total.
        /// </summary>
        private static byte[] DegreesToExif(double decimalDegrees)
        {
            int degrees = (int)decimalDegrees;
            double minFull = (decimalDegrees - degrees) * 60.0;
            int minutes = (int)minFull;
            double seconds = (minFull - minutes) * 60.0;

            uint secNumerator = (uint)Math.Round(seconds * SecondsDenominator);

            var data = new byte[24];
            BitConverter.GetBytes((uint)degrees).CopyTo(data, 0);
            BitConverter.GetBytes((uint)1).CopyTo(data, 4);
            BitConverter.GetBytes((uint)minutes).CopyTo(data, 8);
            BitConverter.GetBytes((uint)1).CopyTo(data, 12);
            BitConverter.GetBytes(secNumerator).CopyTo(data, 16);
            BitConverter.GetBytes(SecondsDenominator).CopyTo(data, 20);
            return data;
        }

        private static byte[] DoubleToExifRational(double value)
        {
            const uint denominator = 1000;
            uint numerator = (uint)Math.Round(value * denominator);
            var data = new byte[8];
            BitConverter.GetBytes(numerator).CopyTo(data, 0);
            BitConverter.GetBytes(denominator).CopyTo(data, 4);
            return data;
        }

        private static void SetRational(Image image, int id, byte[] data, short type = TypeRational)
        {
            var prop = CreatePropertyItem();
            prop.Id = id;
            prop.Type = type;
            prop.Len = data.Length;
            prop.Value = data;
            image.SetPropertyItem(prop);
        }

        private static void SetAscii(Image image, int id, string value)
        {
            var data = new byte[value.Length + 1]; // +1 for null terminator
            for (int i = 0; i < value.Length; i++)
                data[i] = (byte)value[i];
            data[value.Length] = 0;

            var prop = CreatePropertyItem();
            prop.Id = id;
            prop.Type = TypeAscii;
            prop.Len = data.Length;
            prop.Value = data;
            image.SetPropertyItem(prop);
        }

        private static void SetByte(Image image, int id, byte value)
        {
            var prop = CreatePropertyItem();
            prop.Id = id;
            prop.Type = TypeByte;
            prop.Len = 1;
            prop.Value = new[] { value };
            image.SetPropertyItem(prop);
        }

        /// <summary>
        /// Creates a blank <see cref="PropertyItem"/>.
        /// <para>
        /// PropertyItem has no public constructor. In .NET 5+ the recommended
        /// approach is <see cref="RuntimeHelpers.GetUninitializedObject"/> which
        /// supersedes the obsolete <c>FormatterServices.GetUninitializedObject</c>.
        /// </para>
        /// </summary>
        private static PropertyItem CreatePropertyItem()
        {
            // RuntimeHelpers.GetUninitializedObject is the .NET 5+ replacement for
            // the obsolete FormatterServices.GetUninitializedObject.
            return (PropertyItem)RuntimeHelpers.GetUninitializedObject(typeof(PropertyItem));
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRIVATE — I/O helpers
        // ════════════════════════════════════════════════════════════════════

        private static void SaveImage(Image image, string path, string extension)
        {
            var codec = GetEncoder(extension.ToLowerInvariant());
            if (codec is not null)
            {
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 98L);
                image.Save(path, codec, encoderParams);
            }
            else
            {
                image.Save(path);
            }
        }

        private static ImageCodecInfo? GetEncoder(string ext)
        {
            string mimeType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".tiff" or ".tif" => "image/tiff",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(mimeType)) return null;

            foreach (var codec in ImageCodecInfo.GetImageEncoders())
                if (codec.MimeType == mimeType) return codec;

            return null;
        }

        private static void CleanupTemp(string? path)
        {
            if (path is null) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* Non-fatal — temp file may be cleaned up on next run. */ }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRIVATE — validation
        // ════════════════════════════════════════════════════════════════════

        private static void ValidateCoordinates(double latitude, double longitude)
        {
            if (Math.Abs(latitude) > 90)
                throw new ArgumentOutOfRangeException(nameof(latitude),
                    "Latitud debe estar entre -90 y 90.");

            if (Math.Abs(longitude) > 180)
                throw new ArgumentOutOfRangeException(nameof(longitude),
                    "Longitud debe estar entre -180 y 180.");
        }

        private static void ValidateExtension(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            bool supported = Array.IndexOf(SupportedExtensions, ext) >= 0;
            if (!supported)
                throw new NotSupportedException(
                    "Solo se soporta escritura GPS en archivos JPEG y TIFF.");
        }
    }
}