using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using WirelessBatteryLevel.Core.Interfaces;
using WirelessBatteryLevel.Core.Models;

namespace WirelessBatteryLevel.Infrastructure.Battery
{
    public class ClassicBatteryProvider : IBatteryProvider
    {
        // PnP Property Key chính xác cho Dung lượng Pin (DEVPKEY_Device_BatteryLevel)
        private static readonly string PnpBatteryLevelKey =
            "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

        private static readonly string ItemNameDisplayKey =
            "System.ItemNameDisplay";

        private static readonly string AepAddressKey =
            "System.Devices.Aep.DeviceAddress";

        private static readonly string ContainerIdKey =
            "System.Devices.ContainerId";

        public bool CanHandle(WirelessDevice device)
        {
            return device.Source == DeviceSource.ClassicBluetooth;
        }

        public async Task<BatteryInfo?> GetBatteryAsync(
            WirelessDevice device,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!CanHandle(device))
                return null;

            var requestedProperties = new[]
            {
                PnpBatteryLevelKey,
                ItemNameDisplayKey,
                AepAddressKey,
                ContainerIdKey
            };

            // 1. Quét theo AssociationEndpoint (AEP - Nút Bluetooth chính)
            var aepBattery = await QueryBatteryByKindAsync(
                device,
                DeviceInformationKind.AssociationEndpoint,
                requestedProperties,
                "AEP",
                cancellationToken);

            if (aepBattery is not null)
                return aepBattery;

            // 2. Quét theo DeviceContainer (Nút Container chứa thiết bị trong Windows Settings)
            var containerBattery = await QueryBatteryByKindAsync(
                device,
                DeviceInformationKind.DeviceContainer,
                requestedProperties,
                "DeviceContainer",
                cancellationToken);

            if (containerBattery is not null)
                return containerBattery;

            // 3. Quét theo Device (Nút thiết bị hệ thống PnP Node)
            var deviceKindBattery = await QueryBatteryByKindAsync(
                device,
                DeviceInformationKind.Device,
                requestedProperties,
                "DeviceNode",
                cancellationToken);

            if (deviceKindBattery is not null)
                return deviceKindBattery;

            Debug.WriteLine(
                $"[ClassicBatteryProvider] Không lấy được dung lượng pin cho " +
                $"thiết bị Classic Bluetooth: {device.Name} ({device.Address})");

            return null;
        }

        private async Task<BatteryInfo?> QueryBatteryByKindAsync(
            WirelessDevice device,
            DeviceInformationKind kind,
            string[] requestedProperties,
            string sourceLabel,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var devices = await DeviceInformation.FindAllAsync(
                    "",
                    requestedProperties,
                    kind);

                cancellationToken.ThrowIfCancellationRequested();

                foreach (var devInfo in devices)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!IsDeviceMatch(devInfo, device))
                        continue;

                    if (TryExtractBatteryLevel(devInfo, out var batteryLevel))
                    {
                        //Debug.WriteLine(
                        //    $"[ClassicBatteryProvider] Đã lấy pin thành công từ " +
                        //    $"{sourceLabel} cho {device.Name}: {batteryLevel}%");

                        return new BatteryInfo
                        {
                            Level = batteryLevel,
                            IsAvailable = true,
                            Source = $"ClassicBluetooth-{sourceLabel}",
                            LastUpdated = DateTime.Now
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"[ClassicBatteryProvider] Truy vấn qua {sourceLabel} " +
                    $"lỗi cho {device.Name}: {ex.Message}");
            }

