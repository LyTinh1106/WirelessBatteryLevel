using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WirelessBatteryLevel.Core.Models;

namespace WirelessBatteryLevel.Infrastructure.Device
{
    public class DeviceStateCache
    {
        private readonly Dictionary<string, DeviceStatus> _devices =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, DateTime> _lastSeen =
            new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<DeviceStatus> GetAll()
        {
            return _devices.Values.ToList();
        }

        public bool Update(DeviceStatus status, bool forceBatteryUpdate = false)
        {
            var key = GetKey(status);
            _lastSeen[key] = DateTime.Now;

            if (_devices.TryGetValue(key, out var existing))
            {
                bool connectionChanged = existing.Device.IsConnected != status.Device.IsConnected;
                Merge(existing, status, forceBatteryUpdate);
                return connectionChanged;
            }

            _devices[key] = status;
            return true;
        }

        public void Clear()
        {
            _devices.Clear();
            _lastSeen.Clear();
        }

        public bool IsStale(DeviceStatus status, TimeSpan threshold)
        {
            var key = GetKey(status);

            if (!_lastSeen.TryGetValue(
                key,
                out var lastSeen))
            {
                return true;
            }

            return DateTime.Now - lastSeen > threshold;
        }

        private static string GetKey(DeviceStatus status)
        {
            if (!string.IsNullOrWhiteSpace(status.Device.Address))
            {
                return status.Device.Address;
            }

            return status.Device.Id;
        }

        private static void Merge(
            DeviceStatus target,
            DeviceStatus source,
            bool forceBatteryUpdate = false)
        {
            target.Device.Name =
                source.Device.Name;

            target.Device.IsConnected =
                source.Device.IsConnected;

            target.Device.LastUpdated =
                source.Device.LastUpdated;

            if (source.Battery is not null)
            {
                target.Battery = source.Battery;
            }
            else if (!source.Device.IsConnected)
            {
                target.Battery = null;
            }
        }
    }
}
