using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WirelessBatteryLevel.Core.Interfaces;
using WirelessBatteryLevel.Core.Models;

namespace WirelessBatteryLevel.Infrastructure.Discovery
{
    public class DeviceDiscoveryManager
    {
        private readonly IReadOnlyList<IDeviceDiscovery> _discoveries;
        private readonly DeviceAggregator _aggregator;

        public DeviceDiscoveryManager(
            IEnumerable<IDeviceDiscovery> discoveries,
            DeviceAggregator aggregator)
        {
            _discoveries = discoveries.ToList();
            _aggregator = aggregator;
        }

        public async Task<IReadOnlyList<WirelessDevice>> DiscoverAsync(
            CancellationToken cancellationToken = default)
        {
            var tasks = _discoveries.Select(
                discovery =>
                    discovery.DiscoverAsync(cancellationToken));

            var results = await Task.WhenAll(tasks);

            var devices = results
                .SelectMany(result => result);

            return _aggregator.Aggregate(devices);
        }
    }
}
