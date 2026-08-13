using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WirelessBatteryLevel.Core.Interfaces;
using WirelessBatteryLevel.Core.Models;
using WirelessBatteryLevel.Infrastructure.Battery;
using WirelessBatteryLevel.Infrastructure.Discovery;

namespace WirelessBatteryLevel.Infrastructure.Device
{
    public class DeviceManager : IDeviceManager
    {
        private readonly DeviceDiscoveryManager _discoveryManager;
        private readonly BatteryResolver _batteryResolver;

        public DeviceManager(
            DeviceDiscoveryManager discoveryManager,
            BatteryResolver batteryResolver)
        {
            _discoveryManager = discoveryManager;
            _batteryResolver = batteryResolver;
        }

        public async Task<IReadOnlyList<DeviceStatus>> FastDiscoverAsync(
            CancellationToken cancellationToken = default)
        {
            var devices = await _discoveryManager.DiscoverAsync(cancellationToken);

            return devices.Select(device => new DeviceStatus
            {
                Device = device,
                Battery = null
            }).ToList();
        }

        public async Task<IReadOnlyList<DeviceStatus>> RefreshAsync(
            CancellationToken cancellationToken = default)
        {
            var devices =
                await _discoveryManager.DiscoverAsync(
                    cancellationToken);

            var tasks =
                devices.Select(
                    device =>
                        _batteryResolver.GetStatusAsync(
                            device,
                            cancellationToken));

            return await Task.WhenAll(tasks);
        }
    }
}
