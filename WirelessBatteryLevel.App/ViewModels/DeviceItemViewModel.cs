using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WirelessBatteryLevel.Core.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace WirelessBatteryLevel.App.ViewModels
{
    public class DeviceItemViewModel : INotifyPropertyChanged
    {
        private DeviceStatus _status;

        public DeviceItemViewModel(DeviceStatus status)
        {
            _status = status;
            UpdateFromStatus(status);
        }

        public void Update(DeviceStatus status)
        {
            _status = status;
            UpdateFromStatus(status);
            OnPropertyChanged(string.Empty);
        }

        public string Name => _status.Device.Name;

        public string SourceText => _status.Device.Source == DeviceSource.BluetoothLE ? "BLE" : "Classic";

        public bool IsConnected => _status.Device.IsConnected;

        public Brush StatusBrush => IsConnected
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E")) // Green
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")); // Gray

        public string StatusTooltip => IsConnected ? "Connected" : "Disconnected";

        public bool HasBattery => IsConnected && _status.Battery != null && _status.Battery.IsAvailable;

        public int? BatteryLevel => HasBattery ? _status.Battery?.Level : null;

        public string BatteryTooltip => HasBattery ? $"{BatteryLevel}%" : "N/A";

        public Brush BatteryFillBrush
        {
            get
            {
                if (!HasBattery || !BatteryLevel.HasValue)
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")); // Gray N/A
                }

                var level = BatteryLevel.Value;
                if (level > 50)
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")); // Green
                }
                else if (level >= 20)
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // Yellow
                }
                else
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")); // Red
                }
            }
        }

        public double BatteryLevelWidthRatio
        {
            get
            {
                if (!HasBattery || !BatteryLevel.HasValue)
                    return 0;

                return Math.Max(0.05, Math.Min(1.0, BatteryLevel.Value / 100.0));
            }
        }

        private void UpdateFromStatus(DeviceStatus status)
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
