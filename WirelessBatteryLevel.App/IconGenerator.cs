using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WirelessBatteryLevel.App
{
    public static class IconGenerator
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static Icon? _cachedIcon;
        private static ImageSource? _cachedWpfIcon;

        public static string EnsureIconCreated()
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");

            try
            {
                using var iconStream = GenerateBatteryIcon();
                using var fileStream = new FileStream(iconPath, FileMode.Create, FileAccess.Write);
                iconStream.CopyTo(fileStream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconGenerator] Exception while creating icon: {ex.Message}");
            }

            return iconPath;
        }

        public static Icon CreateZtkIconInstance()
        {
            if (_cachedIcon != null)
                return _cachedIcon;

            try
            {
                using var ms = GenerateBatteryIcon();
                _cachedIcon = new Icon(ms);
                return _cachedIcon;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        public static ImageSource GetWpfIconSource()
        {
            if (_cachedWpfIcon != null)
                return _cachedWpfIcon;

            var icon = CreateZtkIconInstance();
            try
            {
                _cachedWpfIcon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                return _cachedWpfIcon;
            }
            catch
            {
                return new BitmapImage();
            }
        }

        private static MemoryStream GenerateBatteryIcon()
        {
            int size = 256;
            using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None; // SHARP CRISP VECTOR CORNERS
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.Transparent);

                using var whiteBrush = new SolidBrush(System.Drawing.Color.White);
                using var whitePen = new System.Drawing.Pen(System.Drawing.Color.White, 14)
                {
                    LineJoin = LineJoin.Miter, // SHARP 90-DEGREE CORNERS
                    MiterLimit = 10
                };

                // Sleek & Tall Vertical Battery Layout (Monochrome Crisp White Concept)
                // 1. Top Battery Terminal Stud (Solid White Rectangle Centered on Top)
                int studWidth = 48;
                int studHeight = 16;
                int studX = (size - studWidth) / 2; // 104
                int studY = 10;
                g.FillRectangle(whiteBrush, studX, studY, studWidth, studHeight);

                // 2. Sharp Taller Outer Body Outline (14px White Stroke, 0-Radius Corners)
                int bodyWidth = 124;
                int bodyHeight = 216;
                int bodyX = (size - bodyWidth) / 2; // 66
                int bodyY = 26;
                g.DrawRectangle(whitePen, bodyX + 7, bodyY + 7, bodyWidth - 14, bodyHeight - 14);

                // 3. Sharp Inner Battery Fill Level (75% Vertical Fill - Solid White Rectangle from Bottom Up)
                int innerX = bodyX + 20;
                int innerY = bodyY + 20;
                int innerWidth = bodyWidth - 40;
                int innerHeight = bodyHeight - 40;

                int fillH = (int)(innerHeight * 0.75); // 75% Fill Level
                int fillY = innerY + (innerHeight - fillH); // Rises from bottom

                g.FillRectangle(whiteBrush, innerX, fillY, innerWidth, fillH);
            }

            var ms = new MemoryStream();
            var hIcon = bmp.GetHicon();
            try
            {
                using (var icon = Icon.FromHandle(hIcon))
                {
                    icon.Save(ms);
                }
            }
            finally
            {
                DestroyIcon(hIcon);
            }
            ms.Position = 0;
            return ms;
        }
    }
}
