using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net;
using Newtonsoft.Json;

namespace serverplatform
{
    internal class SpigotStor
    {
        private readonly string rootPath = Environment.CurrentDirectory + @"\SpigotStor";
        private readonly string buildToolsDir;
        private readonly string buildToolsPath;
        private readonly string repoJsonPath;
        private readonly string repoPath;

        private Dictionary<string, Dictionary<string, bool>> repoData;

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

        private void LoadRepoJson()
        {
            if (File.Exists(repoJsonPath))
            {
                repoData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, bool>>>(
                    File.ReadAllText(repoJsonPath));
            }
            else
            {
                repoData = new Dictionary<string, Dictionary<string, bool>>
                {
                    { "Spigot", new Dictionary<string, bool>() },
                    { "CraftBukkit", new Dictionary<string, bool>() }
                };
            }
        }

        private void SaveRepoJson()
        {
            File.WriteAllText(repoJsonPath, JsonConvert.SerializeObject(repoData, Formatting.Indented));
        }

        public void StoreSpigot(string version, bool compileBukkit)
        {
            // Skip if already available
            if (repoData["Spigot"].ContainsKey(version) && repoData["Spigot"][version])
            {
                Console.WriteLine($"Spigot {version} is already compiled and stored.");
                return;
            }

            if (compileBukkit && repoData["CraftBukkit"].ContainsKey(version) && repoData["CraftBukkit"][version])
            {
                Console.WriteLine($"CraftBukkit {version} is already compiled and stored.");
                return;
            }

            // Download BuildTools if not present
            if (!File.Exists(buildToolsPath))
            {
                Console.WriteLine("Downloading BuildTools...");
                using (var client = new WebClient())
                {
                    client.DownloadFile("https://hub.spigotmc.org/jenkins/job/BuildTools/lastSuccessfulBuild/artifact/target/BuildTools.jar", buildToolsPath);
                }
            }

            // Setup working directory
            string workingDir = Path.Combine(Path.GetTempPath(), $"BuildTools_{version}_{Guid.NewGuid()}");
            Directory.CreateDirectory(workingDir);
            File.Copy(buildToolsPath, Path.Combine(workingDir, "BuildTools.jar"), true);

            // Setup build args
            string args = compileBukkit
                ? $"-jar BuildTools.jar --rev {version} --compile craftbukkit"
                : $"-jar BuildTools.jar --rev {version}";

            Console.WriteLine("Running BuildTools...");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            // Copy Spigot
            string spigotJarName = $"spigot-{version}.jar";
            string spigotJarPath = Path.Combine(workingDir, spigotJarName);
            string spigotDestPath = Path.Combine(repoPath, "Spigot", spigotJarName);

            if (File.Exists(spigotJarPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(spigotDestPath));
                File.Copy(spigotJarPath, spigotDestPath, true);
                Console.WriteLine($"Stored Spigot {version} at {spigotDestPath}");

                repoData["Spigot"][version] = true;
            }

            // Copy CraftBukkit if requested
            if (compileBukkit)
            {
                string cbJarName = $"craftbukkit-{version}.jar";
                string cbJarPath = Path.Combine(workingDir, cbJarName);
                string cbDestPath = Path.Combine(repoPath, "CraftBukkit", cbJarName);

                if (File.Exists(cbJarPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cbDestPath));
                    File.Copy(cbJarPath, cbDestPath, true);
                    Console.WriteLine($"Stored CraftBukkit {version} at {cbDestPath}");

                    repoData["CraftBukkit"][version] = true;
                }
            }

            SaveRepoJson();
            Directory.Delete(workingDir, true);
        }

        public void ListAvailableVersions()
        {
            Console.WriteLine("Available Spigot versions:");
            foreach (var kv in repoData["Spigot"])
            {
                if (kv.Value)
                    Console.WriteLine($"- Spigot {kv.Key}");
            }

            Console.WriteLine("\nAvailable CraftBukkit versions:");
            foreach (var kv in repoData["CraftBukkit"])
            {
                if (kv.Value)
                    Console.WriteLine($"- CraftBukkit {kv.Key}");
            }
        }
    }
}
