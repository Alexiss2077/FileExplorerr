using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  FILE ICON FACTORY
    //  Produces the six 32x32 programmatic icons used in the main ListView,
    //  maps file extensions to ImageList keys, and builds the status-bar text.
    //
    //  Phase 5A: extracted from Form1.cs.
    //  Original methods removed from Form1:
    //    MakeFolderIcon, MakeFileIcon, MakeImageIcon, MakeAudioIcon,
    //    MakeVideoIcon, MakeTextIcon, IconKey, BuildStatusText.
    //
    //  All members are static and stateless (thread-safe).
    // ════════════════════════════════════════════════════════════════════════
    internal static class FileIconFactory
    {
        // ── ImageList key constants ───────────────────────────────────────
        // Must match the string keys in imageList.Images.Add() in Form1.BuildUI.
        public const string KeyFolder = "folder";
        public const string KeyFile = "file";
        public const string KeyImage = "image";
        public const string KeyAudio = "audio";
        public const string KeyVideo = "video";
        public const string KeyText = "text";
        public const string KeyArchive = "archive";

        // ════════════════════════════════════════════════════════════════════
        //  ICON CREATION
        //  Pixel-exact copies of the original private static methods on Form1.
        // ════════════════════════════════════════════════════════════════════

        public static System.Drawing.Icon MakeFolderIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(Color.FromArgb(251, 191, 36));
            g.FillRectangle(b, 4, 12, 24, 16);
            g.FillPolygon(b, new[] { new Point(4, 12), new Point(10, 8), new Point(16, 12) });
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        public static System.Drawing.Icon MakeFileIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var body = new SolidBrush(Color.FromArgb(90, 96, 128));
            using var fold = new SolidBrush(Color.FromArgb(167, 139, 250));
            g.FillRectangle(body, 8, 4, 16, 24);
            g.FillPolygon(fold, new[] { new Point(24, 4), new Point(24, 10), new Point(18, 4) });
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        public static System.Drawing.Icon MakeImageIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(12, 44, 78));
            using var sun = new SolidBrush(Color.FromArgb(251, 191, 36));
            using var mnt = new SolidBrush(Color.FromArgb(96, 165, 250));
            g.FillRectangle(bg, 6, 6, 20, 20);
            g.FillEllipse(sun, 10, 9, 6, 6);
            g.FillPolygon(mnt, new[]
                { new Point(6, 26), new Point(12, 17), new Point(18, 22), new Point(26, 26) });
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        public static System.Drawing.Icon MakeAudioIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(Color.FromArgb(52, 211, 153));
            g.FillEllipse(b, 8, 18, 8, 8);
            g.FillRectangle(b, 14, 8, 2, 14);
            g.FillEllipse(b, 16, 8, 6, 6);
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        public static System.Drawing.Icon MakeVideoIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(72, 24, 60));
            using var play = new SolidBrush(Color.FromArgb(244, 114, 182));
            g.FillRectangle(bg, 6, 10, 14, 12);
            g.FillPolygon(play, new[]
                { new Point(20, 13), new Point(26, 16), new Point(20, 19) });
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        public static System.Drawing.Icon MakeTextIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var page = new SolidBrush(Color.FromArgb(28, 32, 48));
            g.FillRectangle(page, 8, 4, 16, 24);
            using var pen = new Pen(Color.FromArgb(124, 111, 247), 2);
            g.DrawLine(pen, 11, 10, 21, 10);
            g.DrawLine(pen, 11, 14, 21, 14);
            g.DrawLine(pen, 11, 18, 21, 18);
            g.DrawLine(pen, 11, 22, 18, 22);
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        // ════════════════════════════════════════════════════════════════════
        //  ICON KEY RESOLUTION
        //  Originally: private string IconKey(string ext) on Form1.
        //  Now uses FileExtensions.Categorise() — the single source of truth
        //  for extension categorisation introduced in Phase 1/2.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the ImageList key for a given file extension.
        /// Call-sites in Form1 replace <c>IconKey(ext)</c> with
        /// <c>FileIconFactory.IconKey(ext)</c>.
        /// </summary>
        public static string IconKey(string extension)
        {
            return FileExtensions.Categorise(extension) switch
            {
                FileCategory.Image => KeyImage,
                FileCategory.Audio => KeyAudio,
                FileCategory.Video => KeyVideo,
                FileCategory.Text => KeyText,
                FileCategory.Archive => KeyArchive,
                _ => KeyFile
            };
        }

        public static System.Drawing.Icon MakeArchiveIcon()

        {
             using var bmp = new Bitmap(32, 32);
             using var g = Graphics.FromImage(bmp);
             g.SmoothingMode = SmoothingMode.AntiAlias;
             g.Clear(Color.Transparent);
             // Box body
             using var body = new SolidBrush(Color.FromArgb(90, 96, 128));
             g.FillRectangle(body, 6, 14, 20, 14);
             // Box lid
             using var lid = new SolidBrush(Color.FromArgb(124, 111, 247));
             g.FillRectangle(lid, 4, 10, 24, 6);
             // Zipper stripe
             using var zip = new SolidBrush(Color.FromArgb(240, 240, 255));
             g.FillRectangle(zip, 14, 10, 4, 18);
              return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        // ════════════════════════════════════════════════════════════════════
        //  STATUS BAR TEXT
        //  Originally: private static string BuildStatusText(FileStats s, int folders)
        //  on Form1.  Emoji strings are identical to the original.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the complete status-bar string shown at the bottom of Form1,
        /// e.g. "3 carpetas  x  12 archivos  x  8 imagenes  x  4 audios".
        /// Returns "Carpeta vacia" when counts are all zero.
        /// Call-sites in Form1 replace <c>BuildStatusText(stats, dirs.Count)</c>
        /// with <c>FileIconFactory.BuildStatusText(stats, dirs.Count)</c>.
        /// </summary>
        public static string BuildStatusText(FileStats stats, int folderCount)
        {
            var parts = new List<string>();

            if (folderCount > 0)
                parts.Add($"\U0001F4C1 {folderCount} carpeta{(folderCount != 1 ? "s" : "")}");

            if (stats.Total > 0)
                parts.Add($"\U0001F4C4 {stats.Total} archivo{(stats.Total != 1 ? "s" : "")}");

            if (stats.Images > 0)
                parts.Add($"\U0001F5BC\uFE0F {stats.Images} im\u00E1g{(stats.Images != 1 ? "enes" : "en")}");

            if (stats.Audio > 0)
                parts.Add($"\U0001F3B5 {stats.Audio} audio{(stats.Audio != 1 ? "s" : "")}");

            if (stats.Video > 0)
                parts.Add($"\U0001F3AC {stats.Video} video{(stats.Video != 1 ? "s" : "")}");

            if (stats.Text > 0)
                parts.Add($"\U0001F4DD {stats.Text} texto{(stats.Text != 1 ? "s" : "")}");

            if (stats.Other > 0)
                parts.Add($"\U0001F4E6 {stats.Other} otro{(stats.Other != 1 ? "s" : "")}");

            return parts.Count > 0
                ? string.Join("  \u00B7  ", parts)
                : "Carpeta vac\u00EDa";
        }
    }
}