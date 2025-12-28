using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace serverplatform
{
    internal class ApiHandler
    {
        public static async Task StartServer(CancellationToken token, int port)
        {
            string prefix = $"http://+:{port}/";

            string win_userdomain = Environment.GetEnvironmentVariable("USERDOMAIN");
            string win_username = Environment.GetEnvironmentVariable("USERNAME");

            string win_userstring = $"{win_userdomain}\\{win_username}";

            if (!HandleACL.UrlAclExists(prefix))
            {
                ConsoleLogging.LogError("URL ACL for Server Platform backend endpoint is not registered.", "Listener");
                ConsoleLogging.LogWarning("Server Platform will now register the URL ACL. Please press \"Yes\" on the UAC.", "Listener");
                ConsoleLogging.LogMessage("Press Enter to register...", "Listener");
                Console.ReadLine();

                HandleACL.AddUrlAcl(prefix, win_userstring);

                bool success = HandleACL.UrlAclExists(prefix);
                if (!success)
                {
                    ConsoleLogging.LogError("An error occured, and Server Platform was not able to register the URL ACL.", "Listener");
                    ConsoleLogging.LogError($"Please run `netsh http add urlacl url={prefix} user={win_userstring}` in an elevated shell to manually register it.", "Listener");
                } else
                {
                    ConsoleLogging.LogSuccess("Successfully created URL ACL.", "Listener");
                }
            }

            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                ConsoleLogging.LogMessage("Starting HttpListener...", "Listener");
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
                ConsoleLogging.LogMessage("Ctrl+C intercepted. Shutting down API listener...", "Listener");
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

                ConsoleLogging.LogMessage("API listener stopped.", "Listener");
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            try
            {
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    AddCorsHeaders(context.Response);
                    context.Response.StatusCode = 204;
                    context.Response.Close();
                    ConsoleLogging.LogMessage($"Handled CORS preflight request for {context.Request.Url.AbsolutePath}",
                        "API");
                    return;
                }

                ConsoleLogging.LogMessage(
                    $"Incoming {context.Request.HttpMethod} request for {context.Request.Url.AbsolutePath}", "API");

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
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/servers/create")
                {
                    ServerCreation.HandleCreationRequest(context);
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/profile/public")
                {
                    HandleProfile(context);
                }
                else if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/endpointinfo")
                {
                    RespondJson(context, JObject.FromObject(new
                    {
                        server = "BSP Backend Server",
                        version = "1.0.0"
                    }).ToString());
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
            var requestBody =
                new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            var body = JObject.Parse(requestBody);

            var username = body["username"]?.ToString();
            var password = body["password"]?.ToString();

            ConsoleLogging.LogMessage($"User {username} is attempting to authenticate.", "AUTH");

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
                    ConsoleLogging.LogWarning($"User {username} failed to authenticate: {backendError}", "AUTH");

                    result["error"] = "incorrectusrorpwd";
                    RespondJson(context, result.ToString());
                }
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Exception occured while trying to authenticate user {username}: {ex.Message}", "AUTH");
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
                ConsoleLogging.LogWarning("Missing or invalid Authorization header in logout request.", "AUTH");
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

            var result = UserAuth.LogoutUser(token);

            context.Response.StatusCode = 200;
            RespondJson(context, result.ToString());
        }

        private static void HandleRegister(HttpListenerContext context)
        {
            var requestBody =
                new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            var body = JObject.Parse(requestBody);

            var username = body["username"]?.ToString();
            var password = body["password"]?.ToString();

            ConsoleLogging.LogMessage($"User is attempting to register with username {username}.", "AUTH");

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
                    ConsoleLogging.LogWarning($"User {username} failed to register: {backendError}", "AUTH");

                    result["error"] = "registrationfailed";
                    RespondJson(context, result.ToString());
                }
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Exception occured while trying to register user {username}: {ex.Message}", "AUTH");
                RespondJson(context, JObject.FromObject(new
                {
                    success = false, error = "internalError"
                }).ToString());
            }
        }

        private static void HandleProfile(HttpListenerContext context)
        {
            var requestBody =
                new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            var body = JObject.Parse(requestBody);

            var username = body["username"]?.ToString();

            if (File.ReadAllText("users.json").Contains(username))
            {
                var json = File.ReadAllText("users.json");
                var usersArray = JArray.Parse(json);

                var user = usersArray
                    .FirstOrDefault(u =>
                        string.Equals((string)u["Username"], username, StringComparison.OrdinalIgnoreCase));

                if (user == null)
                {
                    ConsoleLogging.LogWarning($"User not found: {username}", "PROFILES");
                    RespondJson(context, JObject.FromObject(new
                    {
                        success = false,
                        error = "userNotFound"
                    }).ToString());
                }

                var userProfile = (JObject)user.DeepClone();
                userProfile.Remove("PasswordHash");

                var profileObj = JObject.FromObject(new
                {
                    success = true,
                    profile = userProfile
                });

                RespondJson(context, profileObj.ToString());
            } else
            {
                ConsoleLogging.LogError($"User attempted to view non-existent profile.", "PROFILES");
                RespondJson(context, JObject.FromObject(new
                {
                    success = false,
                    error = "profileNotFound"
                }).ToString());
            }
        }

        private static void AddCorsHeaders(HttpListenerResponse response)
        {
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
        }

        public static void RespondJson(HttpListenerContext context, string json)
        {
            AddCorsHeaders(context.Response);
            var buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }

        public static void RespondText(HttpListenerContext context, string text)
        {
            AddCorsHeaders(context.Response);
            var buffer = Encoding.UTF8.GetBytes(text);
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }

        public static void RespondHtml(HttpListenerContext context, string html)
        {
            AddCorsHeaders(context.Response);
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