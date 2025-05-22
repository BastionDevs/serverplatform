using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;

namespace serverplatform
{
    internal class ServerCreation
    {
        public static void HandleCreationRequest(HttpListenerContext context)
        {
            var requestBody =
                new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            var body = JObject.Parse(requestBody);

            string auth = body["auth"]?.ToString();
            string name = body["serverName"]?.ToString();
            string desc= body["serverDesc"]?.ToString();
            string software = body["software"]?.ToString();
            string versionString = body["version"]?.ToString();
            string minRam = body["minRam"]?.ToString();
            string maxRam = body["maxRam"]?.ToString();

            string[] versionStringSplit = versionString.Split('/');
            string[] newArray = new string[versionStringSplit.Length + 1];
            newArray[0] = software;
            Array.Copy(versionStringSplit, 0, newArray, 1, versionStringSplit.Length);

            string[] ramAmmounts = { minRam, maxRam };

            CreateServer(name, desc, newArray, ramAmmounts);
        }

        public static void CreateServer(string name, string description, string[] version, string[] ramAmounts)
        {
            var serversFolder = Config.GetConfig("ServersDir", "main");

            if (Directory.Exists($@"{serversFolder}\\{name}")) throw new Exception("Folder already exists.");

            if (version[0] == "paper")
            {
                if (version.Length == 3)
                {
                    /* 1st: paper
                     * 2nd: mc version
                     * 3rd: build num
                     */
                }
                else
                {
                    throw new Exception("Wrong arguments passed.");
                }
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
            else
            {
                throw new Exception("Software not supported.");
            }
        }
    }
}