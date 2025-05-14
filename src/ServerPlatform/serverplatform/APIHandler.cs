using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace serverplatform
{
    internal class APIHandler
    {
        public static async Task StartServer(CancellationToken token, int port)
        {
            var listener = new HttpListener();
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
                        var context = contextTask.Result;
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

                // Handle modular /auth endpoints
                if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath.StartsWith("/auth"))
                {
                    var pathParts = context.Request.Url.AbsolutePath.Split('/');

                    // Check the subpath after '/auth'
                    if (pathParts.Length == 3 && pathParts[2] == "login")
                    {
                        HandleLogin(context);
                    }
                    else if (pathParts.Length == 3 && pathParts[2] == "logout")
                    {
                        HandleLogout(context);
                    }
                    // else if (pathParts.Length == 3 && pathParts[2] == "register")
                    // {
                    //     HandleRegister(context);
                    // }
                    else
                    {
                        context.Response.StatusCode = 404;
                        ConsoleLogging.LogWarning($"404 - Endpoint not found: {context.Request.Url.AbsolutePath}",
                            "API");
                        RespondJson(context, JObject.FromObject(new { error = "Not Found" }).ToString());
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

        private static void HandleLogin(HttpListenerContext context)
        {
            var requestBody =
                new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            var body = JObject.Parse(requestBody);

            var username = body["username"]?.ToString();
            var password = body["password"]?.ToString();

            ConsoleLogging.LogMessage($"User {username} attempting to authenticate...", "Authentication");

            try
            {
                var result = UserAuth.AuthenticateUser(username, password);

                if (result["success"]?.Value<bool>() == true)
                {
                    ConsoleLogging.LogSuccess($"User {username} successfully authenticated.", "Authentication");
                    RespondJson(context, result.ToString());
                }
                else
                {
                    var backendError = result["error"]?.ToString();
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

        private static void HandleLogout(HttpListenerContext context)
        {
            // Implement logout functionality (e.g., invalidate JWT token, etc.)
            var authHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                context.Response.StatusCode = 401;
                RespondJson(context,
                    JObject.FromObject(new { error = "Missing or invalid Authorization header" }).ToString());
                return;
            }

            var token = authHeader.Substring("Bearer ".Length);
            var principal = UserAuth.ValidateJwtToken(token);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                RespondJson(context, JObject.FromObject(new { error = "Invalid or expired token" }).ToString());
                return;
            }

            // Invalidate the token (this depends on your JWT strategy, e.g., token blacklisting)
            // For simplicity, you may just log it for now or set an expiration.

            ConsoleLogging.LogSuccess("User successfully logged out", "Authentication");

            RespondJson(context,
                JObject.FromObject(new { success = true, message = "Successfully logged out" }).ToString());
        }

        // private static void HandleRegister(HttpListenerContext context)
        // {
        //     // Implement registration functionality (e.g., create a new user)
        //     string requestBody = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        //     JObject body = JObject.Parse(requestBody);
        //
        //     string username = body["username"]?.ToString();
        //     string password = body["password"]?.ToString();
        //     string email = body["email"]?.ToString();
        //
        //     ConsoleLogging.LogMessage($"Attempting to register user {username}...", "Authentication");
        //
        //     try
        //     {
        //         // Registration logic (e.g., validate, hash password, create user)
        //         var result = UserAuth.RegisterUser(username, password, email);
        //
        //         if (result["success"]?.Value<bool>() == true)
        //         {
        //             ConsoleLogging.LogSuccess($"User {username} successfully registered.", "Authentication");
        //             RespondJson(context, result.ToString());
        //         }
        //         else
        //         {
        //             string backendError = result["error"]?.ToString();
        //             ConsoleLogging.LogWarning($"User {username} failed to register: {backendError}", "AUTH");
        //
        //             // Send only a generic error to the client
        //             result["error"] = "registrationfailed";
        //             RespondJson(context, result.ToString());
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         ConsoleLogging.LogError($"Registration error: {ex.Message}", "AUTH");
        //         RespondJson(context, JObject.FromObject(new { success = false, error = "internalError" }).ToString());
        //     }
        // }

        public static void AddCORSHeaders(HttpListenerResponse response)
        {
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
        }

        public static void RespondJson(HttpListenerContext context, string json)
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