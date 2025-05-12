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
                    context.Response.AddHeader("Access-Control-Allow-Origin", "*");
                    context.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
                    context.Response.StatusCode = 204; // No content
                    context.Response.Close();
                    return;
                }

                Console.WriteLine($"Incoming {context.Request.HttpMethod} request for {context.Request.Url.AbsolutePath}");

                if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/mcservers/start")
                {
                    string requestBody = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                    Console.WriteLine($"[API] Starting server {JObject.Parse(requestBody)["id"]}");

                    RespondJson(context, "{\"status\":\"Server started\"}");
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/mcservers/stop")
                {
                    string requestBody = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                    Console.WriteLine($"[API] Stopping server {JObject.Parse(requestBody)["id"]}");

                    RespondJson(context, "{\"status\":\"Server stopped\"}");
                }
                else if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/")
                {
                    Console.WriteLine("Website");

                    RespondHTML(context, "<p>Server Platform Backend server</p><p>We recommend that you only port-forward the Frontend to prevent any intrusions.</p><br><p>Made with &#10084;&#65039;</p><p>&copy; 2025 BastionSG</p>");
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/auth")
                {
                    string requestBody = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                    ConsoleLogging.LogMessage($"User {JObject.Parse(requestBody)["username"]} attempting to authenticate...", "Authentication");

                    string token = "";
                    try
                    {
                        token = UserAuth.AuthenticateUser(JObject.Parse(requestBody)["username"].ToString(), JObject.Parse(requestBody)["password"].ToString());
                    }
                    catch (Exception ex)
                    {
                        token = ex.Message;
                    }
                    finally
                    {
                        RespondText(context, token);
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                    RespondText(context, "Not Found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                context.Response.StatusCode = 500;
                RespondText(context, "Internal Server Error");
            }
        }

        public static void RespondJson(HttpListenerContext context, string json)
        {
            context.Response.AddHeader("Access-Control-Allow-Origin", "*"); // Allow all origins
            context.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS"); // Allow specific HTTP methods
            context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization"); // Allow specific headers
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }

        public static void RespondText(HttpListenerContext context, string text)
        {
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");
            context.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }

        public static void RespondHTML(HttpListenerContext context, string text)
        {
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");
            context.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
            context.Response.StatusCode = 200;
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }
    }
}
