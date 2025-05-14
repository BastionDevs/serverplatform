using System;
using System.IO;


namespace serverplatform
{
    internal class ConsoleLogging
    {
        static string logDir = Environment.CurrentDirectory + @"\logs";
        static string logFile = logDir + $@"\{DateTime.Now.ToString("yyyy-dd-MMM-hh-mm-tt").ToLower()}.log";

        public static void LogError(string message)
        {
            LogDirCheck();
            var sw = new StreamWriter(logFile, true);
            Console.ForegroundColor = ConsoleColor.Red;
            sw.WriteLine($"[ERROR] {message}");
            Console.WriteLine($"[ERROR] {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
            sw.Close();
        }

        public static void LogWarning(string message) 
        {
            LogDirCheck();
            var sw = new StreamWriter(logFile, true);
            Console.ForegroundColor = ConsoleColor.Yellow;
            sw.WriteLine($"[WARN] {message}");
            Console.WriteLine($"[WARN] {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
            sw.Close();
        }

        public static void LogMessage(string message)
        {
            LogDirCheck();
            var sw = new StreamWriter(logFile, true);
            sw.WriteLine($"{message}");
            Console.WriteLine($"{message}");
            sw.Close();
        }

        public static void LogError(string message, string component)
        {
            LogDirCheck();
            var sw = new StreamWriter(logFile, true);
            Console.ForegroundColor = ConsoleColor.Red;
            sw.WriteLine($"[{component} - ERROR] {message}");
            Console.WriteLine($"[{component} - ERROR] {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
            sw.Close();
        }

        public static void LogWarning(string message, string component)
        {
            LogDirCheck();
            var sw = new StreamWriter(logFile, true);
            Console.ForegroundColor = ConsoleColor.Yellow;
            sw.WriteLine($"[{component} - WARN] {message}");
            Console.WriteLine($"[{component} - WARN] {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
            sw.Close();
        }

        public static void LogSuccess(string message)
        {
            LogDirCheck();
            var sw = new StreamWriter(logFile, true);
            Console.ForegroundColor = ConsoleColor.Green;
            sw.WriteLine($"[SUCCESS] {message}");
            Console.WriteLine($"[SUCCESS] {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
            sw.Close();
        }

        
        public static void LogMessage(string message, string component)
        {
            LogDirCheck();
            var sw = new StreamWriter(logFile, true);
            sw.WriteLine($"[{component}] {message}");
            Console.WriteLine($"[{component}] {message}");
            sw.Close();
        }

        static void LogDirCheck()
        {
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
        }

        public static void ClearLogFolder(bool conf)
        {
            if (conf)
            {
                Directory.Delete(logDir, true);
            } else
            {
                LogWarning("User almost deleted all the logs!!", "Logging");
            }
        }
    }
}
