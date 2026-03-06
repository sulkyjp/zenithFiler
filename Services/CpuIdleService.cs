using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ZenithFiler.Services
{
    /// <summary>CPU 使用率を定期サンプリングし、アイドル状態を判定するサービス。</summary>
    public class CpuIdleService : IDisposable
    {
        private readonly PerformanceCounter _cpuCounter;
        private readonly System.Timers.Timer _sampleTimer;
        private float _lastCpuUsage = 100f;

        public CpuIdleService()
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // warmup（初回は常に 0 を返す）
            _sampleTimer = new System.Timers.Timer(5000);
            _sampleTimer.Elapsed += (_, _) =>
            {
                try { _lastCpuUsage = _cpuCounter.NextValue(); }
                catch { /* カウンター読み取り失敗は無視 */ }
            };
            _sampleTimer.Start();
        }

        /// <summary>CPU 使用率が閾値以下かを返す。</summary>
        public bool IsIdle(float threshold) => _lastCpuUsage <= threshold;

        /// <summary>CPU 使用率が閾値以下になるまで待機する。</summary>
        public async Task WaitForIdleAsync(float threshold, CancellationToken ct)
        {
            while (!IsIdle(threshold) && !ct.IsCancellationRequested)
                await Task.Delay(10_000, ct).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _sampleTimer.Stop();
            _sampleTimer.Dispose();
            _cpuCounter.Dispose();
        }
    }
}
