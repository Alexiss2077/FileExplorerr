using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  GPS WRITER — escribe coordenadas GPS en metadatos EXIF de imágenes
    //  Soporta JPEG y TIFF.
    // ════════════════════════════════════════════════════════════════════════
    internal static class GpsWriter
    {
        // EXIF Property IDs para GPS
        private const int TagGpsVersionId = 0x0000;
        private const int TagGpsLatRef = 0x0001;
        private const int TagGpsLat = 0x0002;
        private const int TagGpsLonRef = 0x0003;
        private const int TagGpsLon = 0x0004;
        private const int TagGpsAltRef = 0x0005;
        private const int TagGpsAlt = 0x0006;

        /// <summary>
        /// Escribe coordenadas GPS en los metadatos EXIF de una imagen JPEG o TIFF.
        /// Sobreescribe el archivo original.
        /// </summary>
        public static void WriteGps(string filePath, double latitude, double longitude, double? altitude = null)
        {
            if (Math.Abs(latitude) > 90)
                throw new ArgumentOutOfRangeException(nameof(latitude), "Latitud debe estar entre -90 y 90.");
            if (Math.Abs(longitude) > 180)
                throw new ArgumentOutOfRangeException(nameof(longitude), "Longitud debe estar entre -180 y 180.");

            string ext = Path.GetExtension(filePath).ToLower();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".tiff" && ext != ".tif")
                throw new NotSupportedException("Solo se soporta escritura GPS en JPEG y TIFF.");

            // Leer la imagen completa en memoria para no bloquear el archivo
            byte[] fileBytes = File.ReadAllBytes(filePath);
            Image img;
            using (var ms = new MemoryStream(fileBytes))
                img = Image.FromStream(ms, true, true);

            try
            {
                // GPS Version ID: 2.3.0.0
                SetPropertyRational(img, TagGpsVersionId, new byte[] { 2, 3, 0, 0 }, PropertyTagTypeByte);

                // Latitude
                double absLat = Math.Abs(latitude);
                string latRef = latitude >= 0 ? "N" : "S";
                SetPropertyAscii(img, TagGpsLatRef, latRef);
                SetPropertyRational(img, TagGpsLat, DecimalDegreesToExifRational(absLat));

                // Longitude
                double absLon = Math.Abs(longitude);
                string lonRef = longitude >= 0 ? "E" : "W";
                SetPropertyAscii(img, TagGpsLonRef, lonRef);
                SetPropertyRational(img, TagGpsLon, DecimalDegreesToExifRational(absLon));

                // Altitude
                if (altitude.HasValue)
                {
                    double absAlt = Math.Abs(altitude.Value);
                    byte altRef = altitude.Value < 0 ? (byte)1 : (byte)0;
                    SetPropertyByte(img, TagGpsAltRef, altRef);
                    SetPropertyRational(img, TagGpsAlt, DoubleToExifRational(absAlt));
                }

                // Guardar: necesitamos un archivo temporal porque no se puede sobreescribir mientras está abierto
                string tempPath = filePath + ".tmp_gps";
                var codec = GetEncoder(ext);
                if (codec != null)
                {
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 98L);
                    img.Save(tempPath, codec, encoderParams);
                }
                else
                {
                    img.Save(tempPath);
                }

                img.Dispose();

                // Reemplazar original
                File.Delete(filePath);
                File.Move(tempPath, filePath);
            }
            catch
            {
                img.Dispose();
                // Limpiar archivo temporal si existe
                string tempPath = filePath + ".tmp_gps";
                if (File.Exists(tempPath))
                    try { File.Delete(tempPath); } catch { }
                throw;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS — Conversión a formato EXIF
        // ════════════════════════════════════════════════════════════════════

        private const short PropertyTagTypeAscii = 2;
        private const short PropertyTagTypeByte = 1;
        private const short PropertyTagTypeRational = 5;

        /// <summary>
        /// Convierte grados decimales a 3 racionales EXIF: (grados, minutos, segundos)
        /// Cada racional es un par numerador/denominador de 4 bytes (uint32) = 24 bytes total
        /// </summary>
        private static byte[] DecimalDegreesToExifRational(double decimalDegrees)
        {
            int degrees = (int)decimalDegrees;
            double minutesFull = (decimalDegrees - degrees) * 60.0;
            int minutes = (int)minutesFull;
            double seconds = (minutesFull - minutes) * 60.0;

            // Usar denominador de 10000 para segundos para mayor precisión
            uint secNumerator = (uint)Math.Round(seconds * 10000);
            uint secDenominator = 10000;

            byte[] data = new byte[24];
            BitConverter.GetBytes((uint)degrees).CopyTo(data, 0);
            BitConverter.GetBytes((uint)1).CopyTo(data, 4);
            BitConverter.GetBytes((uint)minutes).CopyTo(data, 8);
            BitConverter.GetBytes((uint)1).CopyTo(data, 12);
            BitConverter.GetBytes(secNumerator).CopyTo(data, 16);
            BitConverter.GetBytes(secDenominator).CopyTo(data, 20);

            return data;
        }

        /// <summary>
        /// Convierte un double a un racional EXIF (1 par numerador/denominador)
        /// </summary>
        private static byte[] DoubleToExifRational(double value)
        {
            uint numerator = (uint)Math.Round(value * 1000);
            uint denominator = 1000;

            byte[] data = new byte[8];
            BitConverter.GetBytes(numerator).CopyTo(data, 0);
            BitConverter.GetBytes(denominator).CopyTo(data, 4);

            return data;
        }

        private static void SetPropertyRational(Image img, int id, byte[] data, short type = PropertyTagTypeRational)
        {
            PropertyItem prop = CreatePropertyItem();
            prop.Id = id;
            prop.Type = type;
            prop.Len = data.Length;
            prop.Value = data;
            img.SetPropertyItem(prop);
        }

        private static void SetPropertyAscii(Image img, int id, string value)
        {
            byte[] data = new byte[value.Length + 1];
            for (int i = 0; i < value.Length; i++)
                data[i] = (byte)value[i];
            data[value.Length] = 0; // null terminator

            PropertyItem prop = CreatePropertyItem();
            prop.Id = id;
            prop.Type = PropertyTagTypeAscii;
            prop.Len = data.Length;
            prop.Value = data;
            img.SetPropertyItem(prop);
        }

        private static void SetPropertyByte(Image img, int id, byte value)
        {
            PropertyItem prop = CreatePropertyItem();
            prop.Id = id;
            prop.Type = PropertyTagTypeByte;
            prop.Len = 1;
            prop.Value = new byte[] { value };
            img.SetPropertyItem(prop);
        }

        /// <summary>
        /// PropertyItem no tiene constructor público.
        /// Usamos reflexión para crear una instancia.
        /// </summary>
        private static PropertyItem CreatePropertyItem()
        {
            var type = typeof(PropertyItem);
            var ctor = type.GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                null, Type.EmptyTypes, null);

            if (ctor != null)
                return (PropertyItem)ctor.Invoke(null);

            // Fallback: usar FormatterServices (funciona en .NET Framework y .NET Core)
            return (PropertyItem)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
        }

        private static ImageCodecInfo? GetEncoder(string ext)
        {
            string mimeType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".tiff" or ".tif" => "image/tiff",
                _ => ""
            };

            if (string.IsNullOrEmpty(mimeType)) return null;

            foreach (var codec in ImageCodecInfo.GetImageEncoders())
                if (codec.MimeType == mimeType) return codec;

            return null;
        }
    }
}