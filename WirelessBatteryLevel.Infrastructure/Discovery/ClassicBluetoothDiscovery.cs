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
    public class ClassicBluetoothDiscovery : IDeviceDiscovery
    {
        public async Task<IReadOnlyList<WirelessDevice>> DiscoverAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var selector =
                BluetoothDevice.GetDeviceSelector();

            var deviceInformationCollection =
                await DeviceInformation.FindAllAsync(selector);

            var devices = new List<WirelessDevice>();

            foreach (var deviceInformation in deviceInformationCollection)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var bluetoothDevice =
                        await BluetoothDevice.FromIdAsync(
                            deviceInformation.Id);

                    if (bluetoothDevice is null)
                        continue;

                    devices.Add(new WirelessDevice
                    {
                        Id = bluetoothDevice.DeviceId,
                        Name = bluetoothDevice.Name,
                        Address =
                            bluetoothDevice.BluetoothAddress.ToString(),
                        IsConnected =
                            bluetoothDevice.ConnectionStatus ==
                            BluetoothConnectionStatus.Connected,
                        LastUpdated = DateTime.Now,
                        Source = DeviceSource.ClassicBluetooth
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ClassicBluetoothDiscovery] Exception while create BluetoothDevice from ID {deviceInformation.Id}: {ex.Message}");
                }
            }

            return devices;
        }
    }
}
