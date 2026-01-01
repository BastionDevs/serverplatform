using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TinyINIController;

namespace serverplatform
{
    internal sealed class ServerInstance
    {
        public string Id { get; }
        public Process Process { get; }
        public StringBuilder Log { get; } = new StringBuilder();
        public DateTime StartedAt { get; } = DateTime.UtcNow;

        public ServerInstance(string id, Process process)
        {
            Id = id;
            Process = process;
        }
    }

    internal static class ServerControls
    {
        private static readonly ConcurrentDictionary<string, ServerInstance> servers =
            new ConcurrentDictionary<string, ServerInstance>(StringComparer.OrdinalIgnoreCase);

        public static event Action<string, string> OnConsoleOutput;

        private static readonly string ServersDir =
            Config.GetConfig("ServersDir", "main");

        private static readonly string JavaRuntimesDir =
            Path.Combine(AppContext.BaseDirectory, "JavaRuntimes");

        private const int MaxLogChars = 5_000_000;

        // -------------------------
        // START SERVER
        // -------------------------
        public static async Task StartServer(string id)
        {
            if (servers.ContainsKey(id))
                return;

            string serverRoot = Path.Combine(ServersDir, id);
            string filesDir = Path.Combine(serverRoot, "files");

            var ini = new IniFile(Path.Combine(serverRoot, "srvconfig.ini"));

            string javaVendor = ini.Read("vendor", "java");
            string javaType = ini.Read("type", "java");
            string javaVer = ini.Read("ver", "java");
            string minRam = ini.Read("minRam", "java");
            string maxRam = ini.Read("maxRam", "java");

            string launchArgs = File.ReadAllText(
                Path.Combine(serverRoot, "minecraft.launch"));

            string javaPath = Path.Combine(
                JavaRuntimesDir,
                $"{javaVendor}{javaType}{javaVer}",
                "bin",
                "java.exe"
            );

            if (!File.Exists(javaPath))
            {
                await JavaRuntimes.EnsureRuntimeAsync(
                    JavaRuntimes.ParseVendor(javaVendor),
                    javaVer,
                    javaType
                );

                if (!File.Exists(javaPath))
                    throw new FileNotFoundException(
                        "Java runtime not found after install.", javaPath);
            }

            ConsoleLogging.LogMessage(
                $"Launching server {id} using Java: {javaPath}",
                "ServerControls"
            );

            ConsoleLogging.LogMessage(
                $"{javaPath} -Xms{minRam}M -Xmx{maxRam}M {launchArgs} nogui",
                "ServerControls"
            );

            var psi = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = $"-Xms{minRam}M -Xmx{maxRam}M {launchArgs} nogui",
                WorkingDirectory = filesDir,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            var instance = new ServerInstance(id, process);

            // ATOMIC ADD before start
            if (!servers.TryAdd(id, instance))
                return;

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    AppendLog(instance, e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    AppendLog(instance, "[STDERR] " + e.Data);
            };

            process.Exited += (_, __) =>
            {
                ServerInstance removed;
                servers.TryRemove(id, out removed);
                AppendLog(instance, "[SERVER PLATFORM] Server process exited.");
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch
            {
                servers.TryRemove(id, out _);
                throw;
            }
        }

        // -------------------------
        // STOP SERVER
        // -------------------------
        public static async Task StopServer(string id, int timeoutMs = 30000)
        {
            if (!servers.TryGetValue(id, out var instance))
                return;

            var process = instance.Process;

            try
            {
                if (!process.HasExited)
                {
                    process.StandardInput.WriteLine("stop");
                    process.StandardInput.Flush();

                    bool exited = await Task.Run(() => process.WaitForExit(timeoutMs));
                    if (!exited)
                        process.Kill();
                }
            }
            finally
            {
                process.Dispose();
                servers.TryRemove(id, out _);
            }
        }

        // -------------------------
        // RESTART SERVER
        // -------------------------
        public static async Task RestartServer(string id)
        {
            await StopServer(id);
            await Task.Delay(1000);
            await StartServer(id);
        }

        // -------------------------
        // SEND COMMAND
        // -------------------------
        public static void SendCommand(string id, string command)
        {
            if (servers.TryGetValue(id, out var instance) &&
                !instance.Process.HasExited)
            {
                instance.Process.StandardInput.WriteLine(command);
                instance.Process.StandardInput.Flush();
            }
        }

        // -------------------------
        // STATUS / LOGS
        // -------------------------
        public static bool IsRunning(string id)
        {
            return servers.TryGetValue(id, out var s) &&
                   !s.Process.HasExited;
        }

        public static string GetLog(string id)
        {
            return servers.TryGetValue(id, out var s)
                ? s.Log.ToString()
                : string.Empty;
        }

        private static void AppendLog(ServerInstance instance, string line)
        {
            string stamped = $"[{DateTime.Now:HH:mm:ss}] {line}";
            instance.Log.AppendLine(stamped);

            if (instance.Log.Length > MaxLogChars)
                instance.Log.Remove(0, instance.Log.Length - MaxLogChars);

            OnConsoleOutput?.Invoke(instance.Id, stamped);
        }
    }


    internal class ServerControlsHandler 
    {
        public static void HandleStartServer(HttpListenerContext context)
        {
            // 1. Authenticate
            var principal = UserAuth.VerifyJwtFromContext(context);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"message\":\"Unauthorised.\"}"
                );
                return;
            }

            string username = UserAuth.GetUsernameFromPrincipal(principal);

            // 2. Read request body
            string requestBody;
            using (var reader = new StreamReader(
                context.Request.InputStream,
                context.Request.ContentEncoding))
            {
                requestBody = reader.ReadToEnd();
            }

