using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                Console.WriteLine($"Incoming {context.Request.HttpMethod} request for {context.Request.Url.AbsolutePath}");

                if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/mcservers/start")
                {
                    string requestBody = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                    Console.WriteLine($"[API] Starting server {JObject.Parse(requestBody)["id"]}");

                    RespondJson(context, "{\"status\":\"Server started\"}");
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/mcservers/stop")
                {
                    string requestBody = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                    Console.WriteLine($"[API] Stopping server {JObject.Parse(requestBody)["id"]}");

                    RespondJson(context, "{\"status\":\"Server stopped\"}");
                }
                else if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/")
                {
                    Console.WriteLine("website");

                    RespondHTML(context, "<p>Backend server</p>");
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
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }

        public static void RespondText(HttpListenerContext context, string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }

        public static void RespondHTML(HttpListenerContext context, string text)
        {
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
