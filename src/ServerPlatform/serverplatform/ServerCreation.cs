using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Security.Policy;

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

            string[] versionStringSplit = versionString.Split('/');
            string[] fullVersionArray = new string[versionStringSplit.Length + 1];
            fullVersionArray[0] = software;
            Array.Copy(versionStringSplit, 0, fullVersionArray, 1, versionStringSplit.Length);

            string[] ramAmmounts = { minRam, maxRam };

            try
            {
                CreateServer(name, desc, fullVersionArray, ramAmmounts);
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

        public static void CreateServer(string name, string description, string[] version, string[] ramAmounts)
        {
            var serversFolder = Config.GetConfig("ServersDir", "main");

            if (Directory.Exists($@"{serversFolder}\\{name}")) throw new Exception("Folder already exists.");

            string serverDirectory = $@"{serversFolder}\\{name}";

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
                    new WebClient().DownloadFile(serverJarUrl, $@"{serverDirectory}\\{jarFileName}");

                    File.WriteAllText($@"{serverDirectory}\paper.sh", $"@echo off\r\njava -Xms{ramAmounts[0]}M -Xmx{ramAmounts[1]}M -XX:+AlwaysPreTouch -XX:+DisableExplicitGC -XX:+ParallelRefProcEnabled -XX:+PerfDisableSharedMem -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1HeapRegionSize=8M -XX:G1HeapWastePercent=5 -XX:G1MaxNewSizePercent=40 -XX:G1MixedGCCountTarget=4 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1NewSizePercent=30 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:G1ReservePercent=20 -XX:InitiatingHeapOccupancyPercent=15 -XX:MaxGCPauseMillis=200 -XX:MaxTenuringThreshold=1 -XX:SurvivorRatio=32 -Dusing.aikars.flags=https://mcflags.emc.gs -Daikars.new.flags=true -jar {jarFileName} nogui\r\npause");
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
            }
            else if (version[0] == "bukkit")
            {
            }
            else if (version[0] == "vanilla")
            {
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
                    new WebClient().DownloadFile(serverJarUrl, $@"{serverDirectory}\\{jarFileName}");

                    File.WriteAllText($@"{serverDirectory}\velo.sh", $"@echo off\r\njava -Xms{ramAmounts[0]}M -Xmx{ramAmounts[1]}M -XX:+AlwaysPreTouch -XX:+ParallelRefProcEnabled -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1HeapRegionSize=4M -XX:MaxInlineLevel=15 -jar {jarFileName}");
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
    }
}