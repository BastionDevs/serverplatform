using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using TinyINIController;

namespace serverplatform
{
    internal class ServerCreation
    {
        public static void HandleCreationRequest(HttpListenerContext context)
        {
            var requestBody =
                new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            var body = JObject.Parse(requestBody);

            ConsoleLogging.LogMessage($"User is attempting to create a server with name {body["serverName"]?.ToString()}.", "ServerCreation");

            string auth = body["auth"]?.ToString();
            string name = body["serverName"]?.ToString();
            string desc= body["serverDesc"]?.ToString();
            string software = body["software"]?.ToString();
            string versionString = body["version"]?.ToString();
            string minRam = body["minRam"]?.ToString();
            string maxRam = body["maxRam"]?.ToString();
            string javaRuntime = body["javaVer"]?.ToString();
            string javaVendor = body["javaVendor"]?.ToString();
            string javaType = body["javaType"]?.ToString();

            string[] versionStringSplit = versionString.Split('/');
            string[] fullVersionArray = new string[versionStringSplit.Length + 1];
            fullVersionArray[0] = software;
            Array.Copy(versionStringSplit, 0, fullVersionArray, 1, versionStringSplit.Length);

            string[] ramAmmounts = { minRam, maxRam };

            try
            {
                string sid = GenerateServerId();
                CreateServer(sid, name, desc, fullVersionArray, ramAmmounts, new string[] { javaType, javaRuntime, javaVendor });
                ConsoleLogging.LogSuccess($"Successfully created server {name} with ID {sid}.");
                ApiHandler.RespondJson(context, "{\"success\":\"true\", \"message\":\"Server created successfully.\"}");
            } catch (Exception ex)
            {
                ConsoleLogging.LogError($"Exception occured while trying to create server {name}: {ex.Message}", "ServerCreation");
                ApiHandler.RespondJson(context, JObject.FromObject(new
                {
                    success = false,
                    error = "internalError"
                }).ToString());
            }
        }

        public static string GenerateServerId()
        {
            // Use a cryptographically strong random number generator
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[16]; // 128-bit ID
                rng.GetBytes(bytes);

                // Convert to Base32 or Hex string for readability (Hex here)
                StringBuilder sb = new StringBuilder(32);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2")); // two-digit hex

                return sb.ToString(); // e.g., "d13f04a2b56c8e3e5f0a9b7cd19c234f"
            }
        }

