using System;
using System.IO;
using System.Security.Cryptography;

namespace serverplatform
{
    internal class ServerCreation
    {
        public static void CreateServer(string name, string description, string[] version)
        {
            var serversFolder = Config.GetConfig("ServersDir", "main");

            if (Directory.Exists($@"{serversFolder}\\{name}"))
            {
                throw new Exception("Folder already exists.");
            } else
            {
                if (version[0] == "paper")
                {
                    if (version.Length == 3)
                    {
                        /* 1st: paper
                         * 2nd: mc version
                         * 3rd: build num
                         */
                    } else
                    {
                        throw new Exception("Wrong arguments passed.");
                    }
                } else if (version[0] == "spigot")
                {

                } else if (version[0] == "bukkit")
                {

                } else if (version[0] == "vanilla")
                {

                } else
                {
                    throw new Exception("Software not supported.");
                }
            }
        }
    }
}