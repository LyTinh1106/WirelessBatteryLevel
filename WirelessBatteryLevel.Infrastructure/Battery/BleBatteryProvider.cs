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

            if (!CanHandle(device) || !device.IsConnected)
                return null;

            try
            {
                using var bleDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                if (bleDevice is null || bleDevice.ConnectionStatus != BluetoothConnectionStatus.Connected)
                    return null;

                // Thử Cached mode trước để hạn chế giao tiếp sóng radio
                var servicesResult = await bleDevice.GetGattServicesForUuidAsync(
                    BatteryServiceUuid,
                    BluetoothCacheMode.Cached);

                if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
                {
                    // Phân bổ Uncached mode khi Cached không khả dụng
                    servicesResult = await bleDevice.GetGattServicesForUuidAsync(
                        BatteryServiceUuid,
                        BluetoothCacheMode.Uncached);
                }

                if (servicesResult.Status != GattCommunicationStatus.Success)
                {
                    return null;
                }

                var batteryService = servicesResult.Services.FirstOrDefault();
                if (batteryService is null)
                    return null;

                using (batteryService)
                {
                    var characteristicsResult = await batteryService.GetCharacteristicsForUuidAsync(
                        BatteryLevelCharacteristicUuid,
                        BluetoothCacheMode.Cached);

                    if (characteristicsResult.Status != GattCommunicationStatus.Success || characteristicsResult.Characteristics.Count == 0)
                    {
                        characteristicsResult = await batteryService.GetCharacteristicsForUuidAsync(
                            BatteryLevelCharacteristicUuid,
                            BluetoothCacheMode.Uncached);
                    }

                    if (characteristicsResult.Status != GattCommunicationStatus.Success)
                    {
                        return null;
                    }

                    var characteristic = characteristicsResult.Characteristics.FirstOrDefault();
                    if (characteristic is null)
                        return null;

                    var valueResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Cached);
                    if (valueResult.Status != GattCommunicationStatus.Success)
                    {
                        valueResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                    }

                    if (valueResult.Status != GattCommunicationStatus.Success || valueResult.Value is null)
                    {
                        return null;
                    }

                    var reader = Windows.Storage.Streams.DataReader.FromBuffer(valueResult.Value);
                    if (reader.UnconsumedBufferLength < 1)
                        return null;

                    var batteryLevel = reader.ReadByte();

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
                Debug.WriteLine($"[BleBatteryProvider] Handled COMException for {device.Name}: 0x{ex.HResult:X8}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BleBatteryProvider] Handled Exception for {device.Name}: {ex.Message}");
                return null;
            }
        }
    }
}
