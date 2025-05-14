using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace serverplatform
{
    internal class APIHandler
    {
        public static async Task StartServer(CancellationToken token, int port)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{port}/");

            try
            {
                listener.Start();
                ConsoleLogging.LogSuccess($"Backend API started and listening on port {port}.", "HttpListener");

                while (!token.IsCancellationRequested)
                {
                    var contextTask = listener.GetContextAsync();
                    var completedTask = await Task.WhenAny(contextTask, Task.Delay(-1, token));

                    if (completedTask == contextTask)
                    {
                        HttpListenerContext context = contextTask.Result;
                        _ = Task.Run(() => HandleRequest(context));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (HttpListenerException ex)
            {
                ConsoleLogging.LogError($"Listener exception: {ex.Message}", "HttpListener");
            }
            finally
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                    listener.Close();
                }
                ConsoleLogging.LogMessage("Listener closed.", "HttpListener");
            }
        }

        public static void HandleRequest(HttpListenerContext context)
        {
            try
            {
                // Handle CORS preflight request
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    AddCORSHeaders(context.Response);
                    context.Response.StatusCode = 204;
                    context.Response.Close();
                    return;
                }

                ConsoleLogging.LogMessage(
                    $"Incoming {context.Request.HttpMethod} request for {context.Request.Url.AbsolutePath}",
                    "API");

                if (context.Request.HttpMethod == "POST" &&
                    (context.Request.Url.AbsolutePath == "/mcservers/start" || context.Request.Url.AbsolutePath == "/mcservers/stop"))
                {
                    string authHeader = context.Request.Headers["Authorization"];
                    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                    {
                        context.Response.StatusCode = 401;
                        RespondJson(context, JObject.FromObject(new { error = "Missing or invalid Authorization header" }).ToString());
                        return;
                    }

                    string token = authHeader.Substring("Bearer ".Length);
                    var principal = UserAuth.ValidateJwtToken(token);
                    if (principal == null)
                    {
                        context.Response.StatusCode = 401;
                        RespondJson(context, JObject.FromObject(new { error = "Invalid or expired token" }).ToString());
                        return;
                    }

                    string username = principal.Identity.Name;

                    string requestBody = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                    string serverId = JObject.Parse(requestBody)["id"]?.ToString() ?? "unknown";

                    if (context.Request.Url.AbsolutePath == "/mcservers/start")
                    {
                        ConsoleLogging.LogMessage($"User {username} requested to start server {serverId}", "API");
                        RespondJson(context, JObject.FromObject(new { status = "Server started" }).ToString());
                    }
                    else if (context.Request.Url.AbsolutePath == "/mcservers/stop")
                    {
                        ConsoleLogging.LogMessage($"User {username} requested to stop server {serverId}", "API");
                        RespondJson(context, JObject.FromObject(new { status = "Server stopped" }).ToString());
                    }
                }
                else if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/")
                {
                    ConsoleLogging.LogMessage("Root endpoint accessed.", "API");

                    RespondHTML(context,
                        "<p>Server Platform Backend server</p>" +
                        "<p>We recommend that you only port-forward the Frontend to prevent any intrusions.</p>" +
                        "<br><p>Made with &#10084;&#65039;</p><p>&copy; 2025 BastionSG</p>");
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/auth")
                {
                    string requestBody = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                    JObject body = JObject.Parse(requestBody);

                    string username = body["username"]?.ToString();
                    string password = body["password"]?.ToString();

                    ConsoleLogging.LogMessage($"User {username} attempting to authenticate...", "Authentication");

                    try
                    {
                        JObject result = UserAuth.AuthenticateUser(username, password);

                        if (result["success"]?.Value<bool>() == true)
                        {
                            ConsoleLogging.LogSuccess($"User {username} successfully authenticated.", "Authentication");
                            RespondJson(context, result.ToString());
                        }
                        else
                        {
                            string backendError = result["error"]?.ToString();
                            ConsoleLogging.LogWarning($"User {username} failed to authenticate: {backendError}", "AUTH");

                            // Send only a generic error to the client
                            result["error"] = "incorrectusrorpwd";
                            RespondJson(context, result.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleLogging.LogError($"Authentication error: {ex.Message}", "AUTH");
                        RespondJson(context, JObject.FromObject(new { success = false, error = "internalError" }).ToString());
                    }
                }

                else
                {
                    context.Response.StatusCode = 404;
                    ConsoleLogging.LogWarning($"404 - Endpoint not found: {context.Request.Url.AbsolutePath}", "API");
                    RespondJson(context, JObject.FromObject(new { error = "Not Found" }).ToString());
                }
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Internal server error: {ex.Message}", "API");
                context.Response.StatusCode = 500;
                RespondJson(context, JObject.FromObject(new { error = "Internal Server Error" }).ToString());
            }
        }

        public static void AddCORSHeaders(HttpListenerResponse response)
        {
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
        }

        public static void RespondJson(HttpListenerContext context, string json)
        {
            AddCORSHeaders(context.Response);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }

        public static void RespondText(HttpListenerContext context, string text)
        {
            AddCORSHeaders(context.Response);
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }

        public static void RespondHTML(HttpListenerContext context, string html)
        {
            AddCORSHeaders(context.Response);
            context.Response.StatusCode = 200;
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }
    }
}
