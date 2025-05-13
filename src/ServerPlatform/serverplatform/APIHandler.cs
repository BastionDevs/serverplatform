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
                Console.WriteLine($"[HttpListener] Backend API started and listening on port {port}.");

                while (!token.IsCancellationRequested)
                {
                    var contextTask = listener.GetContextAsync();
                    var completedTask = await Task.WhenAny(contextTask, Task.Delay(-1, token));

                    if (completedTask == contextTask)
                    {
                        HttpListenerContext context = contextTask.Result;
                        _ = Task.Run(() => HandleRequest(context)); // Handle each request separately
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"[HttpListener] Listener exception: {ex.Message}");
            }
            finally
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                    listener.Close();
                }
                Console.WriteLine("[HttpListener] Listener closed.");
            }
        }

        public static void HandleRequest(HttpListenerContext context)
        {
            try
            {
                // Handle CORS preflight request (OPTIONS)
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    AddCORSHeaders(context.Response);
                    context.Response.StatusCode = 204; // No content
                    context.Response.Close();
                    return;
                }

                Console.WriteLine($"Incoming {context.Request.HttpMethod} request for {context.Request.Url.AbsolutePath}");

                if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/mcservers/start")
                {
                    string requestBody = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                    Console.WriteLine($"[API] Starting server {JObject.Parse(requestBody)["id"]}");

                    RespondJson(context, JObject.FromObject(new { status = "Server started" }).ToString());
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/mcservers/stop")
                {
                    string requestBody = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                    Console.WriteLine($"[API] Stopping server {JObject.Parse(requestBody)["id"]}");

                    RespondJson(context, JObject.FromObject(new { status = "Server stopped" }).ToString());
                }
                else if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/")
                {
                    Console.WriteLine("Website");

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
                        RespondJson(context, result.ToString());
                    }
                    catch (Exception ex)
                    {
                        ConsoleLogging.LogError($"Authentication error: {ex.Message}");
                        RespondJson(context, JObject.FromObject(new { success = false, error = "internalError" }).ToString());
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                    RespondJson(context, JObject.FromObject(new { error = "Not Found" }).ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
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
