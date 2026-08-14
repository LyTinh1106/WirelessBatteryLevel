using Microsoft.Extensions.DependencyInjection;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using WirelessBatteryLevel.App.Controls;
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
        private TrayHoverPopup? _hoverPopup;
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
            services.AddSingleton<TrayHoverPopup>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var mainWindow = Services.GetRequiredService<MainWindow>();
            _hoverPopup = Services.GetRequiredService<TrayHoverPopup>();

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

            // Toggle Flyout Window on click
            _notifyIcon.Click += (sender, args) =>
            {
                if (args is MouseEventArgs mouseArgs && mouseArgs.Button == MouseButtons.Left)
                {
                    if (_hoverPopup != null && _hoverPopup.IsVisible)
                    {
                        _hoverPopup.Hide();
                    }
                    mainWindow.ToggleVisibility();
                }
            };

            // Quick Hover Tooltip on Mouse Move over tray icon
            _notifyIcon.MouseMove += (sender, args) =>
            {
                if (mainWindow.IsVisible) return;

                if (_hoverPopup != null && !_hoverPopup.IsVisible)
                {
                    _hoverPopup.PositionNearTray();
                    _hoverPopup.Show();
                }
            };

            // Create Dark Gray Context Menu for Tray Icon
            var contextMenu = new ContextMenuStrip
            {
                Renderer = new ToolStripProfessionalRenderer(new DarkGrayColorTable()),
                ForeColor = System.Drawing.Color.FromArgb(225, 225, 225),
                BackColor = System.Drawing.Color.FromArgb(37, 37, 38),
                ShowImageMargin = false
            };
            
            contextMenu.Items.Add("Show device list", null, (sender, e) =>
            {
                if (_hoverPopup != null && _hoverPopup.IsVisible)
                {
                    _hoverPopup.Hide();
                }
                mainWindow.ToggleVisibility();
            });

            contextMenu.Items.Add(new ToolStripSeparator());

            contextMenu.Items.Add("Exit", null, (sender, e) =>
            {
                _hoverPopup?.Close();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                System.Windows.Application.Current.Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private class DarkGrayColorTable : ProfessionalColorTable
        {
            public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(37, 37, 38);
            public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(37, 37, 38);
            public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(37, 37, 38);
            public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(37, 37, 38);
            public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(55, 55, 61);
            public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.FromArgb(55, 55, 61);
            public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.FromArgb(55, 55, 61);
            public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(63, 63, 70);
            public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(63, 63, 70);
            public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(63, 63, 70);
            public override System.Drawing.Color SeparatorLight => System.Drawing.Color.FromArgb(45, 45, 48);
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
