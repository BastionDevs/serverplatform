using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SpigotStorMgr
{
    internal class Program
    {
        static string RepoLocation = AppDomain.CurrentDomain.BaseDirectory + @"\SpigotStor";
        static void Main(string[] args)
        {
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
            ConsoleLogging.LogMessage("Bastion Server Platform — Spigot JAR Repository Manager");

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
                "Israel has been occupying Palestinian land illegally longer than Kiwi has lived.");

            if (!Directory.Exists(RepoLocation))
            {
                Console.WriteLine();
                ConsoleLogging.LogWarning("SpigotStor Repo directory does not exist! Creating...", "Repo");
                Directory.CreateDirectory(RepoLocation);
                Directory.CreateDirectory(RepoLocation + @"\spigot");
                Directory.CreateDirectory(RepoLocation + @"\bukkit");
                ConsoleLogging.LogSuccess("Created Repo directory!", "Repo");
            }

            while (true)
            {
                Console.WriteLine();
                Console.Write("> ");
                string command = Console.ReadLine();

                if (command == "exit")
                {
                    break;
                } else
                {
                    ParseCommand(command);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to close this window . . .");
            Console.ReadKey();
            Environment.Exit(0);
        }

        static void ParseCommand(string cmd)
        {
            string[] cmdArray = Regex.Split(cmd, "(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"); ;

            switch(cmdArray[0].ToLower())
            {
                case "import":
                    if (cmdArray.Length == 4)
                    {
                        string JARType = cmdArray[1].ToLower();
                        string JARVersion = cmdArray[2];
                        string JARLocation = cmdArray[3].Trim('"');

                        if (JARType == "spigot" || JARType == "craftbukkit" || JARType == "bukkit")
                        {
                            Console.WriteLine(JARLocation);
                            if (!File.Exists(JARLocation))
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("ERROR! Server JAR was not found at the specified location.");
                                Console.ForegroundColor = ConsoleColor.Gray;
                                break;
                            }

                            if (JARType == "craftbukkit")
                            {
                                JARType = "bukkit";
                            }

                            using (FileStream sourceStream = new FileStream(JARLocation, FileMode.Open, FileAccess.Read))
                            using (FileStream destinationStream = new FileStream(RepoLocation + $@"\{JARType}\{JARVersion}.jar", FileMode.Create, FileAccess.Write))
                            {
                                byte[] buffer = new byte[81920]; // 80 KB buffer size
                                long totalBytes = sourceStream.Length;
                                long bytesCopied = 0;
                                int bytesRead;

                                while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    destinationStream.Write(buffer, 0, bytesRead);
                                    bytesCopied += bytesRead;
                                    Console.Write($"\rCopying {JARType.First().ToString().ToUpper() + JARType.Substring(1)} {JARVersion} JAR to SpigotStor Repo... {bytesCopied * 100 / totalBytes}%");
                                    break;
                                }
                            }
                        }
                        else {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("ERROR! Invalid server software type.");
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                    } else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("ERROR! Wrong syntax used for IMPORT command.");
                        Console.ForegroundColor= ConsoleColor.Gray;
                    }
                    break;
                case "uwu":
                    Console.WriteLine("⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣀⣀⣠⣤⣤⣶⣤⣤\r\n⠀⠀⠀⠀⠀⠀⠀⠀⢀⣀⣤⣤⣤⣤⣄⣀⣀⣀⣀⣀⣀⣠⣴⠶⠶⠟⣛⣛⣛⣛⣛⣛⣻⠶⢶⣦⣤⣤⣶⣶⣾⣿⣿⢿⡿⣿⡻⢶⣣⡽\r\n⠀⠀⠀⠀⠀⠀⠀⢀⣾⣻⣭⡵⣭⣳⣽⢿⣿⡿⣟⡿⣭⢷⡶⣛⡿⣻⣭⣻⣭⣯⡽⣹⡞⣽⢯⡞⣽⣫⢟⣻⢿⣷⣿⣻⣽⣳⢽⣫⢷⣹\r\n⠀⠀⠀⠀⠀⠀⢀⣾⣿⣿⡼⣳⢳⣧⣿⣻⡭⣟⡵⣯⠽⠞⠃⠉⢉⢀⣀⣀⠀⠀⠀⠉⠉⠓⠻⠞⣧⢯⢯⡽⣞⢮⢯⣟⡷⣽⡞⣧⢟⣧\r\n⠀⠀⠀⠀⠀⠀⢠⣿⣿⣽⣾⡽⢯⡽⣎⡷⡽⢚⣩⡴⢖⣚⡛⣹⡩⠯⠉⠉⠛⠙⠛⠛⠒⠶⠤⣄⡀⠉⠓⢿⡼⣫⢷⣹⢾⣹⡽⣞⡽⣺\r\n⠀⠀⠀⠀⠀⠀⢸⣿⡿⣾⡳⣽⢫⢷⠝⣡⠞⠋⠀⠀⠀⣠⡟⠁⠀⠀⠀⠀⠀⢰⠀⠀⠀⠀⠀⠀⠉⠙⠦⣄⡉⠻⣞⢧⣯⢳⡽⣎⡷⢯\r\n⠀⠀⠀⠀⠀⠀⢸⣿⡿⡷⣝⣧⡿⣡⠞⠁⠀⠀⠀⠀⣜⡟⠀⠀⠀⠀⠀⠀⠀⢸⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠙⢦⡈⢻⡼⣏⢷⡻⣼⢯\r\n⠀⠀⠀⠀⠀⠀⣾⣿⣹⢟⣵⣾⠞⠁⠀⠀⠀⠀⠀⣜⡿⠀⠀⠀⠀⠀⠀⠀⠀⡸⠀⠀⠀⠀⠀⢠⠀⠀⠀⠀⠀⠀⠙⢦⡙⣞⢯⣳⢯⣞\r\n⠀⠀⠀⠀⠀⢰⣿⡏⣯⣟⣾⠋⠀⠀⠀⠀⠀⠀⡸⣽⡇⢀⠇⠀⠀⠀⠀⠀⠀⡇⠀⠀⠀⠀⠀⠘⡄⠀⠀⠀⠀⠀⠀⠈⢳⡌⢯⣗⢯⣞\r\n⠀⠀⠀⠀⠀⢼⡿⢸⣧⡿⡁⠀⠀⠀⠀⠀⠀⣰⣿⣹⣧⣸⠀⠀⠀⠀⠀⠀⠀⣧⣠⣄⣀⣀⡀⠀⢻⡀⠀⠀⠀⠀⠀⠀⢀⢻⡜⣞⡯⣞\r\n⠀⠀⠀⠀⠀⣾⣧⡿⢋⡺⠁⠀⠀⠀⣠⠴⣻⠟⢱⢹⡇⢻⠀⠀⠀⠀⠀⠀⠀⣿⡄⠀⠀⠈⠉⠙⢺⣇⡀⠀⠀⠀⠀⠀⠈⡆⢻⣼⣳⢯\r\n⠀⠀⠀⢤⣼⣟⣍⣦⠟⠁⠀⠀⠀⠐⣡⡾⠋⠀⠘⡌⣷⢻⠀⠀⠀⠀⠀⠀⠀⢸⣷⡀⠀⠀⠀⠀⢸⣷⠈⠢⠀⠀⠀⠀⠀⢹⠸⣷⢟⡾\r\n⠀⠀⠀⢠⣿⢯⣿⠋⠀⠀⠀⠀⢠⡞⠃⠀⠀⢀⠀⡘⠼⣿⡷⣄⡀⠀⠀⠀⠀⠈⣧⠻⣦⠀⠀⠀⡸⢾⠀⠀⠀⠀⠀⠀⠀⠸⡀⢽⡾⣿\r\n⠀⠀⠀⣾⢯⣿⠃⠀⢀⠀⠠⠀⣼⢦⣠⣴⣾⣭⣯⣭⡇⠈⢻⣌⡛⠳⠶⢤⣄⣀⣹⣟⣚⣻⣦⡤⣃⣿⡄⠀⠀⠀⠀⠀⠀⠀⡇⡝⣷⣿\r\n⠀⠀⣸⣯⣿⡏⠀⣠⡇⠀⠄⢳⣿⣿⣿⣿⣿⠿⠿⠿⠷⠀⠀⠀⠈⠉⠉⠒⠒⠣⣿⣿⣿⣿⣿⣿⣿⣿⣄⣠⡆⠀⠀⢀⠀⠠⡇⢻⣼⣿\r\n⠀⣰⡿⣼⣿⣧⢫⣱⠃⠠⠀⢀⣿⢋⠍⠤⢂⠦⠂⡀⠄⠀⠀⠀⠀⠀⠀⠀⠀⣀⠠⡀⠤⡉⡉⢛⢻⡿⢿⠋⡆⠀⠀⠄⡀⢸⠁⡈⣿⣿\r\n⢰⣿⡳⣿⣽⢿⣷⡞⢰⠀⠁⣾⡇⡊⠤⠑⠂⠠⠁⡀⠄⠲⢧⣀⡠⠶⠤⠶⠞⠋⢀⠐⡐⠠⠉⢆⣾⠃⢀⡜⠇⠀⢈⠀⡀⡾⠀⠄⣿⣿\r\n⣿⡿⣱⣿⣯⣿⡿⣁⢾⠀⢁⢻⣧⡀⠐⠈⠀⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠠⠀⠠⣁⣬⣾⡯⡚⢥⠻⠀⢀⢂⠀⣰⠇⢈⠀⣿⣿\r\n⣿⣷⡝⣿⣿⣷⣦⡷⣿⠀⠠⢸⣙⢻⣦⣄⣀⣀⣂⣀⣀⡀⠀⠀⠀⠀⠀⠀⠀⠀⣀⡠⢤⣚⣴⠟⣜⠩⡖⠁⡀⢂⡼⣰⢿⡀⢀⣼⣿⣿\r\n⠈⢻⣿⣼⣯⣿⣽⣿⡿⣧⣀⠈⢮⠹⣟⠛⣛⢛⣛⣛⡛⠻⠿⣷⡿⣿⣿⣿⡿⡿⣷⢷⣾⣽⣷⣾⣶⣭⣴⣶⢾⠿⣛⢭⣿⣷⣿⡿⣟⣾\r\n⠀⠀⢈⣿⡿⢛⣥⣿⢃⣼⠿⣷⠾⣷⢿⡿⣝⡯⣞⡽⣛⣷⣱⣿⣿⣿⢳⢧⣻⡵⣏⡿⡼⢶⢯⣝⡻⢿⣿⣿⣦⣷⣾⣻⣾⢿⣽⣻⢿⣽\r\n⢀⣴⡿⢋⣴⣿⣿⡏⣾⣝⣻⡼⣻⡝⣮⢟⡾⣱⢯⣳⢟⣼⣿⣿⢯⡷⣏⣟⡶⣏⡷⣽⣹⣛⣮⢏⣟⠷⣮⡙⢿⣟⡿⣿⣯⣿⢯⣿⢯⣿\r\n⣿⠟⣰⣿⢫⣿⣾⣗⢯⡞⣵⣏⡷⣽⢫⡾⡽⣭⢷⣯⡿⣻⣿⣟⣿⢿⣼⣣⢿⡜⣷⣣⢯⣳⠽⡾⣭⢟⡾⣹⢷⣍⠻⣷⣟⡿⣿⣯⣿⣻\r\n⢃⡾⢿⣜⢯⣿⢾⣿⣣⣟⣳⢾⣱⢯⣏⣷⣻⣾⣟⠟⣽⢯⣿⣿⣯⡿⣷⡽⣺⠽⣖⣯⢳⣏⠿⣵⣛⡾⣹⡽⣎⣟⡷⣜⢻⣿⣽⣻⡾⣿\r\n⣸⢻⣝⡮⢯⣿⢿⣽⣷⢯⣾⢷⣻⣾⢯⣿⣽⠗⣍⣾⢟⢺⣿⣞⣿⣽⣟⣿⢯⣛⡷⣚⣯⢞⣻⡵⣫⣞⢷⣹⡽⢮⣝⢯⣧⡹⣷⡿⣽⣿");
                    Console.WriteLine("haiii~ UwU");
                    break;
                case "hewo":
                    Console.WriteLine("omigod haiii!! :3");
                    break;
                case "cls":
                    Console.Clear();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ERROR! Command not found.");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
            }
        }
    }
}
