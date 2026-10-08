using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using WirelessBatteryLevel.App.Helpers;
using WirelessBatteryLevel.App.Services;
using WirelessBatteryLevel.App.ViewModels;
using WirelessBatteryLevel.Core.Interfaces;
using WirelessBatteryLevel.Infrastructure.Battery;
using WirelessBatteryLevel.Infrastructure.Device;
using WirelessBatteryLevel.Infrastructure.Discovery;

namespace WirelessBatteryLevel.App
{
    public partial class App : System.Windows.Application
    {
        private class SlotState
        {
            public bool IsLogoMode { get; set; } = true;
            public string DeviceKey { get; set; } = string.Empty;
            public bool IsConnected { get; set; }
            public int BatteryLevel { get; set; }
            public BatteryColorMode ColorMode { get; set; }
        }

        private NotifyIcon? _notifyIcon;
        private readonly SlotState _slot0State = new();
        private readonly List<NotifyIcon> _pinnedDeviceIcons = new();
        private readonly List<SlotState> _extraSlotStates = new();
        private IReadOnlyList<WirelessBatteryLevel.Core.Models.DeviceStatus> _lastStatuses = 
            new List<WirelessBatteryLevel.Core.Models.DeviceStatus>();

        public IServiceProvider Services { get; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public App()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            Services = serviceCollection.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Discovery Providers
            services.AddSingleton<IDeviceDiscovery, ClassicBluetoothDiscovery>();
            services.AddSingleton<IDeviceDiscovery, BluetoothLEDiscovery>();
            services.AddSingleton<DeviceAggregator>();
            services.AddSingleton<DeviceDiscoveryManager>();

            // Battery Providers
            services.AddSingleton<IBatteryProvider, BleBatteryProvider>();
            services.AddSingleton<IBatteryProvider, ClassicBatteryProvider>();
            services.AddSingleton<BatteryResolver>();

            // Device Core Managers
            services.AddSingleton<IDeviceManager, DeviceManager>();
            services.AddSingleton<DeviceMonitor>();
            services.AddSingleton<DeviceStateCache>();

            // ViewModels & UI
            services.AddSingleton<TrayViewModel>();
            services.AddSingleton<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SystemThemeHelper.ApplySystemAccentColor();

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var trayViewModel = Services.GetRequiredService<TrayViewModel>();
            _ = trayViewModel.StartMonitoringAsync();

            var mainWindow = Services.GetRequiredService<MainWindow>();

            InitializeTrayIcon(mainWindow);

            MemoryCleaner.TrimWorkingSet();
        }

        private void InitializeTrayIcon(MainWindow mainWindow)
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = IconGenerator.CreateZtkIconInstance(),
                Text = "Wireless Battery Level (ZTK)",
                Visible = true
            };

            var deviceMonitor = Services.GetRequiredService<DeviceMonitor>();
            deviceMonitor.DevicesUpdated += (sender, statuses) =>
            {
                _lastStatuses = statuses;
                Current.Dispatcher.Invoke(() =>
                {
                    UpdateTrayIconState(statuses, mainWindow);
                    MemoryCleaner.TrimWorkingSet();
                });
            };

            AppSettingsService.Instance.SettingsChanged += (sender, e) =>
            {
                Current.Dispatcher.Invoke(() =>
                {
                    UpdateTrayIconState(_lastStatuses, mainWindow);
                });
            };

