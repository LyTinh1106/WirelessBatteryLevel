using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WirelessBatteryLevel.Core.Interfaces;
using WirelessBatteryLevel.Core.Models;

namespace WirelessBatteryLevel.Infrastructure.Device
{
    public class DeviceMonitor
    {
        private readonly IDeviceManager _deviceManager;
        private readonly DeviceStateCache _stateCache;
        private CancellationTokenSource? _monitorCts;

        public event EventHandler<IReadOnlyList<DeviceStatus>>? DevicesUpdated;

        public DeviceMonitor(
            IDeviceManager deviceManager, 
            DeviceStateCache stateCache)
        {
            _deviceManager = deviceManager;
            _stateCache = stateCache;
        }

        public async Task StartAsync(TimeSpan interval)
        {
            if (_monitorCts is not null)
                return;

            _monitorCts = new CancellationTokenSource();
            var cancellationToken = _monitorCts.Token;

            try
            {
                // Bước 0: Phát dữ liệu từ Cache lên UI lập tức nếu có
                var cachedDevices = _stateCache.GetAll();
                if (cachedDevices.Count > 0)
                {
                    DevicesUpdated?.Invoke(this, cachedDevices);
                }

                // Thực hiện nạp 2 giai đoạn (Quét thiết bị lập tức -> Nạp pin dưới nền)
                await RefreshProgressiveAsync(cancellationToken);

                using var timer = new PeriodicTimer(interval);
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await RefreshProgressiveAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Stop()
        {
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = null;
        }

        public async Task<IReadOnlyList<DeviceStatus>> ForceRefreshAsync(CancellationToken cancellationToken = default)
        {
            return await RefreshProgressiveAsync(cancellationToken);
        }

        private async Task<IReadOnlyList<DeviceStatus>> RefreshProgressiveAsync(CancellationToken cancellationToken)
        {
            // Giai đoạn 1: Quét nhanh danh sách tất cả các thiết bị đã Paired/Connected (< 100ms)
            var fastStatuses = await _deviceManager.FastDiscoverAsync(cancellationToken);

            foreach (var status in fastStatuses)
            {
                _stateCache.Update(status);
            }

            // Hiển thị danh sách thiết bị lên UI NGAY LẬP TỨC
            var currentAll = _stateCache.GetAll();
            DevicesUpdated?.Invoke(this, currentAll);

            // Giai đoạn 2: Nạp dữ liệu dung lượng pin dưới nền bất đồng bộ
            var fullStatuses = await _deviceManager.RefreshAsync(cancellationToken);

            foreach (var status in fullStatuses)
            {
                _stateCache.Update(status);
            }

            // Cập nhật lại UI sau khi đã trích xuất xong thông số pin
            var finalAll = _stateCache.GetAll();
            DevicesUpdated?.Invoke(this, finalAll);

            return finalAll;
        }
    }
}