            JObject body;
            try
            {
                body = JObject.Parse(requestBody);
            }
            catch
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"invalidJson\"}"
                );
                return;
            }

            string serverId = body["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(serverId))
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"missingServerId\"}"
                );
                return;
            }

            var serverIndex = Config.serverIndex;

            // 3. Ownership check using existing API ONLY
            var userServers = serverIndex.GetServersForUser(username);
            bool ownsServer = userServers.Any(s =>
                s.Id.Equals(serverId, StringComparison.OrdinalIgnoreCase));

            if (!ownsServer)
            {
                // IMPORTANT: identical response for "not found" and "not owned"
                ConsoleLogging.LogWarning($"User {username} tried to start {serverId} but does not exist/have permissions!", "ServerControls");
                context.Response.StatusCode = 404;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"serverNotFound\"}"
                );
                return;
            }

            ConsoleLogging.LogMessage(
                $"User {username} requested to start server {serverId}.",
                "ServerControls"
            );

            try
            {
                _ = Task.Run(() => ServerControls.StartServer(serverId));
                ConsoleLogging.LogSuccess(
                    $"Server {serverId} started by {username}.",
                    "ServerControls"
                );

                ApiHandler.RespondJson(
                    context,
                    "{\"success\":true,\"message\":\"Server started.\"}"
                );
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError(
                    $"Failed to start server {serverId}: {ex.Message}",
                    "ServerControls"
                );

                context.Response.StatusCode = 500;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"internalError\"}"
                );
            }
        }

        public static void HandleStopServer(HttpListenerContext context)
        {
            // 1. Authenticate
            var principal = UserAuth.VerifyJwtFromContext(context);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"message\":\"Unauthorised.\"}"
                );
                return;
            }

            string username = UserAuth.GetUsernameFromPrincipal(principal);

            // 2. Read request body
            string requestBody;
            using (var reader = new StreamReader(
                context.Request.InputStream,
                context.Request.ContentEncoding))
            {
                requestBody = reader.ReadToEnd();
            }

            JObject body;
            try
            {
                body = JObject.Parse(requestBody);
            }
            catch
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"invalidJson\"}"
                );
                return;
            }

            string serverId = body["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(serverId))
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"missingServerId\"}"
                );
                return;
            }

            var serverIndex = Config.serverIndex;

            // 3. Ownership check using existing API ONLY
            var userServers = serverIndex.GetServersForUser(username);
            bool ownsServer = userServers.Any(s =>
                s.Id.Equals(serverId, StringComparison.OrdinalIgnoreCase));

            if (!ownsServer)
            {
                // IMPORTANT: identical response for "not found" and "not owned"
                ConsoleLogging.LogWarning($"User {username} tried to stop {serverId} but does not exist/have permissions!", "ServerDeletion");
                context.Response.StatusCode = 404;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"serverNotFound\"}"
                );
                return;
            }

            ConsoleLogging.LogMessage(
                $"User {username} requested to stop server {serverId}.",
                "ServerControls"
            );

            try
            {
                _ = Task.Run(() => ServerControls.StopServer(serverId));
                ConsoleLogging.LogSuccess(
                    $"Server {serverId} stopped by {username}.",
                    "ServerControls"
                );

                ApiHandler.RespondJson(
                    context,
                    "{\"success\":true,\"message\":\"Server stopped.\"}"
                );
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError(
                    $"Failed to stop server {serverId}: {ex.Message}",
                    "ServerControls"
                );

                context.Response.StatusCode = 500;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"internalError\"}"
                );
            }
        }
        public static void HandleRestartServer(HttpListenerContext context)
        {
            // 1. Authenticate
            var principal = UserAuth.VerifyJwtFromContext(context);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"message\":\"Unauthorised.\"}"
                );
                return;
            }

            string username = UserAuth.GetUsernameFromPrincipal(principal);

            // 2. Read request body
            string requestBody;
            using (var reader = new StreamReader(
                context.Request.InputStream,
                context.Request.ContentEncoding))
            {
                requestBody = reader.ReadToEnd();
            }

            JObject body;
            try
            {
                body = JObject.Parse(requestBody);
            }
            catch
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"invalidJson\"}"
                );
                return;
            }

            string serverId = body["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(serverId))
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"missingServerId\"}"
                );
                return;
            }

            var serverIndex = Config.serverIndex;

            // 3. Ownership check using existing API ONLY
            var userServers = serverIndex.GetServersForUser(username);
            bool ownsServer = userServers.Any(s =>
                s.Id.Equals(serverId, StringComparison.OrdinalIgnoreCase));

            if (!ownsServer)
            {
                // IMPORTANT: identical response for "not found" and "not owned"
                ConsoleLogging.LogWarning($"User {username} tried to restart {serverId} but does not exist/have permissions!", "ServerControls");
                context.Response.StatusCode = 404;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"serverNotFound\"}"
                );
                return;
            }

            ConsoleLogging.LogMessage(
                $"User {username} requested to restart server {serverId}.",
                "ServerControls"
            );

            try
            {
                _ = Task.Run(() => ServerControls.RestartServer(serverId));
                ConsoleLogging.LogSuccess(
                    $"Server {serverId} restarted by {username}.",
                    "ServerControls"
                );

                ApiHandler.RespondJson(
                    context,
                    "{\"success\":true,\"message\":\"Server restarted.\"}"
                );
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError(
                    $"Failed to restart server {serverId}: {ex.Message}",
                    "ServerControls"
                );

                context.Response.StatusCode = 500;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"internalError\"}"
                );
            }
        }
    }
}