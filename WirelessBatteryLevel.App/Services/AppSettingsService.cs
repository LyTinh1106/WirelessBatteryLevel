using System;

namespace WirelessBatteryLevel.App.Services
{
    public enum BatteryColorMode
    {
        DefaultWhite,
        DynamicColors
    }

    public class AppSettingsService
    {
        private static readonly Lazy<AppSettingsService> _instance = new(() => new AppSettingsService());
        public static AppSettingsService Instance => _instance.Value;

        private int _autoCloseSeconds = 60;
        private int _refreshIntervalSeconds = 60;
        private BatteryColorMode _batteryColorMode = BatteryColorMode.DefaultWhite;
        private bool _isPinToTrayEnabled = false;

        public event EventHandler? SettingsChanged;

        private AppSettingsService()
        {
        }

        public bool IsPinToTrayEnabled
        {
            get => _isPinToTrayEnabled;
            set
            {
                if (_isPinToTrayEnabled != value)
                {
                    _isPinToTrayEnabled = value;
                    OnSettingsChanged();
                }
            }
        }

        public int AutoCloseSeconds
        {
            get => _autoCloseSeconds;
            set
            {
                if (_autoCloseSeconds != value)
                {
                    _autoCloseSeconds = value;
                    OnSettingsChanged();
                }
            }
        }

        public int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set
            {
                if (_refreshIntervalSeconds != value)
                {
                    _refreshIntervalSeconds = value;
                    OnSettingsChanged();
                }
            }
        }

        public BatteryColorMode BatteryColorMode
        {
            get => _batteryColorMode;
            set
            {
                if (_batteryColorMode != value)
                {
                    _batteryColorMode = value;
                    OnSettingsChanged();
                }
            }
        }



        private void OnSettingsChanged()
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
