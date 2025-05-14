using System;
using System.IO;

namespace serverplatform
{
    internal class ConsoleLogging
    {
        static string logDir = Path.Combine(Environment.CurrentDirectory, "logs");
        static string logFile = Path.Combine(logDir, $"{DateTime.Now:yyyy-dd-MMM-hh-mm-tt}".ToLower() + ".log");

        public static void LogError(string message, string component = null)
            => WriteLog(message, component, "ERROR", ConsoleColor.Red);

        public static void LogWarning(string message, string component = null)
            => WriteLog(message, component, "WARN", ConsoleColor.Yellow);

        public static void LogSuccess(string message, string component = null)
            => WriteLog(message, component, "SUCCESS", ConsoleColor.Green);

        public static void LogMessage(string message, string component = null)
            => WriteLog(message, component, null, Console.ForegroundColor);

        public static void ClearLogFolder(bool confirm)
        {
            if (confirm)
            {
                if (Directory.Exists(logDir))
                    Directory.Delete(logDir, true);
            }
            else
            {
                LogWarning("User almost deleted all the logs!!", "Logging");
            }
        }

        // Internal logging logic
        private static void WriteLog(string message, string component, string level, ConsoleColor color)
        {
            LogDirCheck();
            string timestamp = $"[{DateTime.Now:HH:mm:ss}]";
            string prefix = level != null
                ? $"[{(component != null ? $"{component} - " : "")}{level}]"
                : component != null
                    ? $"[{component}]"
                    : "";

            string fullMessage = $"{timestamp} {prefix} {message}".Trim();

            using (var sw = new StreamWriter(logFile, true))
            {
                Console.ForegroundColor = color;
                sw.WriteLine(fullMessage);
                Console.WriteLine(fullMessage);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        private static void LogDirCheck()
        {
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
        }
    }
}
