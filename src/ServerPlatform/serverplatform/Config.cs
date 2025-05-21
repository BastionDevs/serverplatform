using TinyINIController;

namespace serverplatform
{
    internal class Config
    {
        private static readonly IniFile SpCnfFile = new IniFile("config.ini");

        public static string GetConfig(string key, string section)
        {
            return SpCnfFile.Read(key, section);
        }

        public static void MakeDefaultConfig()
        {
            SpCnfFile.Write("port", "4100", "backend");
            UserAuth.CreateDefaultUsers();
        }
    }
}