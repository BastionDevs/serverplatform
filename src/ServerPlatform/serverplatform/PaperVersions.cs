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
        static List<string> paperVersionsList(string version)
        {
            string paperRootJson = new WebClient().DownloadString("https://api.papermc.io/v2/projects/paper");
            JObject versionsObj = JObject.Parse(paperRootJson);
            JArray versionsArray = (JArray)versionsObj["versions"];
            return versionsArray.ToObject<List<string>>();
        }

        static List<string> paperBuildsList(string version)
        {
            string versionJson = new WebClient().DownloadString("https://api.papermc.io/v2/projects/paper/versions/"+version);
            JObject versionObj = JObject.Parse(versionJson);
            JArray buildsArray = (JArray)versionObj["builds"];
            return buildsArray.ToObject<List<string>>();
        }
    }
}