            return null;
        }

        private static bool TryExtractBatteryLevel(
            DeviceInformation devInfo, out byte level)
        {
            level = 0;

            if (devInfo.Properties == null)
                return false;

            // 1. Thử lấy từ PnpBatteryLevelKey chính xác ({104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2)
            if (devInfo.Properties.TryGetValue(PnpBatteryLevelKey, out var rawVal) &&
                rawVal is not null)
            {
                if (TryParseBatteryByte(rawVal, out level))
                    return true;
            }

            // 2. Duyệt tìm các thuộc tính chứa GUID hoặc giá trị pin trong dictionary nếu có
            foreach (var kvp in devInfo.Properties)
            {
                if (kvp.Value is null)
                    continue;

                var keyUpper = kvp.Key.ToUpperInvariant();
                if (keyUpper.Contains("104EA319") || keyUpper.Contains("BATTERY"))
                {
                    if (TryParseBatteryByte(kvp.Value, out level))
                        return true;
                }
            }

            return false;
        }

        private static bool TryParseBatteryByte(object rawValue, out byte level)
        {
            level = 0;
            try
            {
                if (rawValue is byte bLevel && bLevel <= 100)
                {
                    level = bLevel;
                    return true;
                }

                if (rawValue is byte[] byteArray &&
                    byteArray.Length > 0 &&
                    byteArray[0] <= 100)
                {
                    level = byteArray[0];
                    return true;
                }

                var converted = Convert.ToByte(rawValue);
                if (converted <= 100)
                {
                    level = converted;
                    return true;
                }
            }
            catch
            {
                // Bỏ qua lỗi ép kiểu
            }

            return false;
        }

        private static bool IsDeviceMatch(
            DeviceInformation devInfo, WirelessDevice device)
        {
            // 1. So khớp theo ID (Exact hoặc Substring)
            if (!string.IsNullOrWhiteSpace(devInfo.Id))
            {
                if (string.Equals(devInfo.Id, device.Id, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(device.Id) && devInfo.Id.Contains(device.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            // 2. So khớp theo Tên thiết bị (DevInfo.Name hoặc ItemNameDisplay)
            if (!string.IsNullOrWhiteSpace(device.Name))
            {
                if (!string.IsNullOrWhiteSpace(devInfo.Name))
                {
                    if (string.Equals(devInfo.Name, device.Name, StringComparison.OrdinalIgnoreCase) ||
                        devInfo.Name.Contains(device.Name, StringComparison.OrdinalIgnoreCase) ||
                        device.Name.Contains(devInfo.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (devInfo.Properties.TryGetValue(ItemNameDisplayKey, out var displayNameObj) &&
                    displayNameObj is not null)
                {
                    var displayName = displayNameObj.ToString();
                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        if (string.Equals(displayName, device.Name, StringComparison.OrdinalIgnoreCase) ||
                            displayName.Contains(device.Name, StringComparison.OrdinalIgnoreCase) ||
                            device.Name.Contains(displayName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            // 3. So khớp theo Địa chỉ Bluetooth (Chuẩn hóa nhiều định dạng)
            if (!string.IsNullOrWhiteSpace(device.Address))
            {
                var addressFormats = GetAddressFormats(device.Address);

                // Kiểm tra xem devInfo.Id có chứa bất kỳ định dạng địa chỉ MAC nào không
                if (!string.IsNullOrWhiteSpace(devInfo.Id))
                {
                    foreach (var fmt in addressFormats)
                    {
                        if (devInfo.Id.Contains(fmt, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }

                // Kiểm tra xem System.Devices.Aep.DeviceAddress có khớp không
                if (devInfo.Properties.TryGetValue(AepAddressKey, out var aepAddrObj) &&
                    aepAddrObj is not null)
                {
                    var aepAddrStr = aepAddrObj.ToString();
                    if (!string.IsNullOrWhiteSpace(aepAddrStr))
                    {
                        foreach (var fmt in addressFormats)
                        {
                            if (aepAddrStr.Contains(fmt, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private static List<string> GetAddressFormats(string rawAddress)
        {
            var formats = new List<string> { rawAddress };

            // Nếu là chuỗi số thập phân ulong (ví dụ: 277459096578711)
            if (ulong.TryParse(rawAddress, out var addressNum))
            {
                var hex = addressNum.ToString("X12");
                formats.Add(hex); // FC58FA012345

                // Tạo định dạng MAC chuẩn FC:58:FA:01:23:45
                if (hex.Length == 12)
                {
                    var macWithColons = string.Join(":",
                        Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
                    formats.Add(macWithColons);
                }
            }
            else
            {
                // Nếu chuỗi là Hex có hoặc không có dấu :
                var cleanHex = new string(rawAddress.Where(char.IsLetterOrDigit).ToArray());
                if (!string.IsNullOrEmpty(cleanHex))
                {
                    formats.Add(cleanHex);
                    if (ulong.TryParse(cleanHex, System.Globalization.NumberStyles.HexNumber, null, out var parsedNum))
                    {
                        formats.Add(parsedNum.ToString());
                    }
                }
            }

            return formats.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
