using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WirelessBatteryLevel.App
{
    public static class IconGenerator
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon CreateZtkIconInstance()
        {
            try
            {
                using var ms = GenerateBatteryIcon();
                using var tempIcon = new Icon(ms);
                return (Icon)tempIcon.Clone();
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private static ImageSource? _cachedWpfIconSource;

        public static ImageSource GetWpfIconSource()
        {
            if (_cachedWpfIconSource != null)
                return _cachedWpfIconSource;

            using var icon = CreateZtkIconInstance();
            try
            {
                var bmpSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                if (bmpSource.CanFreeze)
                {
                    bmpSource.Freeze();
                }

                _cachedWpfIconSource = bmpSource;
                return _cachedWpfIconSource;
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
                g.SmoothingMode = SmoothingMode.AntiAlias; // SMOOTH SHARP VECTOR RENDERING
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.Transparent);

                using var whiteBrush = new SolidBrush(System.Drawing.Color.White);
                using var whitePen = new System.Drawing.Pen(System.Drawing.Color.White, 14)
                {
                    LineJoin = LineJoin.Miter,
                    MiterLimit = 10
                };
                using var bluetoothPen = new System.Drawing.Pen(System.Drawing.Color.White, 20)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };

                // Sleek & Taller Vertical Battery Layout (Hollow 0% Fill Outline with White Bluetooth Overlay)
                // 1. Top Battery Terminal Stud (Solid White Rectangle Centered on Top)
                int studWidth = 48;
                int studHeight = 16;
                int studX = (size - studWidth) / 2; // 104
                int studY = 10;
                g.FillRectangle(whiteBrush, studX, studY, studWidth, studHeight);

                // 2. Sharp Outer Body Outline (14px White Stroke, 0-Radius Corners)
                int bodyWidth = 128;
                int bodyHeight = 218;
                int bodyX = (size - bodyWidth) / 2; // 64
                int bodyY = 25;
                g.DrawRectangle(whitePen, bodyX + 7, bodyY + 7, bodyWidth - 14, bodyHeight - 14);

                // 3. Inner Battery Fill: 0% Fill (Hollow / Transparent interior)

                // 4. Bluetooth Emblem Vector Overlay (Drawn in Solid WHITE inside hollow battery outline)
                float cx = bodyX + (bodyWidth / 2f);   // 128 (Exact Center X)
                float cy = bodyY + (bodyHeight / 2f);  // 134 (Exact Center Y)
                float R = 72f;                         // Large Height of Bluetooth Symbol (144px total height)
                float dx = 30f;                        // Large Width of Bluetooth Symbol (60px total width)

                PointF topStem = new PointF(cx, cy - R);
                PointF botStem = new PointF(cx, cy + R);
                PointF upperRight = new PointF(cx + dx, cy - (R * 0.5f));
                PointF lowerRight = new PointF(cx + dx, cy + (R * 0.5f));
                PointF upperLeft = new PointF(cx - dx, cy - (R * 0.5f));
                PointF lowerLeft = new PointF(cx - dx, cy + (R * 0.5f));

                // Vertical Stem
                g.DrawLine(bluetoothPen, topStem, botStem);

                // Bluetooth Upper & Lower Rune Loops
                g.DrawLines(bluetoothPen, new[] { topStem, upperRight, lowerLeft });
                g.DrawLines(bluetoothPen, new[] { upperLeft, lowerRight, botStem });
            }

            var ms = new MemoryStream();
            SaveAsIco(bmp, ms);
            ms.Position = 0;
            return ms;
        }

        private static void SaveAsIco(Bitmap masterBmp, Stream outputStream)
        {
            int[] sizes = new[] { 256, 48, 32, 16 };
            var pngBytesList = new System.Collections.Generic.List<byte[]>();

            foreach (var s in sizes)
            {
                if (s == masterBmp.Width && s == masterBmp.Height)
                {
                    using var ms = new MemoryStream();
                    masterBmp.Save(ms, ImageFormat.Png);
                    pngBytesList.Add(ms.ToArray());
                }
                else
                {
                    using var resized = new Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(resized))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(masterBmp, 0, 0, s, s);
                    }
                    using var ms = new MemoryStream();
                    resized.Save(ms, ImageFormat.Png);
                    pngBytesList.Add(ms.ToArray());
                }
            }

            using var writer = new BinaryWriter(outputStream, Encoding.UTF8, leaveOpen: true);
            writer.Write((ushort)0); // Reserved
            writer.Write((ushort)1); // Type = ICO
            writer.Write((ushort)sizes.Length); // Count

            uint currentOffset = (uint)(6 + (16 * sizes.Length));

            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                byte w = (byte)(s >= 256 ? 0 : s);
                byte h = (byte)(s >= 256 ? 0 : s);

                writer.Write(w);
                writer.Write(h);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);  // Planes
                writer.Write((ushort)32); // BPP
                writer.Write((uint)pngBytesList[i].Length);
                writer.Write(currentOffset);

                currentOffset += (uint)pngBytesList[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write(pngBytesList[i]);
            }

            writer.Flush();
        }
    }
}
