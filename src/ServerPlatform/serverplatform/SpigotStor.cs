using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace serverplatform
{
    internal class SpigotStor
    {
        private readonly string rootPath = Environment.CurrentDirectory + @"\SpigotStor";
        private readonly string buildToolsDir;
        private readonly string buildToolsPath;
        private readonly string repoJsonPath;
        private readonly string repoPath;

        private Dictionary<string, Dictionary<string, JObject>> repoData;

        public SpigotStor()
        {
            buildToolsDir = Path.Combine(rootPath, "BuildTools");
            buildToolsPath = Path.Combine(buildToolsDir, "BuildTools.jar");
            repoJsonPath = Path.Combine(rootPath, "repo.json");
            repoPath = Path.Combine(rootPath, "Repo");

            Directory.CreateDirectory(buildToolsDir);
            Directory.CreateDirectory(repoPath);

            LoadRepoJson();
        }

        public void StoreSpigot(string version, bool compileBukkit)
        {
            string workingDir = Path.Combine(Path.GetTempPath(), $"BuildTools_{version}_{Guid.NewGuid()}");
            Directory.CreateDirectory(workingDir);

            if (!File.Exists(buildToolsPath))
            {
                Console.WriteLine("Downloading BuildTools...");
                using (var client = new WebClient())
                {
                    client.DownloadFile("https://hub.spigotmc.org/jenkins/job/BuildTools/lastSuccessfulBuild/artifact/target/BuildTools.jar", buildToolsPath);
                }
            }

            File.Copy(buildToolsPath, Path.Combine(workingDir, "BuildTools.jar"), true);

            Console.WriteLine("Running BuildTools...");
            var arguments = compileBukkit
                ? $"-jar BuildTools.jar --rev {version} --compile craftbukkit"
                : $"-jar BuildTools.jar --rev {version}";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (!TryReadRemoteBuild(version, out int buildNumber)) buildNumber = -1;

            if (!compileBukkit)
            {
                string spigotJar = Path.Combine(workingDir, $"spigot-{version}.jar");
                if (File.Exists(spigotJar))
                {
                    string destDir = Path.Combine(repoPath, "Spigot");
                    Directory.CreateDirectory(destDir);
                    string dest = Path.Combine(destDir, $"spigot-{version}.jar");
                    File.Copy(spigotJar, dest, true);

                    SaveToRepoJson("Spigot", version, true, buildNumber);
                    Console.WriteLine($"Spigot {version} stored at {dest}");
                }
            }
            else
            {
                string craftJar = Path.Combine(workingDir, $"craftbukkit-{version}.jar");
                if (File.Exists(craftJar))
                {
                    string destDir = Path.Combine(repoPath, "CraftBukkit");
                    Directory.CreateDirectory(destDir);
                    string dest = Path.Combine(destDir, $"craftbukkit-{version}.jar");
                    File.Copy(craftJar, dest, true);

                    SaveToRepoJson("CraftBukkit", version, true, buildNumber);
                    Console.WriteLine($"CraftBukkit {version} stored at {dest}");
                }
            }

            Directory.Delete(workingDir, true);
            SaveRepoJson();
        }

        public void CheckForUpdates(string version)
        {
            if (!TryReadRemoteBuild(version, out int remoteBuild))
            {
                Console.WriteLine($"Failed to fetch remote build info for {version}.");
                return;
            }

            bool updated = false;
            foreach (var type in new[] { "Spigot", "CraftBukkit" })
            {
                if (repoData.ContainsKey(type) && repoData[type].ContainsKey(version))
                {
                    int localBuild = repoData[type][version]["build"]?.ToObject<int>() ?? -1;
                    if (remoteBuild > localBuild)
                    {
                        Console.WriteLine($"{type} {version} has a new build! Remote: {remoteBuild}, Local: {localBuild}");
                        updated = true;
                    }
                }
            }

            if (!updated)
            {
                Console.WriteLine($"{version} is up to date.");
            }
        }

        public void ListAvailableVersions()
        {
            foreach (var type in repoData.Keys)
            {
                Console.WriteLine($"{type} Versions:");
                foreach (var kvp in repoData[type])
                {
                    Console.WriteLine($"- {kvp.Key} (build {kvp.Value["build"]})");
                }
                Console.WriteLine();
            }
        }

        private bool TryReadRemoteBuild(string version, out int buildNumber)
        {
            buildNumber = -1;
            string url = $"https://hub.spigotmc.org/versions/{version}.json";
            try
            {
                using (var client = new WebClient())
                {
                    string json = client.DownloadString(url);
                    var obj = JsonConvert.DeserializeObject<JObject>(json);
                    buildNumber = int.Parse((string)obj["name"]);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void SaveToRepoJson(string type, string version, bool available, int build)
        {
            if (!repoData.ContainsKey(type))
                repoData[type] = new Dictionary<string, JObject>();

            var meta = new JObject
            {
                ["available"] = available,
                ["build"] = build
            };

            repoData[type][version] = meta;
        }

        private void LoadRepoJson()
        {
            if (File.Exists(repoJsonPath))
            {
                string json = File.ReadAllText(repoJsonPath);
                repoData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, JObject>>>(json);
            }
            else
            {
                repoData = new Dictionary<string, Dictionary<string, JObject>>();
            }
        }

        private void SaveRepoJson()
        {
            string json = JsonConvert.SerializeObject(repoData, Formatting.Indented);
            File.WriteAllText(repoJsonPath, json);
        }
    }
}