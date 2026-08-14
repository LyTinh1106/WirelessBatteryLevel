using System;
using System.Windows;
using System.Windows.Forms;
using WirelessBatteryLevel.App.Helpers;
using WirelessBatteryLevel.App.ViewModels;

namespace WirelessBatteryLevel.App
{
    public partial class MainWindow : Window
    {
        private readonly TrayViewModel _viewModel;

        public MainWindow(TrayViewModel viewModel)
        {
            InitializeComponent();

            Icon = IconGenerator.GetWpfIconSource();

            _viewModel = viewModel;
            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
            Deactivated += MainWindow_Deactivated;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.StartMonitoringAsync();
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            // Auto hide window when user clicks outside
            HideWindow();
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
            }
        }

        private void HideWindow()
        {
            Hide();
            MemoryCleaner.TrimWorkingSet();
        }

        private void PositionNearTray()
        {
            // Position near the bottom-right corner (System Tray area)
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 8;
            Top = workingArea.Bottom - Height - 8;
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.StopMonitoring();
            base.OnClosed(e);
        }
    }
}