using System;
using System.IO;
using System.Text.Json;

namespace WirelessBatteryLevel.App.Services
{
    public enum BatteryColorMode
    {
        DefaultWhite,
        DynamicColors
    }

    public class AppSettingsData
    {
        public bool IsPinToTrayEnabled { get; set; } = false;
        public int AutoCloseSeconds { get; set; } = 60;
        public int RefreshIntervalSeconds { get; set; } = 60;
        public BatteryColorMode BatteryColorMode { get; set; } = BatteryColorMode.DefaultWhite;
    }

    public class AppSettingsService
    {
        private static readonly Lazy<AppSettingsService> _instance = new(() => new AppSettingsService());
        public static AppSettingsService Instance => _instance.Value;

        private readonly string _settingsFilePath;
        private readonly object _lock = new();

        private int _autoCloseSeconds = 60;
        private int _refreshIntervalSeconds = 60;
        private BatteryColorMode _batteryColorMode = BatteryColorMode.DefaultWhite;
        private bool _isPinToTrayEnabled = false;

        public event EventHandler? SettingsChanged;

        private AppSettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDir = Path.Combine(appData, "WirelessBatteryLevel");
            _settingsFilePath = Path.Combine(appDir, "settings.json");

            LoadSettings();
        }

        public bool IsPinToTrayEnabled
        {
            get => _isPinToTrayEnabled;
            set
            {
                if (_isPinToTrayEnabled != value)
                {
                    _isPinToTrayEnabled = value;
                    SaveSettings();
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
                    SaveSettings();
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
                    SaveSettings();
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
                    SaveSettings();
                    OnSettingsChanged();
                }
            }
        }

        private void LoadSettings()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_settingsFilePath))
                    {
                        var json = File.ReadAllText(_settingsFilePath);
                        var data = JsonSerializer.Deserialize<AppSettingsData>(json);
                        if (data != null)
                        {
                            _isPinToTrayEnabled = data.IsPinToTrayEnabled;
                            _autoCloseSeconds = data.AutoCloseSeconds > 0 ? data.AutoCloseSeconds : 60;
                            _refreshIntervalSeconds = data.RefreshIntervalSeconds > 0 ? data.RefreshIntervalSeconds : 60;
                            _batteryColorMode = data.BatteryColorMode;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppSettingsService] Failed to load settings: {ex.Message}");
                }
            }
        }

        private void SaveSettings()
        {
            lock (_lock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(_settingsFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var data = new AppSettingsData
                    {
                        IsPinToTrayEnabled = _isPinToTrayEnabled,
                        AutoCloseSeconds = _autoCloseSeconds,
                        RefreshIntervalSeconds = _refreshIntervalSeconds,
                        BatteryColorMode = _batteryColorMode
                    };

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(data, options);
                    File.WriteAllText(_settingsFilePath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppSettingsService] Failed to save settings: {ex.Message}");
                }
            }
        }

        private void OnSettingsChanged()
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
