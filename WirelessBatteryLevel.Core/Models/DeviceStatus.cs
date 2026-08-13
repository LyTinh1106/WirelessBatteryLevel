using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WirelessBatteryLevel.Core.Models
{
    public class DeviceStatus
    {
        public WirelessDevice Device { get; set; } = new();
        public BatteryInfo? Battery { get; set; }
    }
}