        public static void CreateServer(string id, string name, string description, string[] version, string[] ramAmounts, string[] jdk)
        {
            try
            {
                var serversFolder = Config.GetConfig("ServersDir", "main");

                if (Directory.Exists($@"{serversFolder}\\{id}")) throw new Exception("Folder already exists.");

                string serverDirectory = $@"{serversFolder}\\{id}";
                Directory.CreateDirectory($@"{serverDirectory}");
                Directory.CreateDirectory($@"{serverDirectory}\\files");

                if (version[0] == "paper")
                {
                    if (version.Length == 3)
                    {
                        /* 1st: paper
                         * 2nd: mc version
                         * 3rd: build num
                         */

                        string serverJarUrl = PaperVersions.GetPaperJarUrl(version[1], version[2]);
                        string jarFileName = Path.GetFileName(new Uri(serverJarUrl).AbsolutePath);
                        new WebClient().DownloadFile(serverJarUrl, $@"{serverDirectory}\\files\\{jarFileName}");

                        File.WriteAllText($@"{serverDirectory}\minecraft.launch", $"-XX:+AlwaysPreTouch -XX:+DisableExplicitGC -XX:+ParallelRefProcEnabled -XX:+PerfDisableSharedMem -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1HeapRegionSize=8M -XX:G1HeapWastePercent=5 -XX:G1MaxNewSizePercent=40 -XX:G1MixedGCCountTarget=4 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1NewSizePercent=30 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:G1ReservePercent=20 -XX:InitiatingHeapOccupancyPercent=15 -XX:MaxGCPauseMillis=200 -XX:MaxTenuringThreshold=1 -XX:SurvivorRatio=32 -Dusing.aikars.flags=https://mcflags.emc.gs -Daikars.new.flags=true -jar {jarFileName} nogui");

                        IniFile srvConfig = new IniFile($@"{serverDirectory}\srvconfig.ini");

                        srvConfig.Write("id", id, "info");
                        srvConfig.Write("name", name, "info");
                        srvConfig.Write("desc", description, "info");

                        srvConfig.Write("vendor", jdk[0], "java");
                        srvConfig.Write("ver", jdk[1], "java");
                        srvConfig.Write("type", jdk[2], "java");
                        srvConfig.Write("minRam", ramAmounts[0], "java");
                        srvConfig.Write("maxRam", ramAmounts[1], "java");

                        srvConfig.Write("software", "paper", "software");
                        srvConfig.Write("mcversion", version[1], "software");
                        srvConfig.Write("build", version[2], "software");
                    }
                    else
                    {
                        throw new Exception("Wrong arguments passed.");
                    }
                }
                else if (version[0] == "purpur")
                {
                }
                else if (version[0] == "spigot")
                {
                    File.Copy(SpigotStor.JARPath("spigot", version[1]), $@"{serverDirectory}\\files\\spigot-{version[1]}.jar");
                    File.WriteAllText($@"{serverDirectory}\minecraft.launch", $"-jar spigot-{version[1]}.jar");

                    IniFile srvConfig = new IniFile($@"{serverDirectory}\srvconfig.ini");

                    srvConfig.Write("id", id, "info");
                    srvConfig.Write("name", name, "info");
                    srvConfig.Write("desc", description, "info");

                    srvConfig.Write("vendor", jdk[0], "java");
                    srvConfig.Write("ver", jdk[1], "java");
                    srvConfig.Write("type", jdk[2], "java");
                    srvConfig.Write("minRam", ramAmounts[0], "java");
                    srvConfig.Write("maxRam", ramAmounts[1], "java");

                    srvConfig.Write("software", "spigot", "software");
                    srvConfig.Write("mcversion", version[1], "software");
                    srvConfig.Write("build", version[2], "software");
                }
                else if (version[0] == "bukkit")
                {
                    File.Copy(SpigotStor.JARPath("bukkit", version[1]), $@"{serverDirectory}\\files\\bukkit-{version[1]}.jar");
                    File.WriteAllText($@"{serverDirectory}\minecraft.launch", $"-jar bukkit-{version[1]}.jar");

                    IniFile srvConfig = new IniFile($@"{serverDirectory}\srvconfig.ini");

                    srvConfig.Write("id", id, "info");
                    srvConfig.Write("name", name, "info");
                    srvConfig.Write("desc", description, "info");

                    srvConfig.Write("vendor", jdk[0], "java");
                    srvConfig.Write("ver", jdk[1], "java");
                    srvConfig.Write("type", jdk[2], "java");
                    srvConfig.Write("minRam", ramAmounts[0], "java");
                    srvConfig.Write("maxRam", ramAmounts[1], "java");

                    srvConfig.Write("software", "bukkit", "software");
                    srvConfig.Write("mcversion", version[1], "software");
                    srvConfig.Write("build", version[2], "software");
                }
                else if (version[0] == "vanilla")
                {
                    if (version.Length == 2)
                    {
                        /* 1st: vanilla
                         * 2nd: version
                         */

                        string serverJarUrl = VanillaVersions.GetVanillaJarUrl(version[1]);
                        string jarFileName = Path.GetFileName(new Uri(serverJarUrl).AbsolutePath);
                        new WebClient().DownloadFile(serverJarUrl, $@"{serverDirectory}\\files\\{jarFileName}");

                        File.WriteAllText($@"{serverDirectory}\minecraft.launch", $"-jar {jarFileName}");

                        IniFile srvConfig = new IniFile($@"{serverDirectory}\srvconfig.ini");

                        srvConfig.Write("id", id, "info");
                        srvConfig.Write("name", name, "info");
                        srvConfig.Write("desc", description, "info");

                        srvConfig.Write("vendor", jdk[0], "java");
                        srvConfig.Write("ver", jdk[1], "java");
                        srvConfig.Write("type", jdk[2], "java");
                        srvConfig.Write("minRam", ramAmounts[0], "java");
                        srvConfig.Write("maxRam", ramAmounts[1], "java");

                        srvConfig.Write("software", "vanilla", "software");
                        srvConfig.Write("mcversion", version[1], "software");
                        srvConfig.Write("build", version[1], "software");
                    }
                    else
                    {
                        throw new Exception("Wrong arguments passed.");
                    }
                }
                else if (version[0] == "velocity")
                {
                    if (version.Length == 3)
                    {
                        /* 1st: velocity
                         * 2nd: velocity version
                         * 3rd: build num
                         */

                        string serverJarUrl = PaperVersions.GetPaperJarUrl(version[1], version[2]);
                        string jarFileName = Path.GetFileName(new Uri(serverJarUrl).AbsolutePath);
                        new WebClient().DownloadFile(serverJarUrl, $@"{serverDirectory}\\files\\{jarFileName}");

                        File.WriteAllText($@"{serverDirectory}\minecraft.launch", $"-XX:+AlwaysPreTouch -XX:+ParallelRefProcEnabled -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1HeapRegionSize=4M -XX:MaxInlineLevel=15 -jar {jarFileName}");

                        IniFile srvConfig = new IniFile($@"{serverDirectory}\srvconfig.ini");

                        srvConfig.Write("id", id, "info");
                        srvConfig.Write("name", name, "info");
                        srvConfig.Write("desc", description, "info");

                        srvConfig.Write("vendor", jdk[0], "java");
                        srvConfig.Write("ver", jdk[1], "java");
                        srvConfig.Write("type", jdk[2], "java");
                        srvConfig.Write("minRam", ramAmounts[0], "java");
                        srvConfig.Write("maxRam", ramAmounts[1], "java");

                        srvConfig.Write("software", "velocity", "software");
                        srvConfig.Write("mcversion", version[1], "software");
                        srvConfig.Write("build", version[2], "software");
                    }
                    else
                    {
                        throw new Exception("Wrong arguments passed.");
                    }
                }
                else if (version[0] == "bungeecord")
                {
                }
                else
                {
                    throw new Exception("Software not supported.");
                }
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"An error occurred during server creation: {ex.Message}", "CreateServer");
            }
        }
    }
}