            // Mouse click handling: Left-click toggles Flyout Window, Right-click opens modern WPF ContextMenu
            _notifyIcon.MouseUp += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left)
                {
                    mainWindow.ToggleVisibility();
                }
                else if (args.Button == MouseButtons.Right)
                {
                    OpenContextMenu(mainWindow);
                }
            };
        }

        private static void OpenContextMenu(MainWindow mainWindow)
        {
            if (mainWindow.ActiveContextMenu != null && mainWindow.ActiveContextMenu.IsOpen)
            {
                mainWindow.CloseActiveContextMenu();
                return;
            }

            var handle = new WindowInteropHelper(mainWindow).EnsureHandle();
            SetForegroundWindow(handle);

            var wpfContextMenu = mainWindow.CreateSettingsContextMenu(includeExitItem: true);
            wpfContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            wpfContextMenu.IsOpen = true;
        }

        private void UpdateTrayIconState(
            IReadOnlyList<WirelessBatteryLevel.Core.Models.DeviceStatus> statuses,
            MainWindow mainWindow)
        {
            if (_notifyIcon == null) return;

            try
            {
                // 1. Slot 0 (Main Anchor NotifyIcon) ALWAYS remains visible
                _notifyIcon.Visible = true;

                var connectedDevices = statuses.Where(s => s.Device.IsConnected).ToList();
                bool isPinEnabled = AppSettingsService.Instance.IsPinToTrayEnabled;

                // Case A: Pin to Tray is OFF OR 0 devices connected -> Slot 0 shows Logo App, extra slots hidden
                if (!isPinEnabled || connectedDevices.Count == 0)
                {
                    ClearExtraPinnedIcons();

                    if (!_slot0State.IsLogoMode || _notifyIcon.Icon == null)
                    {
                        var oldIcon = _notifyIcon.Icon;
                        _notifyIcon.Icon = IconGenerator.CreateZtkIconInstance();
                        if (oldIcon != null && oldIcon != System.Drawing.SystemIcons.Application)
                        {
                            oldIcon.Dispose();
                        }

                        _slot0State.IsLogoMode = true;
                        _slot0State.DeviceKey = string.Empty;
                        _slot0State.IsConnected = false;
                        _slot0State.BatteryLevel = 0;
                    }

                    string logoText = "Wireless Battery Level (ZTK)";
                    if (_notifyIcon.Text != logoText)
                    {
                        _notifyIcon.Text = logoText;
                    }
                    return;
                }

                // Case B: Pin to Tray is ON AND at least 1 device is connected!
                // Slot 0 (Main Anchor) REPLACES Logo App with Device 1's Battery Icon!
                var dev1Status = connectedDevices[0];
                string dev1Key = !string.IsNullOrWhiteSpace(dev1Status.Device.Address) ? dev1Status.Device.Address : dev1Status.Device.Id;
                bool dev1Connected = dev1Status.Device.IsConnected;
                int dev1Level = (dev1Status.Battery != null && dev1Status.Battery.IsAvailable && dev1Status.Battery.Level.HasValue) ? dev1Status.Battery.Level.Value : 0;
                var colorMode = AppSettingsService.Instance.BatteryColorMode;

                bool slot0Changed = _slot0State.IsLogoMode ||
                                    _slot0State.DeviceKey != dev1Key ||
                                    _slot0State.IsConnected != dev1Connected ||
                                    _slot0State.BatteryLevel != dev1Level ||
                                    _slot0State.ColorMode != colorMode;

                if (slot0Changed || _notifyIcon.Icon == null)
                {
                    var oldIcon = _notifyIcon.Icon;
                    _notifyIcon.Icon = IconGenerator.CreateSingleDeviceClassicIcon(dev1Status);
                    if (oldIcon != null && oldIcon != System.Drawing.SystemIcons.Application)
                    {
                        oldIcon.Dispose();
                    }

                    _slot0State.IsLogoMode = false;
                    _slot0State.DeviceKey = dev1Key;
                    _slot0State.IsConnected = dev1Connected;
                    _slot0State.BatteryLevel = dev1Level;
                    _slot0State.ColorMode = colorMode;
                }

                string dev1Text = $"{dev1Status.Device.Name}: {dev1Level}%";
                if (dev1Text.Length > 127) dev1Text = dev1Text.Substring(0, 124) + "...";
                if (_notifyIcon.Text != dev1Text)
                {
                    _notifyIcon.Text = dev1Text;
                }

                // Handle Extra Slots for Device 2, Device 3... (if connectedDevices.Count >= 2)
                int extraTargetCount = Math.Min(2, connectedDevices.Count - 1);

                // Add missing extra icon slots
                while (_pinnedDeviceIcons.Count < extraTargetCount)
                {
                    var pinIcon = new NotifyIcon { Visible = true };
                    pinIcon.MouseUp += (sender, args) =>
                    {
                        if (args.Button == MouseButtons.Left)
                        {
                            mainWindow.ToggleVisibility();
                        }
                        else if (args.Button == MouseButtons.Right)
                        {
                            OpenContextMenu(mainWindow);
                        }
                    };
                    _pinnedDeviceIcons.Add(pinIcon);
                    _extraSlotStates.Add(new SlotState());
                }

                // Remove excess extra icon slots
                while (_pinnedDeviceIcons.Count > extraTargetCount)
                {
                    int lastIdx = _pinnedDeviceIcons.Count - 1;
                    var iconToDispose = _pinnedDeviceIcons[lastIdx];
                    iconToDispose.Visible = false;
                    var oldIcon = iconToDispose.Icon;
                    iconToDispose.Dispose();
                    if (oldIcon != null && oldIcon != System.Drawing.SystemIcons.Application)
                    {
                        oldIcon.Dispose();
                    }
                    _pinnedDeviceIcons.RemoveAt(lastIdx);
                    _extraSlotStates.RemoveAt(lastIdx);
                }

                // Update each extra pinned icon for Device 2, 3...
                for (int i = 0; i < extraTargetCount; i++)
                {
                    var status = connectedDevices[i + 1];
                    string deviceKey = !string.IsNullOrWhiteSpace(status.Device.Address) ? status.Device.Address : status.Device.Id;
                    bool isConnected = status.Device.IsConnected;
                    int level = (status.Battery != null && status.Battery.IsAvailable && status.Battery.Level.HasValue) ? status.Battery.Level.Value : 0;

                    var extraState = _extraSlotStates[i];

                    bool extraChanged = extraState.IsLogoMode ||
                                        extraState.DeviceKey != deviceKey ||
                                        extraState.IsConnected != isConnected ||
                                        extraState.BatteryLevel != level ||
                                        extraState.ColorMode != colorMode;

                    if (extraChanged || _pinnedDeviceIcons[i].Icon == null)
                    {
                        var oldIcon = _pinnedDeviceIcons[i].Icon;
                        _pinnedDeviceIcons[i].Icon = IconGenerator.CreateSingleDeviceClassicIcon(status);
                        if (oldIcon != null && oldIcon != System.Drawing.SystemIcons.Application)
                        {
                            oldIcon.Dispose();
                        }

                        extraState.IsLogoMode = false;
                        extraState.DeviceKey = deviceKey;
                        extraState.IsConnected = isConnected;
                        extraState.BatteryLevel = level;
                        extraState.ColorMode = colorMode;
                    }

                    string pinText = $"{status.Device.Name}: {level}%";
                    if (pinText.Length > 127) pinText = pinText.Substring(0, 124) + "...";
                    if (_pinnedDeviceIcons[i].Text != pinText)
                    {
                        _pinnedDeviceIcons[i].Text = pinText;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Exception updating tray icon state: {ex.Message}");
            }
        }

        private void ClearExtraPinnedIcons()
        {
            foreach (var icon in _pinnedDeviceIcons)
            {
                icon.Visible = false;
                var oldIcon = icon.Icon;
                icon.Dispose();
                if (oldIcon != null && oldIcon != System.Drawing.SystemIcons.Application)
                {
                    oldIcon.Dispose();
                }
            }
            _pinnedDeviceIcons.Clear();
            _extraSlotStates.Clear();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ClearExtraPinnedIcons();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                var oldMainIcon = _notifyIcon.Icon;
                _notifyIcon.Dispose();
                if (oldMainIcon != null && oldMainIcon != System.Drawing.SystemIcons.Application)
                {
                    oldMainIcon.Dispose();
                }
            }

            base.OnExit(e);
        }
    }
}
