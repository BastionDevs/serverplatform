using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
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

        // ========= Paths =========

        public static readonly string RuntimesDir =
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "JavaRuntimes"
            );

        private static readonly string DownloadDir =
            Path.Combine(RuntimesDir, "download");

        private static readonly string IndexFile =
            Path.Combine(RuntimesDir, "runtimes.json");

        // ========= Models =========

        private class RuntimeIndex
        {
            public List<RuntimeEntry> Runtimes { get; set; } = new List<RuntimeEntry>();
        }

        private class RuntimeEntry
        {
            public string Distribution { get; set; }
            public string JavaVersion { get; set; }   // "8", "17", "21"
            public string JavaType { get; set; }      // "jre" / "jdk"
            public string FullVersion { get; set; }   // "17.0.10+7"
            public string Path { get; set; }
        }

        // ========= Index Helpers =========

        private static RuntimeIndex LoadIndex()
        {
            if (!File.Exists(IndexFile))
                return new RuntimeIndex();

            return JsonConvert.DeserializeObject<RuntimeIndex>(
                File.ReadAllText(IndexFile)
            );
        }

        private static void SaveIndex(RuntimeIndex index)
        {
            Directory.CreateDirectory(RuntimesDir);

            File.WriteAllText(
                IndexFile,
                JsonConvert.SerializeObject(index, Formatting.Indented)
            );
        }

        public static bool JavaRuntimeExists(JavaRuntimes.JDKDist dist, string javaver, string javatype)
        {
            string javaExe = JavaRuntimes.GetJavaExecutable(dist, javaver, javatype);
            return !string.IsNullOrEmpty(javaExe) && File.Exists(javaExe);
        }

        public static JavaRuntimes.JDKDist ParseVendor(string vendor)
        {
            return (JavaRuntimes.JDKDist)Enum.Parse(
                typeof(JavaRuntimes.JDKDist),
                vendor,
                ignoreCase: true
            );
        }

        // ========= ZIP Extraction =========

        public static void ExtractJavaZip(string zipPath, string targetDir)
        {
            string tempExtractDir = Path.Combine(
                Path.GetTempPath(),
                "spjava",
                Path.GetFileNameWithoutExtension(zipPath)
            );

            ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

            string[] topLevelDirs = Directory.GetDirectories(tempExtractDir);
            if (topLevelDirs.Length == 0)
                throw new Exception("No directories found in Java ZIP.");

            string actualJavaDir = topLevelDirs[0];

            Directory.CreateDirectory(targetDir);

            foreach (string dir in Directory.GetDirectories(actualJavaDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dir.Replace(actualJavaDir, targetDir));
            }

            foreach (string file in Directory.GetFiles(actualJavaDir, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(
                    file,
                    file.Replace(actualJavaDir, targetDir),
                    true
                );
            }

            Directory.Delete(tempExtractDir, true);
        }

        // ========= Download =========

        public static async Task DownloadRuntimeAsync(JDKDist dist, string javaver, string javatype)
        {
            Directory.CreateDirectory(RuntimesDir);
            Directory.CreateDirectory(DownloadDir);

            string downloadUrl;
            string apiResp;
            string fullVersion;
            string os = "windows";

            switch (dist)
            {
                case JDKDist.Temurin:
                    apiResp = await AdoptiumAPI.TemurinAssetsAsync(javaver, os, "x64", javatype);
                    downloadUrl = AdoptiumAPI.ParseDownloadUrl(apiResp);
                    fullVersion = AdoptiumAPI.ParseVersion(apiResp);
                    break;

                case JDKDist.Zulu:
                    apiResp = await AzulMetadataAPI.ZuluPkgAsync(
                        javaver, os, "x64", "zip", javatype, false, true, javaver
                    );
                    downloadUrl = AzulMetadataAPI.ParseDownloadUrl(apiResp);
                    fullVersion = AzulMetadataAPI.ParseVersion(apiResp);
                    break;

                case JDKDist.Liberica:
                    apiResp = await BellSoftOpenJDKProdDiscoveryAPI.LibericaReleaseAsync(
                        javaver, os, "64", "x86", "zip", javatype, false, true, "8"
                    );
                    downloadUrl = BellSoftOpenJDKProdDiscoveryAPI.ParseDownloadUrl(apiResp);
                    fullVersion = BellSoftOpenJDKProdDiscoveryAPI.ParseVersion(apiResp);
                    break;

                default:
                    throw new NotSupportedException("Unsupported JDK distribution.");
            }

            string zipPath = Path.Combine(
                DownloadDir,
                $"{dist}{javatype}{javaver}.zip"
            );

            using (var http = new HttpClient())
            using (var stream = await http.GetStreamAsync(downloadUrl))
            using (var fs = File.Create(zipPath))
            {
                await stream.CopyToAsync(fs);
            }

            string installPath = Path.Combine(
                RuntimesDir,
                $"{dist}{javatype}{javaver}"
            );

            await Task.Run(() => ExtractJavaZip(zipPath, installPath));

            // ========= Update JSON Index =========

            var index = LoadIndex();

            index.Runtimes.RemoveAll(r =>
                r.Distribution == dist.ToString() &&
                r.JavaVersion == javaver &&
                r.JavaType == javatype
            );

            index.Runtimes.Add(new RuntimeEntry
            {
                Distribution = dist.ToString(),
                JavaVersion = javaver,
                JavaType = javatype,
                FullVersion = fullVersion,   // ✅ the only extra data
                Path = installPath
            });

            SaveIndex(index);
        }

        // ========= Lookup (used by server launcher) =========

        public static string GetJavaExecutable(
            JDKDist dist,
            string javaver,
            string javatype
        )
        {
            var index = LoadIndex();

            var runtime = index.Runtimes.FirstOrDefault(r =>
                r.Distribution == dist.ToString() &&
                r.JavaVersion == javaver &&
                r.JavaType == javatype
            );

            if (runtime == null)
                return null;

            return Path.Combine(runtime.Path, "bin", "java.exe");
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
}
