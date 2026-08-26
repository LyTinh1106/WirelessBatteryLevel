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
            var (aepBattery, containerId) = await QueryAepBatteryAsync(device, requestedProperties, cancellationToken);
            if (aepBattery is not null)
                return aepBattery;

            // 2. Quét theo DeviceContainer (Nút Container chứa thiết bị trong Windows Settings)
            var containerBattery = await QueryContainerBatteryAsync(device, containerId, requestedProperties, cancellationToken);
            if (containerBattery is not null)
                return containerBattery;

            // 3. Quét theo Device (Nút thiết bị hệ thống PnP Node)
            var deviceKindBattery = await QueryDeviceNodeBatteryAsync(device, containerId, requestedProperties, cancellationToken);
            if (deviceKindBattery is not null)
                return deviceKindBattery;

            Debug.WriteLine(
                $"[ClassicBatteryProvider] Không lấy được dung lượng pin cho " +
                $"thiết bị Classic Bluetooth: {device.Name} ({device.Address})");

            return null;
        }

        private async Task<(BatteryInfo? Battery, Guid? ContainerId)> QueryAepBatteryAsync(
            WirelessDevice device,
            string[] requestedProperties,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 1. Direct targeted lookup by Device ID (AEP ID)
                if (!string.IsNullOrWhiteSpace(device.Id))
                {
                    try
                    {
                        var singleDevInfo = await DeviceInformation.CreateFromIdAsync(
                            device.Id, requestedProperties, DeviceInformationKind.AssociationEndpoint);
                        if (singleDevInfo is not null)
                        {
                            var cid = ExtractContainerId(singleDevInfo);
                            if (TryExtractBatteryLevel(singleDevInfo, out var batteryLevel))
                            {
                                return (new BatteryInfo
                                {
                                    Level = batteryLevel,
                                    IsAvailable = true,
                                    Source = "ClassicBluetooth-AEP",
                                    LastUpdated = DateTime.Now
                                }, cid);
                            }
                            return (null, cid);
                        }
                    }
                    catch
                    {
                    }
                }

                // 2. Targeted AQS query by MAC / Name
                string aqsFilter = BuildAepAqsFilter(device);
                var devices = await DeviceInformation.FindAllAsync(
                    aqsFilter, requestedProperties, DeviceInformationKind.AssociationEndpoint);

                cancellationToken.ThrowIfCancellationRequested();

                foreach (var devInfo in devices)
                {
                    if (IsDeviceMatch(devInfo, device))
                    {
                        var cid = ExtractContainerId(devInfo);
                        if (TryExtractBatteryLevel(devInfo, out var batteryLevel))
                        {
                            return (new BatteryInfo
                            {
                                Level = batteryLevel,
                                IsAvailable = true,
                                Source = "ClassicBluetooth-AEP",
                                LastUpdated = DateTime.Now
                            }, cid);
                        }
                        return (null, cid);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClassicBatteryProvider] Query AEP error for {device.Name}: {ex.Message}");
            }

            return (null, null);
        }

        private async Task<BatteryInfo?> QueryContainerBatteryAsync(
            WirelessDevice device,
            Guid? containerId,
            string[] requestedProperties,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 1. Direct lookup by ContainerId GUID if available
                if (containerId.HasValue && containerId.Value != Guid.Empty)
                {
                    try
                    {
                        var containerIdStr = $"{{{containerId.Value}}}";
                        var singleDevInfo = await DeviceInformation.CreateFromIdAsync(
                            containerIdStr, requestedProperties, DeviceInformationKind.DeviceContainer);
                        if (singleDevInfo is not null && TryExtractBatteryLevel(singleDevInfo, out var batteryLevel))
                        {
                            return new BatteryInfo
                            {
                                Level = batteryLevel,
                                IsAvailable = true,
                                Source = "ClassicBluetooth-DeviceContainer",
                                LastUpdated = DateTime.Now
                            };
                        }
                    }
                    catch
                    {
                    }
                }

                // 2. Targeted AQS query by ContainerId or Name
                string aqsFilter = BuildContainerAqsFilter(device, containerId);
                if (!string.IsNullOrEmpty(aqsFilter))
                {
                    var devices = await DeviceInformation.FindAllAsync(
                        aqsFilter, requestedProperties, DeviceInformationKind.DeviceContainer);

                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var devInfo in devices)
                    {
                        if (IsDeviceMatch(devInfo, device))
                        {
                            if (TryExtractBatteryLevel(devInfo, out var batteryLevel))
                            {
                                return new BatteryInfo
                                {
                                    Level = batteryLevel,
                                    IsAvailable = true,
                                    Source = "ClassicBluetooth-DeviceContainer",
                                    LastUpdated = DateTime.Now
                                };
                            }
                        }
                    }
                }

                // 3. Fallback: Full scan if targeted query yielded no results
                var fallbackDevices = await DeviceInformation.FindAllAsync(
                    "", requestedProperties, DeviceInformationKind.DeviceContainer);
                foreach (var devInfo in fallbackDevices)
                {
                    if (IsDeviceMatch(devInfo, device) && TryExtractBatteryLevel(devInfo, out var batteryLevel))
                    {
                        return new BatteryInfo
                        {
                            Level = batteryLevel,
                            IsAvailable = true,
                            Source = "ClassicBluetooth-DeviceContainer-Fallback",
                            LastUpdated = DateTime.Now
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClassicBatteryProvider] Query Container error for {device.Name}: {ex.Message}");
            }

            return null;
        }

        private async Task<BatteryInfo?> QueryDeviceNodeBatteryAsync(
            WirelessDevice device,
            Guid? containerId,
            string[] requestedProperties,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 1. Targeted AQS query by ContainerId or Name for PnP Device Nodes
                string aqsFilter = BuildDeviceNodeAqsFilter(device, containerId);
                if (!string.IsNullOrEmpty(aqsFilter))
                {
                    var devices = await DeviceInformation.FindAllAsync(
                        aqsFilter, requestedProperties, DeviceInformationKind.Device);

                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var devInfo in devices)
                    {
                        if (IsDeviceMatch(devInfo, device))
                        {
                            if (TryExtractBatteryLevel(devInfo, out var batteryLevel))
                            {
                                return new BatteryInfo
                                {
                                    Level = batteryLevel,
                                    IsAvailable = true,
                                    Source = "ClassicBluetooth-DeviceNode",
                                    LastUpdated = DateTime.Now
                                };
                            }
                        }
                    }
                }

                // 2. Fallback: Full scan if targeted query returned empty
                var fallbackDevices = await DeviceInformation.FindAllAsync(
                    "", requestedProperties, DeviceInformationKind.Device);
                foreach (var devInfo in fallbackDevices)
                {
                    if (IsDeviceMatch(devInfo, device) && TryExtractBatteryLevel(devInfo, out var batteryLevel))
                    {
                        return new BatteryInfo
                        {
                            Level = batteryLevel,
                            IsAvailable = true,
                            Source = "ClassicBluetooth-DeviceNode-Fallback",
                            LastUpdated = DateTime.Now
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClassicBatteryProvider] Query DeviceNode error for {device.Name}: {ex.Message}");
            }

            return null;
        }

        private static string BuildAepAqsFilter(WirelessDevice device)
        {
            var filters = new List<string>();

            if (!string.IsNullOrWhiteSpace(device.Address))
            {
                var addressFormats = GetAddressFormats(device.Address);
                foreach (var fmt in addressFormats)
                {
                    if (fmt.Contains(":"))
                    {
                        filters.Add($"System.Devices.Aep.DeviceAddress:=\"{fmt}\"");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(device.Name))
            {
                filters.Add($"System.ItemNameDisplay:=\"{device.Name}\"");
            }

            return filters.Count > 0 ? string.Join(" OR ", filters) : "";
        }

        private static string BuildContainerAqsFilter(WirelessDevice device, Guid? containerId)
        {
            if (containerId.HasValue && containerId.Value != Guid.Empty)
            {
                return $"System.Devices.ContainerId:=\"{{{containerId.Value}}}\"";
            }

            if (!string.IsNullOrWhiteSpace(device.Name))
            {
                return $"System.ItemNameDisplay:=\"{device.Name}\"";
            }

            return "";
        }

        private static string BuildDeviceNodeAqsFilter(WirelessDevice device, Guid? containerId)
        {
            if (containerId.HasValue && containerId.Value != Guid.Empty)
            {
                return $"System.Devices.ContainerId:=\"{{{containerId.Value}}}\"";
            }

            if (!string.IsNullOrWhiteSpace(device.Name))
            {
                return $"System.ItemNameDisplay:=\"{device.Name}\"";
            }

            return "";
        }

        private static Guid? ExtractContainerId(DeviceInformation devInfo)
        {
            if (devInfo.Properties != null &&
                devInfo.Properties.TryGetValue(ContainerIdKey, out var val) &&
                val is Guid g && g != Guid.Empty)
            {
                return g;
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
            var formats = new List<string>(4) { rawAddress };

            // Nếu là chuỗi số thập phân ulong (ví dụ: 277459096578711)
            if (ulong.TryParse(rawAddress, out var addressNum))
            {
                var hex = addressNum.ToString("X12");
                formats.Add(hex); // FC58FA012345

                // Tạo định dạng MAC chuẩn FC:58:FA:01:23:45
                if (hex.Length == 12)
                {
                    var macWithColons = $"{hex[0]}{hex[1]}:{hex[2]}{hex[3]}:{hex[4]}{hex[5]}:{hex[6]}{hex[7]}:{hex[8]}{hex[9]}:{hex[10]}{hex[11]}";
                    formats.Add(macWithColons);
                }
            }
            else
            {
                // Nếu chuỗi là Hex có hoặc không có dấu :
                var sb = new StringBuilder(rawAddress.Length);
                foreach (var c in rawAddress)
                {
                    if (char.IsLetterOrDigit(c))
                        sb.Append(c);
                }
                var cleanHex = sb.ToString();
                if (!string.IsNullOrEmpty(cleanHex))
                {
                    formats.Add(cleanHex);
                    if (ulong.TryParse(cleanHex, System.Globalization.NumberStyles.HexNumber, null, out var parsedNum))
                    {
                        formats.Add(parsedNum.ToString());
                    }
                }
            }

            return formats;
        }
    }
}
