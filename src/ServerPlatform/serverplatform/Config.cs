using System;
using System.IO;
using TinyINIController;

namespace serverplatform
{
    internal class Config
    {
        private static readonly IniFile SpCnfFile = new IniFile("config.ini");
        
        public static ServerIndex serverIndex = new ServerIndex("servers.json");

        public static string GetConfig(string key, string section)
        {
            return SpCnfFile.Read(key, section);
        }

        public static void MakeDefaultConfig()
        {
            SpCnfFile.Write("port", "4100", "backend");
            SpCnfFile.Write("ServersDir", Path.Combine(AppContext.BaseDirectory, "servers"), "main");
            UserAuth.CreateDefaultUsers();
        }
    }
}