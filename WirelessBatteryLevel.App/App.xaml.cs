using Microsoft.Extensions.DependencyInjection;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
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

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var mainWindow = Services.GetRequiredService<MainWindow>();

            InitializeTrayIcon(mainWindow);
        }

        private void InitializeTrayIcon(MainWindow mainWindow)
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = IconGenerator.CreateZtkIconInstance(),
                Text = "Wireless Battery Level (ZTK)",
                Visible = true
            };

            // Toggle Flyout Window on click or mouse hover
            _notifyIcon.Click += (sender, args) =>
            {
                if (args is MouseEventArgs mouseArgs && mouseArgs.Button == MouseButtons.Left)
                {
                    mainWindow.ToggleVisibility();
                }
            };

            // Create Context Menu for Tray Icon
            var contextMenu = new ContextMenuStrip();
            
            contextMenu.Items.Add("Show device list", null, (sender, e) =>
            {
                mainWindow.ToggleVisibility();
            });

            contextMenu.Items.Add(new ToolStripSeparator());

            contextMenu.Items.Add("Exit", null, (sender, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                System.Windows.Application.Current.Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
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
