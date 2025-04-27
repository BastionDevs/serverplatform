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
    internal class APIHandler
    {
        public static async Task StartServer(int port)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{port}/"); // Listen on port for all IP addresses
            listener.Start();
            Console.WriteLine($"Backend API listening on port {port}.");

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context)); // Handle each request in its own task
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
