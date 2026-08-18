using System;
using Windows.UI.ViewManagement;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfApplication = System.Windows.Application;

namespace WirelessBatteryLevel.App.Helpers
{
    public static class SystemThemeHelper
    {
        public static void ApplySystemAccentColor()
        {
            try
            {
                var uiSettings = new UISettings();
                var accentColor = uiSettings.GetColorValue(UIColorType.Accent);
                var mediaColor = MediaColor.FromArgb(accentColor.A, accentColor.R, accentColor.G, accentColor.B);

                var accentBrush = new SolidColorBrush(mediaColor);
                accentBrush.Freeze();

                // Lighter variant for hover state
                var hoverMediaColor = MediaColor.FromArgb(
                    accentColor.A,
                    (byte)Math.Min(255, accentColor.R + 40),
                    (byte)Math.Min(255, accentColor.G + 40),
                    (byte)Math.Min(255, accentColor.B + 40)
                );
                var accentHoverBrush = new SolidColorBrush(hoverMediaColor);
                accentHoverBrush.Freeze();

                if (WpfApplication.Current != null)
                {
                    WpfApplication.Current.Resources["SystemAccentBrush"] = accentBrush;
                    WpfApplication.Current.Resources["SystemAccentHoverBrush"] = accentHoverBrush;
                    WpfApplication.Current.Resources["SystemAccentColor"] = mediaColor;
                }
            }
            catch
            {
                // Fallback to standard Windows 10 Accent Blue (#0078D4)
                var fallbackColor = (MediaColor)MediaColorConverter.ConvertFromString("#0078D4");
                var fallbackBrush = new SolidColorBrush(fallbackColor);
                fallbackBrush.Freeze();

                var hoverFallbackColor = (MediaColor)MediaColorConverter.ConvertFromString("#106EBE");
                var hoverFallbackBrush = new SolidColorBrush(hoverFallbackColor);
                hoverFallbackBrush.Freeze();

                if (WpfApplication.Current != null)
                {
                    WpfApplication.Current.Resources["SystemAccentBrush"] = fallbackBrush;
                    WpfApplication.Current.Resources["SystemAccentHoverBrush"] = hoverFallbackBrush;
                    WpfApplication.Current.Resources["SystemAccentColor"] = fallbackColor;
                }
            }
        }
    }
}
