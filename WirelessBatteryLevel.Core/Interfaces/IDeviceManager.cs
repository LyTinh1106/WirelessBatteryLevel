using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WirelessBatteryLevel.Core.Models;

namespace WirelessBatteryLevel.Core.Interfaces
{
    public interface IDeviceManager
    {
        Task<IReadOnlyList<DeviceStatus>> FastDiscoverAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DeviceStatus>> RefreshAsync(
            CancellationToken cancellationToken = default);
    }
}
