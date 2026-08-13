using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WirelessBatteryLevel.Core.Models;

namespace WirelessBatteryLevel.Infrastructure.Discovery
{
    public class DeviceAggregator
    {
        public IReadOnlyList<WirelessDevice> Aggregate(
            IEnumerable<WirelessDevice> devices)
        {
            var result = new List<WirelessDevice>();

            foreach (var device in devices)
            {
                var existing = FindExistingDevice(
                    result,
                    device);

                if (existing is null)
                {
                    result.Add(device);
                    continue;
                }

                Merge(existing, device);
            }

            return result;
        }

        private static WirelessDevice? FindExistingDevice(
            IEnumerable<WirelessDevice> devices,
            WirelessDevice candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate.Address))
            {
                var addressMatch = devices.FirstOrDefault(
                    device =>
                        !string.IsNullOrWhiteSpace(device.Address) &&
                        string.Equals(
                            device.Address,
                            candidate.Address,
                            StringComparison.OrdinalIgnoreCase));

                if (addressMatch is not null)
                    return addressMatch;
            }

            return devices.FirstOrDefault(
                device =>
                    string.Equals(
                        device.Id,
                        candidate.Id,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static void Merge(
            WirelessDevice target,
            WirelessDevice source)
        {
            if (string.IsNullOrWhiteSpace(target.Name) &&
                !string.IsNullOrWhiteSpace(source.Name))
            {
                target.Name = source.Name;
            }

            if (!target.IsConnected &&
                source.IsConnected)
            {
                target.IsConnected = true;
            }

            if (source.LastUpdated >
                target.LastUpdated)
            {
                target.LastUpdated =
                    source.LastUpdated;
            }
        }
    }
}
