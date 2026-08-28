using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using WirelessBatteryLevel.Core.Interfaces;
using WirelessBatteryLevel.Core.Models;

namespace WirelessBatteryLevel.Infrastructure.Discovery
{
    public class BluetoothLEDiscovery : IDeviceDiscovery
    {
        private static readonly string[] RequestedProperties = new[]
        {
            "System.Devices.Aep.IsConnected",
            "System.Devices.Aep.DeviceAddress",
            "System.ItemNameDisplay"
        };

        public async Task<IReadOnlyList<WirelessDevice>> DiscoverAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var selector = BluetoothLEDevice.GetDeviceSelector();

            var deviceInformationCollection =
                await DeviceInformation.FindAllAsync(selector, RequestedProperties);

            var devices = new List<WirelessDevice>();

            foreach (var deviceInformation in deviceInformationCollection)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    bool isConnected = false;
                    if (deviceInformation.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connVal) && connVal is bool connBool)
                    {
                        isConnected = connBool;
                    }

                    string name = deviceInformation.Name;
                    if (string.IsNullOrWhiteSpace(name) && deviceInformation.Properties.TryGetValue("System.ItemNameDisplay", out var nameVal) && nameVal != null)
                    {
                        name = nameVal.ToString() ?? "";
                    }

                    string address = "";
                    if (deviceInformation.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out var addrVal) && addrVal != null)
                    {
                        address = addrVal.ToString() ?? "";
                    }

                    // Nếu thiết bị đang ngắt kết nối (IsConnected = false), tránh gọi FromIdAsync gây tạo COM handle & RF traffic
                    if (!isConnected && !string.IsNullOrWhiteSpace(name))
                    {
                        devices.Add(new WirelessDevice
                        {
                            Id = deviceInformation.Id,
                            Name = name,
                            Address = address,
                            IsConnected = false,
                            LastUpdated = DateTime.Now,
                            Source = DeviceSource.BluetoothLE
                        });
                        continue;
                    }

                    // Với thiết bị connected hoặc thông tin chưa đủ, gọi FromIdAsync an toàn
                    using var bleDevice = await BluetoothLEDevice.FromIdAsync(deviceInformation.Id);
                    if (bleDevice is null)
                        continue;

                    devices.Add(new WirelessDevice
                    {
                        Id = bleDevice.DeviceId,
                        Name = string.IsNullOrWhiteSpace(name) ? bleDevice.Name : name,
                        Address = string.IsNullOrWhiteSpace(address) ? bleDevice.BluetoothAddress.ToString() : address,
                        IsConnected = bleDevice.ConnectionStatus == BluetoothConnectionStatus.Connected,
                        LastUpdated = DateTime.Now,
                        Source = DeviceSource.BluetoothLE
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BluetoothLEDiscovery] Handled exception for ID {deviceInformation.Id}: {ex.Message}");
                }
            }

            return devices;
        }
    }
}
