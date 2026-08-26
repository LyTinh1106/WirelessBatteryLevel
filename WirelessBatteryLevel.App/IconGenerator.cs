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

        public static Icon CreateSingleDeviceClassicIcon(WirelessBatteryLevel.Core.Models.DeviceStatus status)
        {
            int level = (status != null && status.Battery != null && status.Battery.IsAvailable && status.Battery.Level.HasValue) ? status.Battery.Level.Value : 0;
            int width = 32;
            int height = 32;

            using var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None; // SHARP PIXEL-PERFECT VECTOR EDGES (NO BLUR)
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.None;
                g.Clear(System.Drawing.Color.Transparent);

                DrawSingleClassicBatterySlot(g, 0, 0, width, height, level);
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var tempIcon = Icon.FromHandle(hIcon);
                return (Icon)tempIcon.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        public static Icon CreateClassicBatteryTrayIcon(IReadOnlyList<WirelessBatteryLevel.Core.Models.DeviceStatus> connectedDevices)
        {
            var activeDevices = connectedDevices != null 
                ? System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(connectedDevices, d => d.Device.IsConnected))
                : new System.Collections.Generic.List<WirelessBatteryLevel.Core.Models.DeviceStatus>();

            if (activeDevices.Count == 0)
            {
                return CreateZtkIconInstance();
            }

            int count = Math.Min(3, activeDevices.Count);
            int slotWidth = 32;
            int height = 32;
            int totalWidth = slotWidth * count; // 1x1 (32x32), 2x1 (64x32), 3x1 (96x32)

            using var bmp = new Bitmap(totalWidth, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None; // SHARP PIXEL-PERFECT VECTOR EDGES (NO BLUR)
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.None;
                g.Clear(System.Drawing.Color.Transparent);

                for (int i = 0; i < count; i++)
                {
                    var status = activeDevices[i];
                    int level = (status.Battery != null && status.Battery.IsAvailable && status.Battery.Level.HasValue) ? status.Battery.Level.Value : 0;

                    int offsetX = i * slotWidth;
                    DrawSingleClassicBatterySlot(g, offsetX, 0, slotWidth, height, level);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var tempIcon = Icon.FromHandle(hIcon);
                // Clone icon to unbind handle
                return (Icon)tempIcon.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        private static void DrawSingleClassicBatterySlot(Graphics g, int x, int y, int width, int height, int level)
        {
            using var whiteBrush = new SolidBrush(System.Drawing.Color.White);
            using var borderPen = new System.Drawing.Pen(System.Drawing.Color.White, 2f)
            {
                LineJoin = LineJoin.Miter,
                MiterLimit = 10
            };

            // Ultra-Wide Sleek Horizontal Battery Layout (Sharp Pixel-Perfect Crisp Detail)
            int bodyW = 27;
            int bodyH = 18;
            int bodyX = x + 1; // 1px left padding
            int bodyY = y + 7; // 7px top padding (7px top, 7px bottom)

            // 1. Right Battery Terminal Cap (Solid White)
            int capW = 3;
            int capH = 10;
            int capX = bodyX + bodyW;
            int capY = bodyY + 4; // Centered vertically on body
            g.FillRectangle(whiteBrush, capX, capY, capW, capH);

            // 2. Main Outer Battery Body Outline (Sharp Crisp Corners)
            g.DrawRectangle(borderPen, bodyX, bodyY, bodyW - 1, bodyH - 1);

            // 3. Inner Battery Fill Level (Horizontal Crisp Fill from Left to Right)
            int innerPadding = 3;
            int innerX = bodyX + innerPadding;
            int innerY = bodyY + innerPadding;
            int innerW = bodyW - (innerPadding * 2) - 1;
            int innerH = bodyH - (innerPadding * 2) - 1;

            int clampedLevel = Math.Max(0, Math.Min(100, level));
            int fillW = (int)(innerW * (clampedLevel / 100.0));
            if (fillW > 0)
            {
                g.FillRectangle(whiteBrush, innerX, innerY, fillW, innerH);
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
