using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace serverplatform
{
    internal class JavaRuntimes
    {
        public enum JDKDist
        {
            Temurin,
            Zulu,
            Liberica,
            Corretto
        }
    }

    class AdoptiumAPI
    {
        readonly static string APIBaseUri = "https://api.adoptium.net/v3";
        public static string TemurinAssets(string javaver, string os, string arch, string javatype)
        {
            string RequestUri = $"{APIBaseUri}/assets/latest/{javaver}/hotspot?architecture={arch}&image_type={javatype}&os={os}&vendor=eclipse";
            return new WebClient().DownloadString(RequestUri);
        }
    }

    class AzulMetadataAPI
    {
        readonly static string APIBaseUri = "https://api.azul.com/metadata/v1";
        public static string ZuluPkg(string javaver, string os, string arch, string packaging, string javatype, bool jfx, bool latestrel, string distver)
        {
            string RequestUri = $"{APIBaseUri}/zulu/packages/?java_version={javaver}&os={os}&arch={arch}&archive_type={packaging}&java_package_type={javatype}&javafx_bundled={jfx}&latest={latestrel}&distro_version={distver}";
            return new WebClient().DownloadString(RequestUri);
        }

        public static (string name, string version, string url) ParseLatestPackage(string pkgsresp)
        {
            if (string.IsNullOrWhiteSpace(pkgsresp))
                throw new ArgumentException("JSON response is empty.");

            var packages = JArray.Parse(pkgsresp);

            if (!packages.Any())
                throw new InvalidOperationException("No packages found in response.");

            var latest = packages
                .OrderByDescending(p => (int)p["java_version"][0]) // major
                .ThenByDescending(p => (int)p["java_version"][1])  // minor
                .ThenByDescending(p => (int)p["java_version"][2])  // patch
                .First();

            string name = (string)latest["name"];
            string version = string.Join(".", latest["java_version"].Select(v => (int)v));
            string url = (string)latest["download_url"];

            return (name, version, url);
        }
    }

    class BellSoftOpenJDKProdDiscoveryAPI
    {
        readonly static string APIBaseUri = "https://api.bell-sw.com/v1";
        public static string LibericaRelease(string javaver, string os, string bitness, string arch, string packaging, string javatype, bool jfx, bool latestrel, string distver)
        {
            string RequestUri = $"{APIBaseUri}/liberica/releases?version-feature={javaver}&version-modifier=latest&bitness={bitness}&fx={jfx}&os={os}&arch={arch}&installation-type=archive&package-type={packaging}&bundle-type={javatype}&output=json&fields=downloadUrl,version";
            return new WebClient().DownloadString(RequestUri);
        }
    }
}
