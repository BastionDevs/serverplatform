﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
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
            string tempExtractDir = Path.Combine(Path.GetTempPath(), "spjava");

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
        IniFile runtimes = new IniFile($@"{runtimesdir}\runtimes.ini");

        public static void DownloadRuntime(JDKDist dist, string javaver, string javatype)
        {
            string downloadurl;

            string os = "windows";

            string apiresp = "";

            switch (dist)
            {
                case JDKDist.Temurin:
                    apiresp = AdoptiumAPI.TemurinAssets(javaver, os, "x64", javatype);
                    downloadurl = AdoptiumAPI.ParseDownloadUrl(apiresp);
                    break;
                default:
                    break;
            }
            
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
        public static string ZuluPkg(string javaver, string os, string arch, string packaging, string javatype, bool jfx, bool latestrel, string distver)
        {
            string RequestUri = $"{APIBaseUri}/zulu/packages/?java_version={javaver}&os={os}&arch={arch}&archive_type={packaging}&java_package_type={javatype}&javafx_bundled={jfx}&latest={latestrel}&distro_version={distver}";
            return new WebClient().DownloadString(RequestUri);
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
        
        public static string LibericaRelease(string javaver, string os, string bitness, string arch, string packaging, string javatype, bool jfx, bool latestrel, string distver)
        {
            string RequestUri = $"{APIBaseUri}/liberica/releases?version-feature={javaver}&version-modifier=latest&bitness={bitness}&fx={jfx}&os={os}&arch={arch}&installation-type=archive&package-type={packaging}&bundle-type={javatype}&output=json&fields=downloadUrl,version";
            return new WebClient().DownloadString(RequestUri);
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
