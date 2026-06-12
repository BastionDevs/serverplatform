using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace serverplatform
{
    internal sealed class ServerMetricsSnapshot
    {
        public double CpuPercent { get; set; }

        public long MemoryBytes { get; set; }

        public long MemoryMB =>
            MemoryBytes / 1024 / 1024;
    }

    internal class ServerMetrics
    {
        private readonly ServerInstance instance;

        private TimeSpan lastCpuTime;
        private DateTime lastCpuSample;
        private bool hasSample;

        public double CpuPercent { get; private set; }
        public long MemoryBytes { get; private set; }

        public ServerMetrics(ServerInstance instance)
        {
            this.instance = instance;
        }

        public void Update()
        {
            var process = instance.Process;

            if (process == null || process.HasExited)
            {
                CpuPercent = 0;
                MemoryBytes = 0;
                hasSample = false;
                return;
            }

            process.Refresh();

            MemoryBytes = process.WorkingSet64;

            TimeSpan currentCpu = process.TotalProcessorTime;
            DateTime now = DateTime.UtcNow;

            if (!hasSample)
            {
                lastCpuTime = currentCpu;
                lastCpuSample = now;
                hasSample = true;
                CpuPercent = 0;
                return;
            }

            double cpuUsedMs = (currentCpu - lastCpuTime).TotalMilliseconds;
            double elapsedMs = (now - lastCpuSample).TotalMilliseconds;

            CpuPercent = elapsedMs > 0
                ? cpuUsedMs / (elapsedMs * Environment.ProcessorCount) * 100.0
                : 0;

            lastCpuTime = currentCpu;
            lastCpuSample = now;
        }

        public ServerMetricsSnapshot GetSnapshot()
        {
            return new ServerMetricsSnapshot
            {
                CpuPercent = CpuPercent,
                MemoryBytes = MemoryBytes
            };
        }

        public static void HandleServerMetrics(HttpListenerContext context)
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
                $"User {username} requested for metrics for server {serverId}.",
                "ServerControls"
            );

            try
            {
                ServerControls.TryGetInstance(serverId, out var server);

                var metrics = server.Metrics.GetSnapshot();

                ApiHandler.RespondJson(
                    context,
                    JObject.FromObject(new
                    {
                        success = true,
                        cpu = metrics.CpuPercent,
                        memory = metrics.MemoryBytes,
                        memoryMB = metrics.MemoryMB
                    }).ToString()
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
    }
}
