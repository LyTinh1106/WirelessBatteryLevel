using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WirelessBatteryLevel.Core.Models;

namespace WirelessBatteryLevel.Core.Interfaces
{
    public interface IBatteryProvider
    {
        bool CanHandle(WirelessDevice device);

        Task<BatteryInfo?> GetBatteryAsync(
            WirelessDevice device,
            CancellationToken cancellationToken = default);
    }
}
