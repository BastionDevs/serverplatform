using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TinyINIController;

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

        public static readonly string runtimesdir = $@"{Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}\JavaRuntimes";
        IniFile runtimes = new IniFile($@"{runtimesdir}\runtimes.ini");

        public static void DownloadRuntime()
        {

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

        public static string ZuluDownloadUrl(string pkgsresp)
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

            return (string)latest["download_url"];
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

    class CorrettoDownload
    {
        public enum CorrettoVersion
        {
            J8,
            JRE8,
            J11,
            J17,
            J21
        }

        static string GetVersionNumber(CorrettoVersion version)
        {
            string name = version.ToString();

            // Extract the number from the end of the enum name
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsDigit(name[i]))
                {
                    return name.Substring(i);
                }
            }

            return string.Empty; // or throw exception if needed
        }

        public static string CorrettoDownloadLink(CorrettoVersion ver, string platform, string arch, string packaging)
        {
            if (ver == CorrettoVersion.JRE8)
            {
                return $"https://corretto.aws/downloads/latest/amazon-corretto-8-{arch}-{platform}-jre.{packaging}";
            } else
            {
                return $"https://corretto.aws/downloads/latest/amazon-corretto-{GetVersionNumber(ver)}-{arch}-{platform}-jdk.{packaging}";
            }
        }
    }
}
