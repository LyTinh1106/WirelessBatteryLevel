using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WirelessBatteryLevel.Core.Models;
using WirelessBatteryLevel.Infrastructure.Device;
using Application = System.Windows.Application;

namespace WirelessBatteryLevel.App.ViewModels
{
    public class TrayViewModel : INotifyPropertyChanged
    {
        private readonly DeviceMonitor _deviceMonitor;
        private string _lastUpdatedText = "Updating...";
        private bool _isRefreshing;

        public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();

        public string LastUpdatedText
        {
            get => _lastUpdatedText;
            set
            {
                _lastUpdatedText = value;
                OnPropertyChanged();
            }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                _isRefreshing = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ExitCommand { get; }

        public TrayViewModel(DeviceMonitor deviceMonitor)
        {
            _deviceMonitor = deviceMonitor;
            _deviceMonitor.DevicesUpdated += DeviceMonitor_DevicesUpdated;

            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            ExitCommand = new RelayCommand(ExitApp);
        }

        public async Task StartMonitoringAsync()
        {
            await _deviceMonitor.StartAsync(TimeSpan.FromSeconds(45));
        }

        public void StopMonitoring()
        {
            _deviceMonitor.DevicesUpdated -= DeviceMonitor_DevicesUpdated;
            _deviceMonitor.Stop();
        }

        private async Task RefreshAsync()
        {
            if (IsRefreshing)
                return;

            IsRefreshing = true;
            try
            {
                var statuses = await _deviceMonitor.ForceRefreshAsync();
                UpdateDevicesList(statuses);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void DeviceMonitor_DevicesUpdated(object? sender, IReadOnlyList<DeviceStatus> statuses)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateDevicesList(statuses);
            });
        }

        private void UpdateDevicesList(IReadOnlyList<DeviceStatus> statuses)
        {
            var sortedStatuses = statuses
                .OrderByDescending(s => s.Device.IsConnected)
                .ThenBy(s => s.Device.Name)
                .ToList();

            Devices.Clear();

            foreach (var status in sortedStatuses)
            {
                Devices.Add(new DeviceItemViewModel(status));
            }

            LastUpdatedText = $"Updated at: {DateTime.Now:HH:mm:ss}";
        }

        private void ExitApp()
        {
            StopMonitoring();
            Application.Current?.Shutdown();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
