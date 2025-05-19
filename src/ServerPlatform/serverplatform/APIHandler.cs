using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;

namespace serverplatform
{
    internal class APIHandler
    {
        public static async Task StartServer(CancellationToken token, int port)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");

            try
            {
                Console.WriteLine("Calling listener.Start()...");
                listener.Start();
                ConsoleLogging.LogSuccess($"Backend API started and listening on port {port}.", "Listener");

                while (!token.IsCancellationRequested)
                {
                    var contextTask = listener.GetContextAsync();
                    var completedTask = await Task.WhenAny(contextTask, Task.Delay(-1, token));

                    if (completedTask == contextTask)
                    {
                        var context = contextTask.Result;
                        _ = Task.Run(() => HandleRequest(context), token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ConsoleLogging.LogMessage("Cancellation requested. Shutting down API listener...", "Listener");
            }
            catch (HttpListenerException ex)
            {
                ConsoleLogging.LogError($"HttpListener exception: {ex.Message}", "Listener");
            }
            finally
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                    listener.Close();
                }

                ConsoleLogging.LogMessage("API listener stopped and cleaned up.", "Listener");
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            try
            {
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    AddCORSHeaders(context.Response);
                    context.Response.StatusCode = 204;
                    context.Response.Close();
                    ConsoleLogging.LogMessage($"Handled CORS preflight request for {context.Request.Url.AbsolutePath}", "API");
                    return;
                }

                ConsoleLogging.LogMessage($"Incoming {context.Request.HttpMethod} request for {context.Request.Url.AbsolutePath}", "API");

                if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath.StartsWith("/auth"))
                {
                    var pathParts = context.Request.Url.AbsolutePath.Split('/');

                    if (pathParts.Length == 3 && pathParts[2] == "login")
                    {
                        HandleLogin(context);
                    }
                    else if (pathParts.Length == 3 && pathParts[2] == "logout")
                    {
                        HandleLogout(context);
                    }
                    else if (pathParts.Length == 3 && pathParts[2] == "register")
                    {
                        HandleRegister(context);
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        ConsoleLogging.LogWarning($"404 Not Found: {context.Request.Url.AbsolutePath}", "API");
                        RespondJson(context, JObject.FromObject(new
                        {
                            error = "Not Found"
                        }).ToString());
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                    ConsoleLogging.LogWarning($"404 Not Found: {context.Request.Url.AbsolutePath}", "API");
                    RespondJson(context, JObject.FromObject(new
                    {
                        error = "Not Found"
                    }).ToString());
                }
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Unhandled exception: {ex.Message}", "API");
                context.Response.StatusCode = 500;
                RespondJson(context, JObject.FromObject(new
                {
                    error = "Internal Server Error"
                }).ToString());
            }
        }

        private static void HandleLogin(HttpListenerContext context)
        {
            var requestBody = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            var body = JObject.Parse(requestBody);

            var username = body["username"]?.ToString();
            var password = body["password"]?.ToString();

            ConsoleLogging.LogMessage($"User {username} is attempting to log in.", "AUTH");

            try
            {
                var result = UserAuth.AuthenticateUser(username, password);

                if (result["success"]?.Value<bool>() == true)
                {
                    ConsoleLogging.LogSuccess($"User {username} authenticated successfully.", "AUTH");
                    RespondJson(context, result.ToString());
                }
                else
                {
                    var backendError = result["error"]?.ToString();
                    ConsoleLogging.LogWarning($"Authentication failed for {username}: {backendError}", "AUTH");

                    result["error"] = "incorrectusrorpwd";
                    RespondJson(context, result.ToString());
                }
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Login exception for {username}: {ex.Message}", "AUTH");
                RespondJson(context, JObject.FromObject(new
                {
                    success = false, error = "internalError"
                }).ToString());
            }
        }

        private static void HandleLogout(HttpListenerContext context)
        {
            var authHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                ConsoleLogging.LogWarning("Missing or invalid Authorization header in logout attempt.", "AUTH");
                context.Response.StatusCode = 401;
                RespondJson(context, JObject.FromObject(new
                {
                    error = "Missing or invalid Authorization header"
                }).ToString());
                return;
            }
            var token = authHeader.Substring("Bearer ".Length).Trim();
            
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                context.Response.StatusCode = 400;
                RespondJson(context, JObject.FromObject(new
                {
                    error = "Hacker Hacker on the Wall!"
                }).ToString());
                return;
            }
            
            JObject result = UserAuth.LogoutUser(token);

            context.Response.StatusCode = 200;
            RespondJson(context, result.ToString());
        }

        private static void HandleRegister(HttpListenerContext context)
        {
            var requestBody = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            var body = JObject.Parse(requestBody);

            var username = body["username"]?.ToString();
            var password = body["password"]?.ToString();

            ConsoleLogging.LogMessage($"Attempting registration for user {username}.", "AUTH");

            try
            {
                var result = UserAuth.RegisterUser(username, password);

                if (result["success"]?.Value<bool>() == true)
                {
                    ConsoleLogging.LogSuccess($"User {username} registered successfully.", "AUTH");
                    RespondJson(context, result.ToString());
                }
                else
                {
                    var backendError = result["error"]?.ToString();
                    ConsoleLogging.LogWarning($"Registration failed for {username}: {backendError}", "AUTH");

                    result["error"] = "registrationfailed";
                    RespondJson(context, result.ToString());
                }
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Registration exception for {username}: {ex.Message}", "AUTH");
                RespondJson(context, JObject.FromObject(new
                {
                    success = false, error = "internalError"
                }).ToString());
            }
        }

        private static void AddCORSHeaders(HttpListenerResponse response)
        {
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
        }

        private static void RespondJson(HttpListenerContext context, string json)
        {
            AddCORSHeaders(context.Response);
            var buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }

        public static void RespondText(HttpListenerContext context, string text)
        {
            AddCORSHeaders(context.Response);
            var buffer = Encoding.UTF8.GetBytes(text);
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
            var buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }
    }
}