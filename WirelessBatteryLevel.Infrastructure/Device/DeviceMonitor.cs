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

            // Bước 0: Phát dữ liệu từ Cache lên UI lập tức nếu có
            var cachedDevices = _stateCache.GetAll();
            if (cachedDevices.Count > 0)
            {
                DevicesUpdated?.Invoke(this, cachedDevices);
            }

            // Chạy 2 Layer song song:
            // Layer 1: Connection Monitor Loop (100ms) - Kiểm tra nhanh IsConnected
            // Layer 2: Battery Polling Loop (interval) - Truy vấn dung lượng pin
            _ = Task.Run(() => FastConnectionMonitorLoopAsync(cancellationToken), cancellationToken);
            _ = Task.Run(() => BatteryPollingLoopAsync(interval, cancellationToken), cancellationToken);

            await Task.CompletedTask;
        }

        public void Stop()
        {
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = null;
        }

        public async Task<IReadOnlyList<DeviceStatus>> ForceRefreshAsync(CancellationToken cancellationToken = default)
        {
            return await RefreshProgressiveAsync(forceBatteryUpdate: true, cancellationToken: cancellationToken);
        }

        private async Task FastConnectionMonitorLoopAsync(CancellationToken cancellationToken)
        {
            using var fastTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));

            try
            {
                while (!cancellationToken.IsCancellationRequested && await fastTimer.WaitForNextTickAsync(cancellationToken))
                {
                    var fastStatuses = await _deviceManager.FastDiscoverAsync(cancellationToken);
                    bool hasChange = false;

                    foreach (var status in fastStatuses)
                    {
                        bool changed = _stateCache.Update(status, forceBatteryUpdate: false);
                        if (changed)
                        {
                            hasChange = true;
                        }
                    }

                    if (hasChange)
                    {
                        var currentAll = _stateCache.GetAll();
                        DevicesUpdated?.Invoke(this, currentAll);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task BatteryPollingLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            try
            {
                // Thực hiện quét ban đầu
                await RefreshProgressiveAsync(forceBatteryUpdate: false, cancellationToken: cancellationToken);

                using var timer = new PeriodicTimer(interval);
                while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await RefreshProgressiveAsync(forceBatteryUpdate: false, cancellationToken: cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task<IReadOnlyList<DeviceStatus>> RefreshProgressiveAsync(bool forceBatteryUpdate, CancellationToken cancellationToken)
        {
            // Layer 1: Quét nhanh danh sách tất cả các thiết bị đã Paired/Connected
            var fastStatuses = await _deviceManager.FastDiscoverAsync(cancellationToken);

            foreach (var status in fastStatuses)
            {
                _stateCache.Update(status, forceBatteryUpdate);
            }

            var currentAll = _stateCache.GetAll();
            DevicesUpdated?.Invoke(this, currentAll);

            // Layer 2: Nạp dữ liệu dung lượng pin dưới nền bất đồng bộ
            var fullStatuses = await _deviceManager.RefreshAsync(cancellationToken);

            foreach (var status in fullStatuses)
            {
                _stateCache.Update(status, forceBatteryUpdate: true);
            }

            var finalAll = _stateCache.GetAll();
            DevicesUpdated?.Invoke(this, finalAll);

            return finalAll;
        }
    }
}
