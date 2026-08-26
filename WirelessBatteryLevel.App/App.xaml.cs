using Microsoft.Extensions.DependencyInjection;
using System;
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
        private NotifyIcon? _notifyIcon;
        private readonly System.Collections.Generic.List<NotifyIcon> _pinnedDeviceIcons = new();
        private System.Collections.Generic.IReadOnlyList<WirelessBatteryLevel.Core.Models.DeviceStatus> _lastStatuses = 
            new System.Collections.Generic.List<WirelessBatteryLevel.Core.Models.DeviceStatus>();

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
            System.Collections.Generic.IReadOnlyList<WirelessBatteryLevel.Core.Models.DeviceStatus> statuses,
            MainWindow mainWindow)
        {
            if (_notifyIcon == null) return;

            try
            {
                var connectedDevices = System.Linq.Enumerable.ToList(
                    System.Linq.Enumerable.Where(statuses, s => s.Device.IsConnected));

                // 1. Keep Main NotifyIcon with standard app icon
                _notifyIcon.Icon = IconGenerator.CreateZtkIconInstance();
                _notifyIcon.Text = "Wireless Battery Level (ZTK)";

                // 2. Handle Pinned Device NotifyIcons (Optional feature, Default = OFF)
                if (!AppSettingsService.Instance.IsPinToTrayEnabled)
                {
                    ClearPinnedIcons();
                    return;
                }

                int targetCount = Math.Min(3, connectedDevices.Count);

                // Create missing pinned icons
                while (_pinnedDeviceIcons.Count < targetCount)
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
                }

                // Remove extra pinned icons
                while (_pinnedDeviceIcons.Count > targetCount)
                {
                    int lastIdx = _pinnedDeviceIcons.Count - 1;
                    var iconToDispose = _pinnedDeviceIcons[lastIdx];
                    iconToDispose.Visible = false;
                    iconToDispose.Dispose();
                    _pinnedDeviceIcons.RemoveAt(lastIdx);
                }

                // Update each pinned icon
                for (int i = 0; i < targetCount; i++)
                {
                    var status = connectedDevices[i];
                    int level = (status.Battery != null && status.Battery.IsAvailable && status.Battery.Level.HasValue) ? status.Battery.Level.Value : 0;

                    var oldIcon = _pinnedDeviceIcons[i].Icon;
                    _pinnedDeviceIcons[i].Icon = IconGenerator.CreateSingleDeviceClassicIcon(status);
                    if (oldIcon != null && oldIcon != System.Drawing.SystemIcons.Application)
                    {
                        oldIcon.Dispose();
                    }

                    string pinText = $"{status.Device.Name}: {level}%";
                    if (pinText.Length > 127) pinText = pinText.Substring(0, 124) + "...";
                    _pinnedDeviceIcons[i].Text = pinText;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Exception updating tray icon state: {ex.Message}");
            }
        }

        private void ClearPinnedIcons()
        {
            foreach (var icon in _pinnedDeviceIcons)
            {
                icon.Visible = false;
                icon.Dispose();
            }
            _pinnedDeviceIcons.Clear();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ClearPinnedIcons();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            base.OnExit(e);
        }
    }
}
