using TinyINIController;

namespace serverplatform
{
    internal class Config
    {
        private static readonly IniFile spCnfFile = new IniFile("config.ini");

        public static string GetConfig(string key, string section)
        {
            return spCnfFile.Read(key, section);
        }

        public static void MakeDefaultConfig()
        {
            spCnfFile.Write("port", "4100", "backend");
            UserAuth.CreateDefaultUsers();
        }
    }
}