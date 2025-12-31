using System;
using System.IO;

namespace serverplatform
{
    internal class ConsoleLogging
    {
        private static readonly string LogDir = Path.Combine(Environment.CurrentDirectory, "logs");

        private static readonly string LogFile =
            Path.Combine(LogDir, $"{DateTime.Now:yyyy-dd-MMM-hh-mm-tt}".ToLower() + ".log");

        public static void LogError(string message, string component = null)
        {
            WriteLog(message, component, "ERROR", ConsoleColor.Red);
        }

        public static void LogWarning(string message, string component = null)
        {
            WriteLog(message, component, "WARN", ConsoleColor.Yellow);
        }

        public static void LogSuccess(string message, string component = null)
        {
            WriteLog(message, component, "SUCCESS", ConsoleColor.Green);
        }

        public static void LogMessage(string message, string component = null)
        {
            WriteLog(message, component, null, Console.ForegroundColor);
        }

        public static void ClearLogFolder(bool confirm)
        {
            if (confirm)
            {
                if (Directory.Exists(LogDir))
                    Directory.Delete(LogDir, true);
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

            var timestamp = $"[{DateTime.Now:HH:mm:ss}]";

            string prefix = null;

            if (!string.IsNullOrEmpty(level))
            {
                prefix = component != null
                    ? $"[{component} - {level}]"
                    : $"[{level}]";
            }
            else if (!string.IsNullOrEmpty(component))
            {
                prefix = $"[{component}]";
            }

            var fullMessage = prefix != null
                ? $"{timestamp} {prefix} {message}"
                : $"{timestamp} {message}";

            using (var sw = new StreamWriter(LogFile, true))
            {
                Console.ForegroundColor = color;
                sw.WriteLine(fullMessage);
                Console.WriteLine(fullMessage);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }


        private static void LogDirCheck()
        {
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);
        }
    }
}