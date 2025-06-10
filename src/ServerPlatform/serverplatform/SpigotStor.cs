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
                ConsoleLogging.LogMessage("SpigotStor Repo directory does not exist. Creating...", "SpigotStor");
                Directory.CreateDirectory(rootPath);
            }

        }
    }
}