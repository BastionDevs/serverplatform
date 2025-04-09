using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace serverplatform
{
    internal class PaperVersions
    {
        public static List<string> paperVersionsList()
        {
            string paperRootJson = new WebClient().DownloadString("https://api.papermc.io/v2/projects/paper");
            JObject versionsObj = JObject.Parse(paperRootJson);
            JArray versionsArray = (JArray)versionsObj["versions"];
            return versionsArray.ToObject<List<string>>();
        }

        public static List<string> paperBuildsList(string version)
        {
            string versionJson = new WebClient().DownloadString("https://api.papermc.io/v2/projects/paper/versions/"+version);
            JObject versionObj = JObject.Parse(versionJson);
            JArray buildsArray = (JArray)versionObj["builds"];
            return buildsArray.ToObject<List<string>>();
        }

        public static string getPaperJarURL(string version, string build)
        {
            string rootUrl = $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{build}";
            string buildJson = new WebClient().DownloadString(rootUrl);
            JObject buildObj = JObject.Parse(buildJson);
            return $"{rootUrl}/downloads/{buildObj["downloads"]["application"]["name"]?.ToString()}";
        }

        public static string getPaperJarHash(string version, string build)
        {
            string buildJson = new WebClient().DownloadString($"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{build}");
            JObject buildObj = JObject.Parse(buildJson);
            return buildObj["downloads"]["application"]["sha256"]?.ToString();
        }
    }
}
