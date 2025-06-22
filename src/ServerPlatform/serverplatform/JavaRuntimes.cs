using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
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

        public static void ExtractJavaZip(string zipPath, string targetDir)
        {
            string tempExtractDir = Path.Combine(Path.GetTempPath(), "spjava", Path.GetFileName(zipPath).Replace(".zip", ""));

            // Extract to temp folder
            ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

            // Find the first directory inside the extracted temp dir
            string[] topLevelEntries = Directory.GetDirectories(tempExtractDir);
            if (topLevelEntries.Length == 0)
                throw new Exception("No directories found in ZIP archive.");

            string actualJavaFolder = topLevelEntries[0];

            // Create target if not exists
            Directory.CreateDirectory(targetDir);

            // Move contents from actualJavaFolder to targetDir
            foreach (string dirPath in Directory.GetDirectories(actualJavaFolder, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(actualJavaFolder, targetDir));
            }

            foreach (string newPath in Directory.GetFiles(actualJavaFolder, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(actualJavaFolder, targetDir), true);
            }

            // Clean up
            Directory.Delete(tempExtractDir, true);
        }

        public static readonly string runtimesdir = $@"{Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}\JavaRuntimes";
        static IniFile runtimes = new IniFile($@"{runtimesdir}\runtimes.ini");

        public static async Task DownloadRuntimeAsync(JDKDist dist, string javaver, string javatype)
        {
            string downloadurl = "";
            string os = "windows";
            string apiresp = "";
            string version = "";

            switch (dist)
            {
                case JDKDist.Temurin:
                    apiresp = await AdoptiumAPI.TemurinAssetsAsync(javaver, os, "x64", javatype);
                    downloadurl = AdoptiumAPI.ParseDownloadUrl(apiresp);
                    version = AdoptiumAPI.ParseVersion(apiresp);
                    break;

                case JDKDist.Zulu:
                    apiresp = await AzulMetadataAPI.ZuluPkgAsync(javaver, os, "x64", "zip", javatype, false, true, javaver);
                    downloadurl = AzulMetadataAPI.ParseDownloadUrl(apiresp);
                    version = AzulMetadataAPI.ParseVersion(apiresp);
                    break;

                case JDKDist.Liberica:
                    apiresp = await BellSoftOpenJDKProdDiscoveryAPI.LibericaReleaseAsync(javaver, os, "64", "x86", "zip", javatype, false, true, "8");
                    downloadurl = BellSoftOpenJDKProdDiscoveryAPI.ParseDownloadUrl(apiresp);
                    version = BellSoftOpenJDKProdDiscoveryAPI.ParseVersion(apiresp);
                    break;
            }

            string zipdlpath = Path.Combine(runtimesdir, "download", $"{dist}{javatype}{javaver}.zip");

            using (var httpClient = new HttpClient())
            using (var stream = await httpClient.GetStreamAsync(downloadurl))
            using (var fileStream = File.Create(zipdlpath))
            {
                await stream.CopyToAsync(fileStream);
            }

            await Task.Run(() => ExtractJavaZip(zipdlpath, Path.Combine(runtimesdir, $"{dist}{javatype}{javaver}")));

            runtimes.Write("version", version, $"{dist}{javaver}");
        }

    }

    class AdoptiumAPI
    {
        readonly static string APIBaseUri = "https://api.adoptium.net/v3";
        public static async Task<string> TemurinAssetsAsync(string javaver, string os, string arch, string javatype)
        {
            string RequestUri = $"{APIBaseUri}/assets/latest/{javaver}/hotspot?architecture={arch}&image_type={javatype}&os={os}&vendor=eclipse";
            using (var client = new HttpClient())
            {
                return await client.GetStringAsync(RequestUri);
            }
        }

        public static string ParseDownloadUrl(string assetsresponse)
        {
            var assetsarray = JArray.Parse(assetsresponse);
            var assetitself = assetsarray[0];

            return assetitself["binary"]?["package"]?["link"]?.ToString();
        }

        public static string ParseVersion(string assetsresponse)
        {
            var assetsarray = JArray.Parse(assetsresponse);
            var assetitself = assetsarray[0];

            return assetitself["release_name"]?.ToString();
        }
    }

    class AzulMetadataAPI
    {
        readonly static string APIBaseUri = "https://api.azul.com/metadata/v1";
        public static async Task<string> ZuluPkgAsync(string javaver, string os, string arch, string packaging, string javatype, bool jfx, bool latestrel, string distver)
        {
            string RequestUri = $"{APIBaseUri}/zulu/packages/?java_version={javaver}&os={os}&arch={arch}&archive_type={packaging}&java_package_type={javatype}&javafx_bundled={jfx}&latest={latestrel}&distro_version={distver}";
            using (var client = new HttpClient())
            {
                return await client.GetStringAsync(RequestUri);
            }
        }

        public static string ParseDownloadUrl(string pkgsresp)
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

            string url = (string)latest["download_url"];

            return url;
        }

        public static string ParseVersion(string pkgsresp)
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

            string version = string.Join(".", latest["java_version"].Select(v => (int)v));

            return version;
        }
    }

    class BellSoftOpenJDKProdDiscoveryAPI
    {
        readonly static string APIBaseUri = "https://api.bell-sw.com/v1";
        
        public static async Task<string> LibericaReleaseAsync(string javaver, string os, string bitness, string arch, string packaging, string javatype, bool jfx, bool latestrel, string distver)
        {
            string RequestUri = $"{APIBaseUri}/liberica/releases?version-feature={javaver}&version-modifier=latest&bitness={bitness}&fx={jfx}&os={os}&arch={arch}&installation-type=archive&package-type={packaging}&bundle-type={javatype}&output=json&fields=downloadUrl,version";
            using (var client = new HttpClient())
            {
                return await client.GetStringAsync(RequestUri);
            }
        }

        public static string ParseDownloadUrl(string assetsresponse)
        {
            var assetsarray = JArray.Parse(assetsresponse);
            var assetitself = assetsarray[0];

            return assetitself["downloadUrl"]?.ToString();
        }

        public static string ParseVersion(string assetsresponse)
        {
            var assetsarray = JArray.Parse(assetsresponse);
            var assetitself = assetsarray[0];

            return assetitself["version"]?.ToString();
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
