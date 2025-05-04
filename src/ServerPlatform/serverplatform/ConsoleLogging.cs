using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace serverplatform
{
    internal class ConsoleLogging
    {
        static string logDir = Environment.CurrentDirectory + @"\logs";
        static string logFile = logDir + $@"\{DateTime.Now.ToString("yyyy-dd-MMM-hh-mm-tt").ToLower()}.log";

        public static void LogError(string message)
        {
            var sw = new StreamWriter(logFile, true);
            Console.ForegroundColor = ConsoleColor.Red;
            sw.WriteLine($"[ERROR] {message}");
            Console.WriteLine($"[ERROR] {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
            sw.Close();
        }

        public static void LogWarning(string message) 
        {
            var sw = new StreamWriter(logFile, true);
            Console.ForegroundColor = ConsoleColor.Yellow;
            sw.WriteLine($"[WARN] {message}");
            Console.WriteLine($"[WARN] {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
            sw.Close();
        }

        public static void LogMessage(string message)
        {
            var sw = new StreamWriter(logFile, true);
            sw.WriteLine($"{message}");
            Console.WriteLine($"{message}");
            sw.Close();
        }

        public static void LogError(string message, string component)
        {
            var sw = new StreamWriter(logFile, true);
            Console.ForegroundColor = ConsoleColor.Red;
            sw.WriteLine($"[{component}: ERROR] {message}");
            Console.WriteLine($"[{component}: ERROR] {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
            sw.Close();
        }

        public static void LogWarning(string message, string component)
        {
            var sw = new StreamWriter(logFile, true);
            Console.ForegroundColor = ConsoleColor.Yellow;
            sw.WriteLine($"[WARN] {message}");
            Console.WriteLine($"[WARN] {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
            sw.Close();
        }

        public static void LogMessage(string message, string component)
        {
            var sw = new StreamWriter(logFile, true);
            sw.WriteLine($"[{component}] {message}");
            Console.WriteLine($"[{component}] {message}");
            sw.Close();
        }
    }
}
