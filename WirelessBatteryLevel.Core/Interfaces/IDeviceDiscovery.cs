using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WirelessBatteryLevel.Core.Models;

namespace WirelessBatteryLevel.Core.Interfaces
{
    public interface IDeviceDiscovery
    {
        Task<IReadOnlyList<WirelessDevice>> DiscoverAsync(
            CancellationToken cancellationToken = default);
    }
}
