using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WirelessBatteryLevel.Core.Models
{
    public class WirelessDevice
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Address { get; set; }

        public bool IsConnected { get; set; }

        public DateTime LastUpdated { get; set; }

        public string? DeviceType { get; set; }

        public DeviceSource Source { get; set; }
    }
}
