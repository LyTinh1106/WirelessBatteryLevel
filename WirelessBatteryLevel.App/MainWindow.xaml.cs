using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WirelessBatteryLevel.App.Helpers;
using WirelessBatteryLevel.App.ViewModels;

namespace WirelessBatteryLevel.App
{
    public partial class MainWindow : Window
    {
        private readonly TrayViewModel _viewModel;
        private readonly DispatcherTimer _autoCloseTimer;

        public MainWindow(TrayViewModel viewModel)
        {
            InitializeComponent();

            Icon = IconGenerator.GetWpfIconSource();

            _viewModel = viewModel;
            DataContext = _viewModel;

            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer.Stop();
                HideWindow();
            };

            MouseEnter += (s, e) => ResetAutoCloseTimer();
            MouseMove += (s, e) => ResetAutoCloseTimer();

            Loaded += MainWindow_Loaded;
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            HideWindow();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ResetAutoCloseTimer();
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
            _autoCloseTimer.Stop();
            _viewModel.StopMonitoring();
            base.OnClosed(e);
        }
    }
}