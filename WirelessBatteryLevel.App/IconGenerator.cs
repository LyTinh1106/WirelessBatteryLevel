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
                int capHeight = 20;
                int capX = (size - capWidth) / 2;
                int capY = 4;
                using (var capBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 20, 20, 20)))
                {
                    g.FillRectangle(capBrush, capX, capY, capWidth, capHeight);
                }

                // Battery Body Dimensions (Slimmer & Taller proportions)
                int bodyX = 44;
                int bodyY = 22;
                int bodyWidth = 168;
                int bodyHeight = 228;
                int cornerRadius = 22;

                // Inner Fill Area
                int innerMargin = 12;
                int innerX = bodyX + innerMargin;
                int innerY = bodyY + innerMargin;
                int innerWidth = bodyWidth - (innerMargin * 2);
                int innerHeight = bodyHeight - (innerMargin * 2);
                int innerRadius = 14;

                // Create rounded clip path for inner battery fill
                using (var innerPath = GetRoundedPath(innerX, innerY, innerWidth, innerHeight, innerRadius))
                {
                    var oldClip = g.Clip;
                    g.SetClip(innerPath);

                    // Top 35% Gray Unfilled Area
                    int top35Height = (int)(innerHeight * 0.35);
                    using (var grayBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 71, 85, 105)))
                    {
                        g.FillRectangle(grayBrush, innerX, innerY, innerWidth, top35Height);
                    }

                    // Bottom 65% Green Filled Area
                    int bottom65Height = innerHeight - top35Height;
                    using (var greenBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 34, 197, 94)))
                    {
                        g.FillRectangle(greenBrush, innerX, innerY + top35Height, innerWidth, bottom65Height);
                    }

                    g.Clip = oldClip;
                }

                // Draw "ZTK" Text in the exact Middle of Battery Body
                using (var font = new Font("Arial Black", 58, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel))
                using (var textBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 15, 23, 42))) // High contrast dark text
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    var textRect = new RectangleF(bodyX - 4, bodyY + (bodyHeight - 86) / 2.0f, bodyWidth + 8, 86);
                    g.DrawString("ZTK", font, textBrush, textRect, sf);
                }

                // Outer Black Border
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
