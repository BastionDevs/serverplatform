using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace serverplatform
{
    internal class Program
    {
        static CancellationTokenSource _cts = new CancellationTokenSource();

        static void Main(string[] args)
        {
            //Firstrun logic
            if (args.Length >= 1)
            {
                if (args[0] == "--firstrun")
                {
                    Config.MakeDefaultConfig();
                } else if (args[0] == "--clearlogs")
                {
                    if (args.Length >= 2)
                    {
                        if (args[1] == "true")
                        {
                            ConsoleLogging.ClearLogFolder(true);
                            Environment.Exit(1);
                        } else 
                        { 
                            ConsoleLogging.ClearLogFolder(false);
                            Environment.Exit(2);
                        }
                    } else
                    {
                        ConsoleLogging.ClearLogFolder(false);
                        Environment.Exit(2);
                    }
                }
            }

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                ConsoleLogging.LogMessage("Stopping server...");
                _cts.Cancel();
                eventArgs.Cancel = true; // Prevent immediate termination
            };

            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    ____             __  _             ___________\r\n   / __ )____ ______/ /_(_)___  ____  / ____/ ___/\r\n  / __  / __ `/ ___/ __/ / __ \\/ __ \\/ /    \\__ \\ \r\n / /_/ / /_/ (__  ) /_/ / /_/ / / / / /___ ___/ / \r\n/_____/\\__,_/____/\\__/_/\\____/_/ /_/\\____//____/");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;

            ConsoleLogging.LogMessage("Bastion Server Platform");
            ConsoleLogging.LogMessage("Backend Server");
            Console.ForegroundColor = ConsoleColor.DarkCyan;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            ConsoleLogging.LogMessage("© 2025 Bastion Faculty of Computer Science");
            Console.WriteLine("https://www.bastionsg.rf.gd");

            Console.WriteLine();
            ConsoleLogging.LogMessage("Version 1.0");

            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("STABLE");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("] Release Channel");

            ConsoleLogging.LogMessage("F̶u̶n̶ Devastating fact:");
            ConsoleLogging.LogMessage("Israel has been occupying Palestinian land illegally for longer than CubeNotFound has lived.");

            Console.WriteLine("Development use? [True/False]");
            bool devMode = bool.Parse(Console.ReadLine());

            int backendPort;
            if (devMode) { backendPort = 1234; } else { backendPort = int.Parse(Config.GetConfig("port", "backend")); }
            APIHandler.StartServer(_cts.Token, backendPort).GetAwaiter().GetResult();

            //Server stopping
            ConsoleLogging.LogMessage("Stopping Server Platform...");
            Console.ReadLine();
        }
    }
}
