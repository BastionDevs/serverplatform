using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace serverplatform
{
    internal sealed class ServerMetricsSnapshot
    {
        public double CpuPercent { get; set; }

        public long MemoryBytes { get; set; }

        public long MemoryMB =>
            MemoryBytes / 1024 / 1024;
    }

    internal class ServerMetrics
    {
        private readonly ServerInstance instance;

        private TimeSpan lastCpuTime;
        private DateTime lastCpuSample;

        public double CpuPercent { get; private set; }
        public long MemoryBytes { get; private set; }

        public ServerMetrics(ServerInstance instance)
        {
            this.instance = instance;

            lastCpuTime = instance.Process.TotalProcessorTime;
            lastCpuSample = DateTime.UtcNow;
        }

        public void Update()
        {
            var process = instance.Process;

            if (process.HasExited)
            {
                CpuPercent = 0;
                MemoryBytes = 0;
                return;
            }

            process.Refresh();

            MemoryBytes = process.WorkingSet64;

            TimeSpan currentCpu = process.TotalProcessorTime;
            DateTime now = DateTime.UtcNow;

            double cpuUsedMs =
                (currentCpu - lastCpuTime).TotalMilliseconds;

            double elapsedMs =
                (now - lastCpuSample).TotalMilliseconds;

            if (elapsedMs > 0)
            {
                CpuPercent =
                    cpuUsedMs /
                    (elapsedMs * Environment.ProcessorCount) *
                    100.0;
            }

            lastCpuTime = currentCpu;
            lastCpuSample = now;
        }

        public ServerMetricsSnapshot GetSnapshot()
        {
            return new ServerMetricsSnapshot
            {
                CpuPercent = CpuPercent,
                MemoryBytes = MemoryBytes
            };
        }
    }
}
