using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;

namespace serverplatform
{
    internal class PaperVersions
    {
        public static List<string> PaperVersionsList()
        {
            var paperRootJson = new WebClient().DownloadString("https://api.papermc.io/v2/projects/paper");
            var versionsObj = JObject.Parse(paperRootJson);
            var versionsArray = (JArray)versionsObj["versions"];
            return versionsArray.ToObject<List<string>>();
        }

        public static List<string> PaperBuildsList(string version)
        {
            var versionJson =
                new WebClient().DownloadString("https://api.papermc.io/v2/projects/paper/versions/" + version);
            var versionObj = JObject.Parse(versionJson);
            var buildsArray = (JArray)versionObj["builds"];
            return buildsArray.ToObject<List<string>>();
        }

        public static string GetPaperJarUrl(string version, string build)
        {
            var rootUrl = $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{build}";
            var buildJson = new WebClient().DownloadString(rootUrl);
            var buildObj = JObject.Parse(buildJson);
            return $"{rootUrl}/downloads/{buildObj["downloads"]["application"]["name"]}";
        }

        public static string GetPaperJarHash(string version, string build)
        {
            var buildJson =
                new WebClient().DownloadString(
                    $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{build}");
            var buildObj = JObject.Parse(buildJson);
            return buildObj["downloads"]["application"]["sha256"]?.ToString();
        }
    }

    internal class VelocityVersions
    {
        public static List<string> VelocityVersionsList()
        {
            var paperRootJson = new WebClient().DownloadString("https://api.papermc.io/v2/projects/velocity");
            var versionsObj = JObject.Parse(paperRootJson);
            var versionsArray = (JArray)versionsObj["versions"];
            return versionsArray.ToObject<List<string>>();
        }

        public static List<string> VelocityBuildsList(string version)
        {
            var versionJson =
                new WebClient().DownloadString("https://api.papermc.io/v2/projects/velocity/versions/" + version);
            var versionObj = JObject.Parse(versionJson);
            var buildsArray = (JArray)versionObj["builds"];
            return buildsArray.ToObject<List<string>>();
        }

        public static string GetVelocityJarUrl(string version, string build)
        {
            var rootUrl = $"https://api.papermc.io/v2/projects/velocity/versions/{version}/builds/{build}";
            var buildJson = new WebClient().DownloadString(rootUrl);
            var buildObj = JObject.Parse(buildJson);
            return $"{rootUrl}/downloads/{buildObj["downloads"]["application"]["name"]}";
        }

        public static string GetVelocityJarHash(string version, string build)
        {
            var buildJson =
                new WebClient().DownloadString(
                    $"https://api.papermc.io/v2/projects/velocity/versions/{version}/builds/{build}");
            var buildObj = JObject.Parse(buildJson);
            return buildObj["downloads"]["application"]["sha256"]?.ToString();
        }
    }
}