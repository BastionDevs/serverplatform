using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace serverplatform
{
    internal class SpigotStor
    {
        private readonly static string rootPath = Environment.CurrentDirectory + @"\SpigotStor";
        public static void CreateStor()
        {
            if (!Directory.Exists(rootPath)) 
            {
                ConsoleLogging.LogWarning("SpigotStor Repo directory does not exist. Creating...", "SpigotStor");
                Directory.CreateDirectory(rootPath);
                ConsoleLogging.LogMessage("Use the SpigotStor Repository Manager to make Spigot versions available.");
            }
        }

        public static string JARPath(string type, string version)
        {
            return Path.Combine(rootPath, type, $"{version}.jar");
        }
    }
}