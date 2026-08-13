using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace WirelessBatteryLevel.App
{
    public static class IconGenerator
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static Icon? _cachedIcon;

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

        private static MemoryStream GenerateBatteryIcon()
        {
            int size = 128;
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Top Battery Nipple / Cap
                int capWidth = 36;
                int capHeight = 14;
                int capX = (size - capWidth) / 2;
                int capY = 6;
                using (var capBrush = new SolidBrush(Color.FromArgb(255, 20, 20, 20)))
                {
                    g.FillRectangle(capBrush, capX, capY, capWidth, capHeight);
                }

                // Battery Body Dimensions
                int bodyX = 20;
                int bodyY = 18;
                int bodyWidth = 88;
                int bodyHeight = 102;
                int cornerRadius = 12;

                // Gray Fill Background
                using (var bgBrush = new SolidBrush(Color.FromArgb(255, 170, 175, 185)))
                {
                    FillRoundedRectangle(g, bgBrush, bodyX, bodyY, bodyWidth, bodyHeight, cornerRadius);
                }

                // Inner Battery Charge Level Indicator Bar (Green/Dark High Contrast Accent)
                int innerMargin = 12;
                int innerX = bodyX + innerMargin;
                int innerY = bodyY + 36;
                int innerWidth = bodyWidth - (innerMargin * 2);
                int innerHeight = bodyHeight - 48;
                using (var fillBrush = new SolidBrush(Color.FromArgb(255, 34, 197, 94))) // Green battery charge fill
                {
                    FillRoundedRectangle(g, fillBrush, innerX, innerY, innerWidth, innerHeight, 6);
                }

                // Black Outer Border
                using (var borderPen = new Pen(Color.Black, 10))
                {
                    borderPen.LineJoin = LineJoin.Round;
                    DrawRoundedRectangle(g, borderPen, bodyX, bodyY, bodyWidth, bodyHeight, cornerRadius);
                }
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

        private static void FillRoundedRectangle(Graphics g, Brush brush, int x, int y, int width, int height, int radius)
        {
            using var path = GetRoundedPath(x, y, width, height, radius);
            g.FillPath(brush, path);
        }

        private static void DrawRoundedRectangle(Graphics g, Pen pen, int x, int y, int width, int height, int radius)
        {
            using var path = GetRoundedPath(x, y, width, height, radius);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath GetRoundedPath(int x, int y, int width, int height, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(x, y, diameter, diameter, 180, 90);
            path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
            path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
            path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}
