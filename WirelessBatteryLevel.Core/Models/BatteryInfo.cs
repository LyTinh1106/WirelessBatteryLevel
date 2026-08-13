using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WirelessBatteryLevel.Core.Models
{
    public class BatteryInfo
    {
        public int? Level { get; set; }

        public bool IsAvailable { get; set; }

        public string Source { get; set; } = string.Empty;

        public DateTime LastUpdated { get; set; }
    }
}
