using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace serverplatform
{
    internal class SpigotStor
    {
        private readonly string buildToolsUrl = "https://hub.spigotmc.org/jenkins/job/BuildTools/lastSuccessfulBuild/artifact/target/BuildTools.jar";
        private readonly string localRepoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LocalRepo", "Spigot");

        public static void CreateConfig()
        {

        }

        public void StoreSpigot(string version, bool compileBukkit)
        {
            string workingDir = Path.Combine(Path.GetTempPath(), $"BuildTools_{version}_{Guid.NewGuid()}");
            Directory.CreateDirectory(workingDir);

            string buildToolsPath = Path.Combine(workingDir, "BuildTools.jar");

            Console.WriteLine("Downloading BuildTools...");
            using (var client = new WebClient())
            {
                client.DownloadFile(buildToolsUrl, buildToolsPath);
            }

            Console.WriteLine("Running BuildTools...");
            var process = new Process { };
            if (compileBukkit)
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "java",
                        Arguments = $"-jar BuildTools.jar --rev {version} --compile craftbukkit",
                        WorkingDirectory = workingDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
            } else
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "java",
                        Arguments = $"-jar BuildTools.jar --rev {version}",
                        WorkingDirectory = workingDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
            }

            process.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            string builtJarPath = Path.Combine(workingDir, $"spigot-{version}.jar");
            if (!File.Exists(builtJarPath))
            {
                Console.WriteLine("Build failed or jar not found.");
                return;
            }

            string repoVersionPath = Path.Combine(localRepoPath, version);
            Directory.CreateDirectory(repoVersionPath);

            string destinationPath = Path.Combine(repoVersionPath, $"spigot-{version}.jar");
            File.Copy(builtJarPath, destinationPath, true);

            Console.WriteLine($"Spigot {version} stored at {destinationPath}");

            // Optional: Cleanup
            Directory.Delete(workingDir, true);
        }
    }
}
