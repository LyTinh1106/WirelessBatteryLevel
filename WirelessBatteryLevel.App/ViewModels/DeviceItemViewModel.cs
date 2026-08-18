using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WirelessBatteryLevel.App.Services;
using WirelessBatteryLevel.Core.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace WirelessBatteryLevel.App.ViewModels
{
    public class DeviceItemViewModel : INotifyPropertyChanged
    {
        private static readonly Brush GreenStatusBrush = CreateFrozenBrush("#22C55E");
        private static readonly Brush GrayBrush = CreateFrozenBrush("#6B7280");
        private static readonly Brush GreenBatteryBrush = CreateFrozenBrush("#10B981");
        private static readonly Brush YellowBatteryBrush = CreateFrozenBrush("#F59E0B");
        private static readonly Brush RedBatteryBrush = CreateFrozenBrush("#EF4444");
        private static readonly Brush WhiteBatteryBrush = CreateFrozenBrush("#FFFFFF");

        private static Brush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private DeviceStatus _status;
        private BatteryDisplayStyle _displayStyle = BatteryDisplayStyle.ClassicBattery;

        public DeviceItemViewModel(DeviceStatus status)
        {
            _status = status;
            AppSettingsService.Instance.SettingsChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(BatteryFillBrush));
                OnPropertyChanged(nameof(IsMonochromeMode));
            };
            UpdateFromStatus(status);
        }

        public string Key => !string.IsNullOrWhiteSpace(_status.Device.Address) ? _status.Device.Address : _status.Device.Id;

        public void Update(DeviceStatus status)
        {
            _status = status;
            UpdateFromStatus(status);
            OnPropertyChanged(string.Empty);
        }

        public string Name => _status.Device.Name;

        public string SourceText => _status.Device.Source == DeviceSource.BluetoothLE ? "BLE" : "Classic";

        public bool IsConnected => _status.Device.IsConnected;

        public Brush IndicatorBrush => IsConnected ? GreenBatteryBrush : GrayBrush;

        public string FullTooltipText => $"{Name} - {(SourceText == "BLE" ? "Bluetooth LE" : "Classic Bluetooth")}";

        public string BatteryPercentageText => HasBattery && BatteryLevel.HasValue ? $"{BatteryLevel.Value}%" : "0%";

        public Brush StatusBrush => IsConnected ? GreenStatusBrush : GrayBrush;

        public string StatusTooltip => IsConnected ? "Connected" : "Disconnected";

        public bool HasBattery => IsConnected && _status.Battery != null && _status.Battery.IsAvailable;

        public int? BatteryLevel => HasBattery ? _status.Battery?.Level : null;

        public string BatteryTooltip => HasBattery ? $"{BatteryLevel}%" : "0%";

        public BatteryDisplayStyle DisplayStyle
        {
            get => _displayStyle;
            set
            {
                if (_displayStyle != value)
                {
                    _displayStyle = value;
                    OnPropertyChanged(nameof(DisplayStyle));
                    OnPropertyChanged(nameof(IsLinearBarMode));
                    OnPropertyChanged(nameof(IsClassicMode));
                }
            }
        }

        public bool IsLinearBarMode => DisplayStyle == BatteryDisplayStyle.LinearCapsuleBar;

        public bool IsClassicMode => !IsLinearBarMode;

        public bool IsMonochromeMode => AppSettingsService.Instance.BatteryColorMode == BatteryColorMode.DefaultWhite;

        public Brush BatteryFillBrush
        {
            get
            {
                if (!HasBattery || !BatteryLevel.HasValue)
                {
                    return GrayBrush;
                }

                if (AppSettingsService.Instance.BatteryColorMode == BatteryColorMode.DefaultWhite)
                {
                    return WhiteBatteryBrush;
                }

                var level = BatteryLevel.Value;
                if (level > 50)
                {
                    return GreenBatteryBrush;
                }
                else if (level >= 20)
                {
                    return YellowBatteryBrush;
                }
                else
                {
                    return RedBatteryBrush;
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
