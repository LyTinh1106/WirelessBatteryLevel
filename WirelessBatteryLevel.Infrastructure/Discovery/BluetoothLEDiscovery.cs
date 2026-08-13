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
        public async Task<IReadOnlyList<WirelessDevice>> DiscoverAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var selector =
                BluetoothLEDevice.GetDeviceSelector();

            var deviceInformationCollection =
                await DeviceInformation.FindAllAsync(selector);

            var devices = new List<WirelessDevice>();

            foreach (var deviceInformation in deviceInformationCollection)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var bleDevice =
                        await BluetoothLEDevice.FromIdAsync(
                            deviceInformation.Id);

                    if (bleDevice is null)
                        continue;

                    devices.Add(new WirelessDevice
                    {
                        Id = bleDevice.DeviceId,
                        Name = bleDevice.Name,
                        Address =
                            bleDevice.BluetoothAddress.ToString(),
                        IsConnected =
                            bleDevice.ConnectionStatus ==
                            BluetoothConnectionStatus.Connected,
                        LastUpdated = DateTime.Now,
                        Source = DeviceSource.BluetoothLE
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BluetoothLEDiscovery] Exception while create BluetoothLEDevice from ID {deviceInformation.Id}: {ex.Message}");
                }
            }

            return devices;
        }
    }
}
