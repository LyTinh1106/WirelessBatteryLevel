using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using WirelessBatteryLevel.Core.Interfaces;
using WirelessBatteryLevel.Core.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WirelessBatteryLevel.Infrastructure.Battery
{
    public class BleBatteryProvider : IBatteryProvider
    {
        private static readonly Guid BatteryServiceUuid =
            new("0000180F-0000-1000-8000-00805F9B34FB");

        private static readonly Guid BatteryLevelCharacteristicUuid =
            new("00002A19-0000-1000-8000-00805F9B34FB");

        public bool CanHandle(WirelessDevice device)
        {
            return device.Source == DeviceSource.BluetoothLE;
        }

        public async Task<BatteryInfo?> GetBatteryAsync(
            WirelessDevice device,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!CanHandle(device))
                return null;

            var bleDevice =
                await BluetoothLEDevice.FromIdAsync(device.Id);

            if (bleDevice is null)
                return null;

            using (bleDevice)
            {
                try
                {
                    var servicesResult =
                    await bleDevice.GetGattServicesForUuidAsync(
                        BatteryServiceUuid,
                        BluetoothCacheMode.Uncached);

                    if (servicesResult.Status !=
                        GattCommunicationStatus.Success)
                    {
                        Debug.WriteLine($"GATT service discovery failed. " + $"Status: {servicesResult.Status}");
                        return null;
                    }

                    var batteryService =
                        servicesResult.Services.FirstOrDefault();

                    if (batteryService is null)
                        return null;

                    using (batteryService)
                    {
                        var characteristicsResult =
                            await batteryService
                                .GetCharacteristicsForUuidAsync(
                                    BatteryLevelCharacteristicUuid,
                                    BluetoothCacheMode.Uncached);

                        if (characteristicsResult.Status !=
                            GattCommunicationStatus.Success)
                        {
                            return null;
                        }

                        var characteristic =
                            characteristicsResult.Characteristics
                                .FirstOrDefault();

                        if (characteristic is null)
                            return null;

                        var valueResult =
                            await characteristic.ReadValueAsync(
                                BluetoothCacheMode.Uncached);

                        if (valueResult.Status !=
                            GattCommunicationStatus.Success)
                        {
                            return null;
                        }

                        var reader =
                            Windows.Storage.Streams.DataReader
                                .FromBuffer(valueResult.Value);

                        if (reader.UnconsumedBufferLength < 1)
                            return null;

                        var batteryLevel =
                            reader.ReadByte();

                        return new BatteryInfo
                        {
                            Level = batteryLevel,
                            IsAvailable = true,
                            Source = "BLE",
                            LastUpdated = DateTime.Now
                        };
                    }
                }
                catch (COMException ex)
                {
                    Debug.WriteLine(
                        $"BLE GATT COMException");

                    Debug.WriteLine(
                        $"Device: {device.Name}");

                    Debug.WriteLine(
                        $"ID: {device.Id}");

                    Debug.WriteLine(
                        $"HRESULT: 0x{ex.HResult:X8}");

                    Debug.WriteLine(
                        $"Message: {ex.Message}");

                    return null;
                }
            }
        }
    }
}
