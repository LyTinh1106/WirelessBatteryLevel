using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
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
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(System.Drawing.Color.Transparent);

                // Top Battery Nipple / Cap
                int capWidth = 72;
                int capHeight = 28;
                int capX = (size - capWidth) / 2;
                int capY = 12;
                using (var capBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 30, 30, 30)))
                {
                    g.FillRectangle(capBrush, capX, capY, capWidth, capHeight);
                }

                // Battery Body Dimensions
                int bodyX = 40;
                int bodyY = 36;
                int bodyWidth = 176;
                int bodyHeight = 204;
                int cornerRadius = 24;

                // Gray Fill Background
                using (var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 170, 175, 185)))
                {
                    FillRoundedRectangle(g, bgBrush, bodyX, bodyY, bodyWidth, bodyHeight, cornerRadius);
                }

                // Inner Battery Charge Level Indicator Bar (Green Accent)
                int innerMargin = 24;
                int innerX = bodyX + innerMargin;
                int innerY = bodyY + 72;
                int innerWidth = bodyWidth - (innerMargin * 2);
                int innerHeight = bodyHeight - 96;
                using (var fillBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 34, 197, 94))) // Green battery fill
                {
                    FillRoundedRectangle(g, fillBrush, innerX, innerY, innerWidth, innerHeight, 12);
                }

                // Draw "ZTK" Text on Battery Cap/Body area
                using (var font = new Font("Segoe UI", 32, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
                using (var textBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 20, 20, 20)))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    // Draw ZTK in upper portion of battery body
                    var textRect = new RectangleF(bodyX, bodyY + 12, bodyWidth, 54);
                    g.DrawString("ZTK", font, textBrush, textRect, sf);
                }

                // Black Outer Border
                using (var borderPen = new System.Drawing.Pen(System.Drawing.Color.Black, 16))
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

        private static void FillRoundedRectangle(Graphics g, System.Drawing.Brush brush, int x, int y, int width, int height, int radius)
        {
            using var path = GetRoundedPath(x, y, width, height, radius);
            g.FillPath(brush, path);
        }

        private static void DrawRoundedRectangle(Graphics g, System.Drawing.Pen pen, int x, int y, int width, int height, int radius)
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
