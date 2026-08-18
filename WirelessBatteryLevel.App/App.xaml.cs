using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using WirelessBatteryLevel.App.Helpers;
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

            // Mouse click handling: Left-click toggles Flyout Window, Right-click opens modern WPF ContextMenu
            _notifyIcon.MouseUp += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left)
                {
                    mainWindow.ToggleVisibility();
                }
                else if (args.Button == MouseButtons.Right)
                {
                    if (mainWindow.ActiveContextMenu != null && mainWindow.ActiveContextMenu.IsOpen)
                    {
                        mainWindow.CloseActiveContextMenu();
                        return;
                    }

                    // Win32 Requirement: Set foreground window before opening tray context menu
                    // so Windows routes click-outside dismissal events to the WPF ContextMenu.
                    var handle = new WindowInteropHelper(mainWindow).EnsureHandle();
                    SetForegroundWindow(handle);

                    var wpfContextMenu = mainWindow.CreateSettingsContextMenu(includeExitItem: true);
                    wpfContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                    wpfContextMenu.IsOpen = true;
                }
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            base.OnExit(e);
        }
    }
}
