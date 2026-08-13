using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WirelessBatteryLevel.App.Helpers;
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

            var existingMap = Devices.ToDictionary(d => d.Key, d => d);
            var newKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < sortedStatuses.Count; i++)
            {
                var status = sortedStatuses[i];
                var key = !string.IsNullOrWhiteSpace(status.Device.Address) ? status.Device.Address : status.Device.Id;
                newKeys.Add(key);

                if (existingMap.TryGetValue(key, out var existingVm))
                {
                    existingVm.Update(status);
                    int currentIndex = Devices.IndexOf(existingVm);
                    if (currentIndex != i && currentIndex >= 0)
                    {
                        Devices.Move(currentIndex, i);
                    }
                }
                else
                {
                    var newVm = new DeviceItemViewModel(status);
                    if (i < Devices.Count)
                        Devices.Insert(i, newVm);
                    else
                        Devices.Add(newVm);
                }
            }

            for (int i = Devices.Count - 1; i >= 0; i--)
            {
                if (!newKeys.Contains(Devices[i].Key))
                {
                    Devices.RemoveAt(i);
                }
            }

            LastUpdatedText = $"Updated at: {DateTime.Now:HH:mm:ss}";

            MemoryCleaner.TrimWorkingSet();
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
