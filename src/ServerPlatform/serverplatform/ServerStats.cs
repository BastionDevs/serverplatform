using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace serverplatform
{
    public class ServerStats
    {
        private string serversDir;

        public ServerStats()
        {
            serversDir = Config.GetConfig("ServersDir", "main");
        }

        // 1. List of available servers (folder names)
        public List<string> GetAvailableServers()
        {
            if (!Directory.Exists(serversDir))
                return new List<string>();

            return Directory.GetDirectories(serversDir)
                            .Select(Path.GetFileName)
                            .ToList();
        }

        // 2. List of running servers (by name)
        // This assumes you have some way to map server names to running processes.
        // For example, process names match server folder names or you keep a registry.
        public List<string> GetRunningServers()
        {
            var availableServers = GetAvailableServers();
            var runningServers = new List<string>();

            var allProcesses = Process.GetProcesses();

            foreach (var serverName in availableServers)
            {
                // This is a simple example: check if any process name contains serverName
                bool isRunning = allProcesses.Any(p =>
                {
                    try
                    {
                        return p.ProcessName.IndexOf(serverName, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    catch
                    {
                        return false; // Some system processes may throw on access
                    }
                });

                if (isRunning)
                    runningServers.Add(serverName);
            }

            return runningServers;
        }

        // 3. Individual RAM usage (in MB) by server name
        public double GetServerRamUsage(string serverName)
        {
            var process = GetProcessByServerName(serverName);
            if (process == null)
                return 0.0;

            try
            {
                return process.WorkingSet64 / (1024.0 * 1024.0);
            }
            catch
            {
                return 0.0;
            }
        }

        // 4. Individual CPU usage is tricky in .NET Framework 4.7.2 without third party libs.
        // Here's a simple approach using Process.TotalProcessorTime sampled twice:
        public double GetServerCpuUsage(string serverName, int sampleMilliseconds = 500)
        {
            var process = GetProcessByServerName(serverName);
            if (process == null)
                return 0.0;

            try
            {
                var startCpuTime = process.TotalProcessorTime;
                var startTime = DateTime.UtcNow;

                System.Threading.Thread.Sleep(sampleMilliseconds);

                process.Refresh(); // Refresh process info

                var endCpuTime = process.TotalProcessorTime;
                var endTime = DateTime.UtcNow;

                var cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;

                int cpuCount = Environment.ProcessorCount;

                var cpuUsageTotal = (cpuUsedMs / (totalMsPassed * cpuCount)) * 100;

                return Math.Round(cpuUsageTotal, 2);
            }
            catch
            {
                return 0.0;
            }
        }

        // Helper to get process by server name (case insensitive, simple match)
        private Process GetProcessByServerName(string serverName)
        {
            var processes = Process.GetProcesses();

            foreach (var p in processes)
            {
                try
                {
                    if (p.ProcessName.IndexOf(serverName, StringComparison.OrdinalIgnoreCase) >= 0)
                        return p;
                }
                catch
                {
                    // Ignore access exceptions
                }
            }

            return null;
        }

        // Example of additional useful stat: uptime
        public TimeSpan GetServerUptime(string serverName)
        {
            var process = GetProcessByServerName(serverName);
            if (process == null)
                return TimeSpan.Zero;

            try
            {
                return DateTime.Now - process.StartTime;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        // You could add disk usage or network stats similarly with more custom code.
    }
}
