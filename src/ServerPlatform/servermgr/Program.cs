using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace servermgr
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    ____             __  _             ___________\r\n   / __ )____ ______/ /_(_)___  ____  / ____/ ___/\r\n  / __  / __ `/ ___/ __/ / __ \\/ __ \\/ /    \\__ \\ \r\n / /_/ / /_/ (__  ) /_/ / /_/ / / / / /___ ___/ / \r\n/_____/\\__,_/____/\\__/_/\\____/_/ /_/\\____//____/");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine("Bastion Server Platform");
            Console.WriteLine("Server Manager");
            Console.ForegroundColor = ConsoleColor.DarkCyan;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("© 2025 Bastion Faculty of Computer Science");
            Console.WriteLine("https://www.bastionsg.rf.gd");

            Console.WriteLine();
            Console.WriteLine("Version 1.0");

            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("STABLE");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("] Release Channel");
        }
    }
}
