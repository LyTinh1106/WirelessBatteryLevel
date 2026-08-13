using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WirelessBatteryLevel.Core.Interfaces;
using WirelessBatteryLevel.Core.Models;

namespace WirelessBatteryLevel.Infrastructure.Battery
{
    public class BatteryResolver
    {
        private readonly IReadOnlyList<IBatteryProvider> _providers;

        public BatteryResolver(
            IEnumerable<IBatteryProvider> providers)
        {
            _providers = providers.ToList();
        }

        public async Task<BatteryInfo?> GetBatteryAsync(
            WirelessDevice device,
            CancellationToken cancellationToken = default)
        {
            var provider = _providers.FirstOrDefault(
                provider => provider.CanHandle(device));

            if (provider is null)
                return null;

            return await provider.GetBatteryAsync(
                device,
                cancellationToken);
        }

        public async Task<DeviceStatus> GetStatusAsync(
            WirelessDevice device,
            CancellationToken cancellationToken = default)
        {
            var battery =
                await GetBatteryAsync(
                    device,
                    cancellationToken);

            return new DeviceStatus
            {
                Device = device,
                Battery = battery
            };
        }
    }
}
