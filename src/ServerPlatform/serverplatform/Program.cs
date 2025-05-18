using System;
using System.IO;
using System.Text;
using System.Threading;

namespace serverplatform
{
    internal class Program
    {
        private static readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private static void Main(string[] args)
        {
            // First run logic
            if (args.Length >= 1)
            {
                if (args[0] == "--firstrun")
                {
                    if (File.Exists("users.json") || File.Exists("appsettings.json"))
                    {
                        ConsoleLogging.LogWarning("First run has already been completed. Aborting setup.");
                        Environment.Exit(1);
                    }
                    else
                    {
                        Config.MakeDefaultConfig();
                        UserAuth.CreateDefaultUsers(); // Add this if user creation is part of first run
                        ConsoleLogging.LogSuccess("First run completed successfully.");
                        Environment.Exit(0);
                    }
                }
                else if (args[0] == "--clearlogs")
                {
                    if (args.Length >= 2 && args[1] == "true")
                    {
                        ConsoleLogging.ClearLogFolder(true);
                        Environment.Exit(1);
                    }
                    else
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
                eventArgs.Cancel = true;
            };

            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.Title = "Bastion Server Platform | Backend Server";

            // Fancy ASCII Logo
            Console.WriteLine(" ▄▄▄▄    ▄▄▄        ██████ ▄▄▄█████▓ ██▓ ▒█████   ███▄    █  ▄████▄    ██████ ");
            Console.WriteLine("▓█████▄ ▒████▄    ▒██    ▒ ▓  ██▒ ▓▒▓██▒▒██▒  ██▒ ██ ▀█   █ ▒██▀ ▀█  ▒██    ▒ ");
            Console.WriteLine("▒██▒ ▄██▒██  ▀█▄  ░ ▓██▄   ▒ ▓██░ ▒░▒██▒▒██░  ██▒▓██  ▀█ ██▒▒▓█    ▄ ░ ▓██▄   ");
            Console.WriteLine("▒██░█▀  ░██▄▄▄▄██   ▒   ██▒░ ▓██▓ ░ ░██░▒██   ██░▓██▒  ▐▌██▒▒▓▓▄ ▄██▒  ▒   ██▒");
            Console.WriteLine("░▓█  ▀█▓ ▓█   ▓██▒▒██████▒▒  ▒██▒ ░ ░██░░ ████▓▒░▒██░   ▓██░▒ ▓███▀ ░▒██████▒▒");
            Console.WriteLine("░▒▓███▀▒ ▒▒   ▓▒█░▒ ▒▓▒ ▒ ░  ▒ ░░   ░▓  ░ ▒░▒░▒░ ░ ▒░   ▒ ▒ ░ ░▒ ▒  ░▒ ▒▓▒ ▒ ░");
            Console.WriteLine("▒░▒   ░   ▒   ▒▒ ░░ ░▒  ░ ░    ░     ▒ ░  ░ ▒ ▒░ ░ ░░   ░ ▒░  ░  ▒   ░ ░▒  ░ ░");
            Console.WriteLine(" ░    ░   ░   ▒   ░  ░  ░    ░       ▒ ░░ ░ ░ ▒     ░   ░ ░ ░        ░  ░  ░  ");
            Console.WriteLine(" ░            ░  ░      ░            ░      ░ ░           ░ ░ ░            ░");
            Console.WriteLine("      ░                                                     ░              ");
            Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            ConsoleLogging.LogMessage("Bastion Server Platform — Backend Server");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Visit " + "https://www.bastionsg.rf.gd");

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            ConsoleLogging.LogMessage("© 2025 Bastion Faculty of Computer Science");

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            ConsoleLogging.LogMessage("Version 1.0.0");

            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("STABLE");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("] Release Channel");

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            ConsoleLogging.LogMessage("💣 Devastating Fact:");
            Console.ForegroundColor = ConsoleColor.Gray;
            ConsoleLogging.LogMessage(
                "Israel has been occupying Palestinian land illegally longer than CubeNotFound has lived.");
            Console.WriteLine();


            Console.WriteLine("Development use? [True/False]");
            var devMode = bool.Parse(Console.ReadLine());

            int backendPort;
            if (devMode)
                backendPort = 1234;
            else
                backendPort = int.Parse(Config.GetConfig("port", "backend"));

            // Start server
            ConsoleLogging.LogSuccess("Server is now online and listening.");
            APIHandler.StartServer(_cts.Token, backendPort).GetAwaiter().GetResult();

            // Server stopping
            ConsoleLogging.LogSuccess("Server has stopped cleanly.");
            ConsoleLogging.LogMessage("Stopping Server Platform...");
            Console.ReadLine();
        }
    }
}