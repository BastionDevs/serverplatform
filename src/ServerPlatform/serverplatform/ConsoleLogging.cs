using System;
using System.IO;

namespace serverplatform
{
    internal class ConsoleLogging
    {
        private static readonly object LogLock = new object();

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

            lock (LogLock)
            {
                try
                {
                    LogDirCheck();
                    File.AppendAllText(LogFile, fullMessage + Environment.NewLine);
                }
                catch
                {
                    // Logging must never break an API request.
                }

                try
                {
                    Console.ForegroundColor = color;
                    Console.WriteLine(fullMessage);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                catch
                {
                    // A detached console must not break the backend either.
                }
            }
        }


        private static void LogDirCheck()
        {
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);
        }
    }
}
