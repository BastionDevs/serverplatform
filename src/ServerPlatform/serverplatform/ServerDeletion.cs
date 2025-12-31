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
    internal class ServerDeletion
    {
        public static void HandleDeletionRequest(HttpListenerContext context)
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

            ConsoleLogging.LogMessage(
                $"User {username} requested deletion of server {serverId}.",
                "ServerDeletion"
            );

            var serverIndex = Config.serverIndex;

            // 3. Ownership check using existing API ONLY
            var userServers = serverIndex.GetServersForUser(username);
            bool ownsServer = userServers.Any(s =>
                s.Id.Equals(serverId, StringComparison.OrdinalIgnoreCase));

            if (!ownsServer)
            {
                // IMPORTANT: identical response for "not found" and "not owned"
                context.Response.StatusCode = 404;
                ApiHandler.RespondJson(
                    context,
                    "{\"success\":false,\"error\":\"serverNotFound\"}"
                );
                return;
            }

            try
            {
                // 4. Remove from index
                serverIndex.RemoveServer(username, serverId);

                // 5. Delete server directory
                var serversFolder = Config.GetConfig("ServersDir", "main");
                string serverPath = Path.Combine(serversFolder, serverId);

                if (Directory.Exists(serverPath))
                    Directory.Delete(serverPath, recursive: true);

                ConsoleLogging.LogSuccess(
                    $"Server {serverId} deleted by {username}.",
                    "ServerDeletion"
                );

                ApiHandler.RespondJson(
                    context,
                    "{\"success\":true,\"message\":\"Server deleted.\"}"
                );
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError(
                    $"Failed to delete server {serverId}: {ex.Message}",
                    "ServerDeletion"
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
