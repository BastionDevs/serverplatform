using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TinyINIController;

namespace serverplatform
{
    internal class ServerControls
    {
        private static Dictionary<string, Process> servers = new Dictionary<string, Process>();
        private static Dictionary<string, StringBuilder> logs = new Dictionary<string, StringBuilder>();

        public static event Action<string, string> OnConsoleOutput;

        static string serversDirectory = Config.GetConfig("ServersDir", "main");
        static string runtimesdir = Path.Combine(AppContext.BaseDirectory, "JavaRuntimes");

        public static void StartServer(string id)
        {
            IniFile srvConfig = new IniFile($@"{serversDirectory}\{id}\srvconfig.ini");
            string srvName = srvConfig.Read("name", "info");

            string javaVendor = srvConfig.Read("vendor", "java");
            string javaVer = srvConfig.Read("ver", "java");
            string javaType = srvConfig.Read("type", "java");

            string minRam = srvConfig.Read("minRam", "java");
            string maxRam = srvConfig.Read("maxRam", "java");

            if (servers.ContainsKey(id) && !servers[id].HasExited)
                return; // Already running

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = $"{runtimesdir}\\{javaVendor}{javaType}{javaVer}\\bin\\java.exe",
                Arguments = $"-Xms {minRam} -Xmx {maxRam}" + File.ReadAllText($"{serversDirectory}\\{id}\\minecraft.launch"),
                WorkingDirectory = $"{serversDirectory}\\id\\files",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            Process serverProcess = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            StringBuilder consoleLog = new StringBuilder();
            logs[id] = consoleLog;
            servers[id] = serverProcess;

            serverProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    consoleLog.AppendLine(e.Data);
                    OnConsoleOutput?.Invoke(id, e.Data);
                }
            };

            serverProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    consoleLog.AppendLine("[ERROR] " + e.Data);
                    OnConsoleOutput?.Invoke(id, "[ERROR] " + e.Data);
                }
            };

            serverProcess.Start();
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();
        }

        public static async Task StopServer(string id)
        {
            if (servers.TryGetValue(id, out var process) && !process.HasExited)
            {
                try
                {
                    // Send "stop" command
                    process.StandardInput.WriteLine("stop");
                    process.StandardInput.Flush();

                    // Wait for graceful shutdown (e.g., 30 seconds max)
                    await Task.Run(() => process.WaitForExit(30000));

                    if (!process.HasExited)
                    {
                        // Force kill as fallback
                        process.Kill();
                    }
                }
                catch
                {
                    process.Kill(); // Ensure cleanup on exception
                }
                finally
                {
                    process.Dispose();
                    servers.Remove(id);
                    logs.Remove(id);
                }
            }
        }

        public static async Task RestartServer(string id)
        {
            if (servers.TryGetValue(id, out var process) && !process.HasExited)
            {
                await StopServer(id);
                StartServer(id);
            }
        }

        public static void SendCommand(string id, string command)
        {
            if (servers.TryGetValue(id, out var process) && !process.HasExited)
            {
                process.StandardInput.WriteLine(command);
                process.StandardInput.Flush();
            }
        }

        public static string GetLog(string id)
        {
            if (logs.TryGetValue(id, out var log))
                return log.ToString();

            return null;
        }

        public static bool IsServerRunning(string id)
        {
            return servers.TryGetValue(id, out var process) && !process.HasExited;
        }
    }
}