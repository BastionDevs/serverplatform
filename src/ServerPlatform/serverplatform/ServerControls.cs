using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace serverplatform
{
    internal class ServerControls
    {
        public static Process ServerProcess;
        public static StringBuilder ConsoleLog = new StringBuilder();

        public static event Action<string> OnConsoleOutput;

        public static void StartServer(string id, string jarPath, string javaArgs)
        {
            if (ServerProcess != null && !ServerProcess.HasExited)
                return; // Already running

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"{javaArgs} -jar \"{jarPath}\" nogui",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            ServerProcess = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            ServerProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    ConsoleLog.AppendLine(e.Data);
                    OnConsoleOutput?.Invoke(e.Data);
                }
            };

            ServerProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    ConsoleLog.AppendLine("[ERROR] " + e.Data);
                    OnConsoleOutput?.Invoke("[ERROR] " + e.Data);
                }
            };

            ServerProcess.Start();
            ServerProcess.BeginOutputReadLine();
            ServerProcess.BeginErrorReadLine();
        }

        public static void SendCommand(string id, string command)
        {
            if (ServerProcess != null && !ServerProcess.HasExited)
            {
                ServerProcess.StandardInput.WriteLine(command);
            }
        }

        public static string GetLatestLog(string id)
        {
            return ConsoleLog.ToString();
        }

        public static void StopServer(string id)
        {

        }
    }
}