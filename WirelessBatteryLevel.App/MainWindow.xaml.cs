using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WirelessBatteryLevel.App.Helpers;
using WirelessBatteryLevel.App.Services;
using WirelessBatteryLevel.App.ViewModels;

namespace WirelessBatteryLevel.App
{
    public partial class MainWindow : Window
    {
        private readonly TrayViewModel _viewModel;
        private readonly DispatcherTimer _autoCloseTimer;
        public ContextMenu? ActiveContextMenu { get; private set; }

        public MainWindow(TrayViewModel viewModel)
        {
            InitializeComponent();

            Icon = IconGenerator.GetWpfIconSource();

            _viewModel = viewModel;
            DataContext = _viewModel;

            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AppSettingsService.Instance.AutoCloseSeconds)
            };
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer.Stop();
                HideWindow();
            };

            AppSettingsService.Instance.SettingsChanged += (s, e) =>
            {
                _autoCloseTimer.Interval = TimeSpan.FromSeconds(AppSettingsService.Instance.AutoCloseSeconds);
            };

            MouseEnter += (s, e) => ResetAutoCloseTimer();
            MouseMove += (s, e) => ResetAutoCloseTimer();
            
            // Intercept mouse clicks inside MainWindow to close ActiveContextMenu if open
            PreviewMouseDown += MainWindow_PreviewMouseDown;
            Deactivated += (s, e) =>
            {
                CloseActiveContextMenu();
                HideWindow();
            };

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ResetAutoCloseTimer();

            if (ActiveContextMenu != null && ActiveContextMenu.IsOpen)
            {
                var source = e.OriginalSource as DependencyObject;
                if (source != null && IsDescendantOfContextMenu(source, ActiveContextMenu))
                {
                    return;
                }

                // Clicked outside ContextMenu inside MainWindow -> Close ActiveContextMenu!
                CloseActiveContextMenu();
            }
        }

        public void CloseActiveContextMenu()
        {
            if (ActiveContextMenu != null)
            {
                if (ActiveContextMenu.IsOpen)
                {
                    ActiveContextMenu.IsOpen = false;
                }
                ActiveContextMenu = null;
            }
        }

        private static bool IsDescendantOfContextMenu(DependencyObject element, ContextMenu menu)
        {
            var current = element;
            while (current != null)
            {
                if (current == menu)
                    return true;

                if (current is Popup popup && popup.Child == menu)
                    return true;

                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            }
            return false;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.StartMonitoringAsync();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                ResetAutoCloseTimer();
                DragMove();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ResetAutoCloseTimer();

            if (ActiveContextMenu != null && ActiveContextMenu.IsOpen)
            {
                CloseActiveContextMenu();
                return;
            }

            if (sender is System.Windows.Controls.Button settingsBtn)
            {
                var contextMenu = CreateSettingsContextMenu(includeExitItem: false);
                ActiveContextMenu = contextMenu;
                contextMenu.PlacementTarget = settingsBtn;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            }
        }

        public ContextMenu CreateSettingsContextMenu(bool includeExitItem = false)
        {
            var contextMenu = new ContextMenu
            {
                Style = (Style)FindResource("Win10ContextMenuStyle"),
                StaysOpen = false
            };

            // 5-second Auto-close Timer for Menu (Closes menu automatically after 5s idle)
            var menuAutoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            menuAutoCloseTimer.Tick += (s, e) =>
            {
                menuAutoCloseTimer.Stop();
                if (contextMenu.IsOpen)
                {
                    contextMenu.IsOpen = false;
                }
            };

            MouseButtonEventHandler outsideClickHandler = (s, e) =>
            {
                if (contextMenu.IsOpen)
                {
                    contextMenu.IsOpen = false;
                }
            };

            contextMenu.Opened += (s, e) =>
            {
                menuAutoCloseTimer.Stop();
                menuAutoCloseTimer.Start();
                Mouse.Capture(contextMenu, CaptureMode.SubTree);
                Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(contextMenu, outsideClickHandler);
            };

            contextMenu.Closed += (s, e) =>
            {
                Mouse.RemovePreviewMouseDownOutsideCapturedElementHandler(contextMenu, outsideClickHandler);
                if (Mouse.Captured == contextMenu)
                {
                    Mouse.Capture(null);
                }
                menuAutoCloseTimer.Stop();
                if (ActiveContextMenu == contextMenu)
                {
                    ActiveContextMenu = null;
                }
            };

            contextMenu.MouseEnter += (s, e) =>
            {
                menuAutoCloseTimer.Stop();
            };

            contextMenu.MouseLeave += (s, e) =>
            {
                if (contextMenu.IsOpen)
                {
                    menuAutoCloseTimer.Stop();
                    menuAutoCloseTimer.Start();
                }
            };

            // 1. Window Auto-Close Time Sub-Menu
            var autoCloseMenu = new MenuItem
            {
                Header = "Window Auto-Close Time",
                Style = (Style)FindResource("Win10MenuItemStyle")
            };
            var closeTimes = new (string Label, int Seconds)[]
            {
                ("15s", 15),
                ("30s", 30),
                ("45s", 45),
                ("1m", 60),
                ("2m", 120)
            };
            foreach (var (label, sec) in closeTimes)
            {
                var targetSec = sec;
                var item = new MenuItem
                {
                    Header = label,
                    IsCheckable = true,
                    IsChecked = AppSettingsService.Instance.AutoCloseSeconds == targetSec,
                    Style = (Style)FindResource("Win10MenuItemStyle")
                };
                item.Click += (s, e) =>
                {
                    AppSettingsService.Instance.AutoCloseSeconds = targetSec;
                    contextMenu.IsOpen = false;
                };
                autoCloseMenu.Items.Add(item);
            }

            // 2. Auto-Refresh Interval Sub-Menu
            var refreshMenu = new MenuItem
            {
                Header = "Auto-Refresh Interval",
                Style = (Style)FindResource("Win10MenuItemStyle")
            };
            var refreshTimes = new (string Label, int Seconds)[]
            {
                ("15s", 15),
                ("30s", 30),
                ("45s", 45),
                ("1m", 60),
                ("2m", 120)
            };
            foreach (var (label, sec) in refreshTimes)
            {
                var targetSec = sec;
                var item = new MenuItem
                {
                    Header = label,
                    IsCheckable = true,
                    IsChecked = AppSettingsService.Instance.RefreshIntervalSeconds == targetSec,
                    Style = (Style)FindResource("Win10MenuItemStyle")
                };
                item.Click += (s, e) =>
                {
                    AppSettingsService.Instance.RefreshIntervalSeconds = targetSec;
                    contextMenu.IsOpen = false;
                };
                refreshMenu.Items.Add(item);
            }

            // 3. Set Battery Color Mode Sub-Menu
            var batteryColorMenu = new MenuItem
            {
                Header = "Set Battery Color",
                Style = (Style)FindResource("Win10MenuItemStyle")
            };
            var defaultWhiteItem = new MenuItem
            {
                Header = "Default (White)",
                IsCheckable = true,
                IsChecked = AppSettingsService.Instance.BatteryColorMode == BatteryColorMode.DefaultWhite,
                Style = (Style)FindResource("Win10MenuItemStyle")
            };
            defaultWhiteItem.Click += (s, e) =>
            {
                AppSettingsService.Instance.BatteryColorMode = BatteryColorMode.DefaultWhite;
                contextMenu.IsOpen = false;
            };

            var dynamicColorItem = new MenuItem
            {
                Header = "Display Battery Color",
                IsCheckable = true,
                IsChecked = AppSettingsService.Instance.BatteryColorMode == BatteryColorMode.DynamicColors,
                Style = (Style)FindResource("Win10MenuItemStyle")
            };
            dynamicColorItem.Click += (s, e) =>
            {
                AppSettingsService.Instance.BatteryColorMode = BatteryColorMode.DynamicColors;
                contextMenu.IsOpen = false;
            };

            batteryColorMenu.Items.Add(defaultWhiteItem);
            batteryColorMenu.Items.Add(dynamicColorItem);

            contextMenu.Items.Add(autoCloseMenu);
            contextMenu.Items.Add(refreshMenu);
            contextMenu.Items.Add(batteryColorMenu);

            if (includeExitItem)
            {
                contextMenu.Items.Add(new Separator());
                var exitItem = new MenuItem
                {
                    Header = "Exit",
                    Style = (Style)FindResource("Win10MenuItemStyle")
                };
                exitItem.Click += (s, e) =>
                {
                    contextMenu.IsOpen = false;
                    System.Windows.Application.Current.Shutdown();
                };
                contextMenu.Items.Add(exitItem);
            }

            return contextMenu;
        }

        public void ToggleVisibility()
        {
            if (IsVisible)
            {
                HideWindow();
            }
            else
            {
                PositionNearTray();
                Show();
                Activate();
                ResetAutoCloseTimer();
            }
        }

        public void ResetAutoCloseTimer()
        {
            if (IsVisible)
            {
                _autoCloseTimer.Stop();
                _autoCloseTimer.Start();
            }
        }

        private void HideWindow()
        {
            CloseActiveContextMenu();
            _autoCloseTimer.Stop();
            Hide();
            MemoryCleaner.TrimWorkingSet();
        }

        private void PositionNearTray()
        {
            // Position flush to bottom-right corner and top edge of Taskbar (gap = 0)
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width;
            Top = workingArea.Bottom - Height;
        }

        protected override void OnClosed(EventArgs e)
        {
            CloseActiveContextMenu();
            _autoCloseTimer.Stop();
            _viewModel.StopMonitoring();
            base.OnClosed(e);
        }
    }
}