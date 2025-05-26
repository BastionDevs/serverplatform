using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace serverplatform
{
    internal class VanillaVersions
    {
        private const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

        public static List<string> VanillaVersionsList()
        {
            var manifestJson = new WebClient().DownloadString(ManifestUrl);
            var manifestObj = JObject.Parse(manifestJson);
            var versionsArray = (JArray)manifestObj["versions"];
            var versionList = new List<string>();

            foreach (var version in versionsArray)
            {
                versionList.Add(version["id"].ToString());
            }

            return versionList;
        }

        public static string GetVanillaJarUrl(string version)
        {
            var manifestJson = new WebClient().DownloadString(ManifestUrl);
            var manifestObj = JObject.Parse(manifestJson);
            var versionsArray = (JArray)manifestObj["versions"];

            foreach (var versionObj in versionsArray)
            {
                if (versionObj["id"].ToString() == version)
                {
                    var versionUrl = versionObj["url"].ToString();
                    var versionDetailJson = new WebClient().DownloadString(versionUrl);
                    var versionDetailObj = JObject.Parse(versionDetailJson);
                    return versionDetailObj["downloads"]["server"]["url"].ToString();
                }
            }

            return null;
        }

        public static string GetVanillaJarHash(string version)
        {
            var manifestJson = new WebClient().DownloadString(ManifestUrl);
            var manifestObj = JObject.Parse(manifestJson);
            var versionsArray = (JArray)manifestObj["versions"];

            foreach (var versionObj in versionsArray)
            {
                if (versionObj["id"].ToString() == version)
                {
                    var versionUrl = versionObj["url"].ToString();
                    var versionDetailJson = new WebClient().DownloadString(versionUrl);
                    var versionDetailObj = JObject.Parse(versionDetailJson);
                    return versionDetailObj["downloads"]["server"]["sha1"].ToString();
                }
            }

            return null;
        }
    }
}
