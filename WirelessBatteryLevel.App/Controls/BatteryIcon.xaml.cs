using System;
using System.Globalization;
using System.Windows.Data;
using UserControl = System.Windows.Controls.UserControl;

namespace WirelessBatteryLevel.App.Controls
{
    public partial class BatteryIcon : UserControl
    {
        public BatteryIcon()
        {
            InitializeComponent();
        }
    }

    public class BatteryWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length > 0 && values[0] is double ratio)
            {
                double maxWidth = 19.0;
                if (parameter != null && double.TryParse(parameter.ToString(), out var parsedMax))
                {
                    maxWidth = parsedMax;
                }

                return Math.Max(0, Math.Min(maxWidth, ratio * maxWidth));
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
