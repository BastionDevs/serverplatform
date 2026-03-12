using HttpMultipartParser;
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
    internal sealed class FileEntry
    {
        public string RelativePath { get; private set; }
        public string Name { get; private set; }
        public bool IsDirectory { get; private set; }
        public long Size { get; private set; }
        public DateTime Modified { get; private set; }

        private FileEntry() { }

        public static FileEntry FromPath(string root, string fullPath)
        {
            string relative = fullPath
                .Substring(root.Length)
                .TrimStart(System.IO.Path.DirectorySeparatorChar);

            bool isDir = Directory.Exists(fullPath);

            var entry = new FileEntry
            {
                RelativePath = relative.Replace('\\', '/'),
                Name = System.IO.Path.GetFileName(fullPath),
                IsDirectory = isDir
            };

            if (isDir)
            {
                entry.Size = 0;
                entry.Modified = Directory.GetLastWriteTimeUtc(fullPath);
            }
            else
            {
                var info = new FileInfo(fullPath);
                entry.Size = info.Length;
                entry.Modified = info.LastWriteTimeUtc;
            }

            return entry;
        }
    }

    internal static class ServerFiles
    {
        private static readonly string ServersDir =
            Config.GetConfig("ServersDir", "main");

        // ------------------------------------------------
        // PATH HELPERS
        // ------------------------------------------------
        private static string GetServerFilesRoot(string serverId)
        {
            return Path.GetFullPath(
                Path.Combine(ServersDir, serverId, "files")
            );
        }

        private static string ResolveSafePath(string serverId, string relativePath)
        {
            if (relativePath == null)
                relativePath = "";

            relativePath = relativePath.Replace('\\', '/').TrimStart('/');

            string root = GetServerFilesRoot(serverId);

            string fullPath = Path.GetFullPath(
                Path.Combine(root, relativePath)
            );

            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Invalid file path.");

            return fullPath;
        }

        // ------------------------------------------------
        // LIST FILES
        // ------------------------------------------------
        public static IEnumerable<FileEntry> List(
            string serverId,
            string path,
            bool recursive)
        {
            string root = ResolveSafePath(serverId, path);

            if (!Directory.Exists(root))
                return Enumerable.Empty<FileEntry>();

            SearchOption option = recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var result = new List<FileEntry>();

            foreach (var p in Directory.EnumerateFileSystemEntries(root, "*", option))
            {
                result.Add(FileEntry.FromPath(
                    GetServerFilesRoot(serverId), p
                ));
            }

            return result;
        }

        // ------------------------------------------------
        // READ TEXT FILE
        // ------------------------------------------------
        public static Task<string> ReadTextAsync(string serverId, string path)
        {
            return Task.Run(() =>
            {
                string full = ResolveSafePath(serverId, path);

                using (var reader = new StreamReader(full))
                    return reader.ReadToEnd();
            });
        }

        // ------------------------------------------------
        // WRITE TEXT FILE
        // ------------------------------------------------
        public static Task WriteTextAsync(
            string serverId,
            string path,
            string content)
        {
            return Task.Run(() =>
            {
                string full = ResolveSafePath(serverId, path);

                string dir = Path.GetDirectoryName(full);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var writer = new StreamWriter(full, false))
                    writer.Write(content);
            });
        }

        // ------------------------------------------------
        // UPLOAD FILE (STREAM)
        // ------------------------------------------------
        public static Task UploadAsync(
            string serverId,
            string path,
            Stream input,
            bool overwrite)
        {
            return Task.Run(() =>
            {
                string full = ResolveSafePath(serverId, path);

                if (File.Exists(full) && !overwrite)
                    throw new IOException("File already exists.");

                string dir = Path.GetDirectoryName(full);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var fs = new FileStream(
                    full,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    input.CopyTo(fs);
                }
            });
        }

        // ------------------------------------------------
        // DOWNLOAD FILE
        // ------------------------------------------------
        public static Stream OpenRead(string serverId, string path)
        {
            string full = ResolveSafePath(serverId, path);
            return new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        // ------------------------------------------------
        // DELETE FILE OR DIRECTORY
        // ------------------------------------------------
        public static void Delete(string serverId, string path)
        {
            string full = ResolveSafePath(serverId, path);

            if (File.Exists(full))
            {
                File.Delete(full);
                return;
            }

            if (Directory.Exists(full))
            {
                Directory.Delete(full, true);
            }
        }

        // ------------------------------------------------
        // CREATE DIRECTORY
        // ------------------------------------------------
        public static void CreateDirectory(string serverId, string path)
        {
            string full = ResolveSafePath(serverId, path);
            Directory.CreateDirectory(full);
        }

        // ------------------------------------------------
        // MOVE / RENAME
        // ------------------------------------------------
        public static void Move(
            string serverId,
            string from,
            string to,
            bool overwrite)
        {
            string src = ResolveSafePath(serverId, from);
            string dst = ResolveSafePath(serverId, to);

            string dir = Path.GetDirectoryName(dst);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(src))
            {
                if (File.Exists(dst))
                {
                    if (!overwrite)
                        throw new IOException("Target exists.");

                    File.Delete(dst);
                }

                File.Move(src, dst);
                return;
            }

            if (Directory.Exists(src))
            {
                if (Directory.Exists(dst))
                    throw new IOException("Target directory exists.");

                Directory.Move(src, dst);
            }
        }
    }

    internal class ServerFilesHandler
    {
        //HELPER
        private static string Escape(string s)
        {
            if (s == null) return "";
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        //HANDLERS
        public static void HandleListFiles(HttpListenerContext context)
        {
            // 1. Authenticate
            var principal = UserAuth.VerifyJwtFromContext(context);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"message\":\"Unauthorised.\"}");
                return;
            }

            string username = UserAuth.GetUsernameFromPrincipal(principal);
            var qs = context.Request.QueryString;

            string serverId = qs["id"];
            string path = qs["path"] ?? "";
            bool recursive = string.Equals(qs["recursive"], "true", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(serverId))
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"missingServerId\"}");
                return;
            }

            // 2. Ownership check
            var serverIndex = Config.serverIndex;
            bool ownsServer = serverIndex
                .GetServersForUser(username)
                .Any(s => s.Id.Equals(serverId, StringComparison.OrdinalIgnoreCase));

            if (!ownsServer)
            {
                context.Response.StatusCode = 404;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"serverNotFound\"}");
                return;
            }

            try
            {
                var entries = ServerFiles.List(serverId, path, recursive);

                var sb = new StringBuilder();
                sb.Append("{\"success\":true,\"entries\":[");

                bool first = true;
                foreach (var e in entries)
                {
                    if (!first) sb.Append(",");
                    first = false;

                    sb.Append("{");
                    sb.AppendFormat("\"path\":\"{0}\",", Escape(e.RelativePath));
                    sb.AppendFormat("\"name\":\"{0}\",", Escape(e.Name));
                    sb.AppendFormat("\"isDirectory\":{0},", e.IsDirectory.ToString().ToLowerInvariant());
                    sb.AppendFormat("\"size\":{0},", e.Size);
                    sb.AppendFormat("\"modified\":\"{0:O}\"", e.Modified);
                    sb.Append("}");
                }

                sb.Append("]}");

                ApiHandler.RespondJson(context, sb.ToString());
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError(
                    $"File list failed for {serverId}: {ex.Message}",
                    "ServerFiles");

                context.Response.StatusCode = 500;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"internalError\"}");
            }
        }

        public static void HandleReadFile(HttpListenerContext context)
        {
            var principal = UserAuth.VerifyJwtFromContext(context);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"message\":\"Unauthorised.\"}");
                return;
            }

            string username = UserAuth.GetUsernameFromPrincipal(principal);
            var qs = context.Request.QueryString;

            string serverId = qs["id"];
            string path = qs["path"];

            if (string.IsNullOrWhiteSpace(serverId) || string.IsNullOrWhiteSpace(path))
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"missingParameters\"}");
                return;
            }

            bool ownsServer = Config.serverIndex
                .GetServersForUser(username)
                .Any(s => s.Id.Equals(serverId, StringComparison.OrdinalIgnoreCase));

            if (!ownsServer)
            {
                context.Response.StatusCode = 404;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"serverNotFound\"}");
                return;
            }

            try
            {
                string content = ServerFiles
                    .ReadTextAsync(serverId, path)
                    .GetAwaiter()
                    .GetResult();

                ApiHandler.RespondJson(context,
                    "{\"success\":true,\"content\":\"" + Escape(content) + "\"}");
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError(
                    $"File read failed for {serverId}:{path} - {ex.Message}",
                    "ServerFiles");

                context.Response.StatusCode = 500;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"internalError\"}");
            }
        }

        public static void HandleWriteFile(HttpListenerContext context)
        {
            var principal = UserAuth.VerifyJwtFromContext(context);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"message\":\"Unauthorised.\"}");
                return;
            }

            string username = UserAuth.GetUsernameFromPrincipal(principal);

            string bodyText;
            using (var reader = new StreamReader(
                context.Request.InputStream,
                context.Request.ContentEncoding))
            {
                bodyText = reader.ReadToEnd();
            }

            JObject body;
            try { body = JObject.Parse(bodyText); }
            catch
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"invalidJson\"}");
                return;
            }

            string serverId = body["id"]?.ToString();
            string path = body["path"]?.ToString();
            string content = body["content"]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(serverId) || string.IsNullOrWhiteSpace(path))
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"missingParameters\"}");
                return;
            }

            bool ownsServer = Config.serverIndex
                .GetServersForUser(username)
                .Any(s => s.Id.Equals(serverId, StringComparison.OrdinalIgnoreCase));

            if (!ownsServer)
            {
                context.Response.StatusCode = 404;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"serverNotFound\"}");
                return;
            }

            try
            {
                ServerFiles.WriteTextAsync(serverId, path, content)
                    .GetAwaiter()
                    .GetResult();

                ApiHandler.RespondJson(context,
                    "{\"success\":true}");
            }
            catch
            {
                context.Response.StatusCode = 500;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"internalError\"}");
            }
        }

        public static void HandleDeleteFile(HttpListenerContext context)
        {
            var principal = UserAuth.VerifyJwtFromContext(context);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"message\":\"Unauthorised.\"}");
                return;
            }

            string username = UserAuth.GetUsernameFromPrincipal(principal);

            string bodyText;
            using (var reader = new StreamReader(context.Request.InputStream))
                bodyText = reader.ReadToEnd();

            JObject body;
            try { body = JObject.Parse(bodyText); }
            catch
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"invalidJson\"}");
                return;
            }

            string serverId = body["id"]?.ToString();
            string path = body["path"]?.ToString();

            if (string.IsNullOrWhiteSpace(serverId) || string.IsNullOrWhiteSpace(path))
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"missingParameters\"}");
                return;
            }

            bool ownsServer = Config.serverIndex
                .GetServersForUser(username)
                .Any(s => s.Id.Equals(serverId, StringComparison.OrdinalIgnoreCase));

            if (!ownsServer)
            {
                context.Response.StatusCode = 404;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"serverNotFound\"}");
                return;
            }

            try
            {
                ServerFiles.Delete(serverId, path);
                ApiHandler.RespondJson(context,
                    "{\"success\":true}");
            }
            catch
            {
                context.Response.StatusCode = 500;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"internalError\"}");
            }
        }

        public static void HandleDownloadFile(HttpListenerContext context)
        {
            string serverId = context.Request.QueryString["id"];
            string path = context.Request.QueryString["path"];
            string token = context.Request.QueryString["token"];

            if (string.IsNullOrWhiteSpace(serverId) || string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(token))
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"missingParameters\"}");
                return;
            }

            var principal = UserAuth.ValidateJwtToken(token);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"unauthorised\"}");
                return;
            }

            try
            {
                using (var stream = ServerFiles.OpenRead(serverId, path))
                {
                    context.Response.ContentType = "application/octet-stream";
                    context.Response.AddHeader(
                        "Content-Disposition",
                        $"attachment; filename=\"{Path.GetFileName(path)}\""
                    );

                    stream.CopyTo(context.Response.OutputStream);
                    context.Response.OutputStream.Flush();
                    context.Response.Close();
                }
            }
            catch (FileNotFoundException)
            {
                context.Response.StatusCode = 404;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"fileNotFound\"}");
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"internalError\"}");
            }
        }

        public static void HandleCreateDirectory(HttpListenerContext context)
        {
            var principal = UserAuth.VerifyJwtFromContext(context);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"unauthorised\"}");
                return;
            }

            JObject body;
            try
            {
                using (var r = new StreamReader(context.Request.InputStream))
                    body = JObject.Parse(r.ReadToEnd());
            }
            catch
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"invalidJson\"}");
                return;
            }

            string serverId = body["id"]?.ToString();
            string path = body["path"]?.ToString();

            if (string.IsNullOrWhiteSpace(serverId) || string.IsNullOrWhiteSpace(path))
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"missingParameters\"}");
                return;
            }

            try
            {
                ServerFiles.CreateDirectory(serverId, path);
                ApiHandler.RespondJson(context,
                    "{\"success\":true}");
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"internalError\"}");
            }
        }

        public static void HandleMoveFile(HttpListenerContext context)
        {
            var principal = UserAuth.VerifyJwtFromContext(context);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"unauthorised\"}");
                return;
            }

            JObject body;
            try
            {
                using (var r = new StreamReader(context.Request.InputStream))
                    body = JObject.Parse(r.ReadToEnd());
            }
            catch
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"invalidJson\"}");
                return;
            }

            string serverId = body["id"]?.ToString();
            string from = body["from"]?.ToString();
            string to = body["to"]?.ToString();
            bool overwrite = body["overwrite"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrWhiteSpace(serverId) ||
                string.IsNullOrWhiteSpace(from) ||
                string.IsNullOrWhiteSpace(to))
            {
                context.Response.StatusCode = 400;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"missingParameters\"}");
                return;
            }

            try
            {
                ServerFiles.Move(serverId, from, to, overwrite);
                ApiHandler.RespondJson(context,
                    "{\"success\":true}");
            }
            catch (IOException ex)
            {
                context.Response.StatusCode = 409;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"targetExists\"}");
            }
            catch (Exception)
            {
                context.Response.StatusCode = 500;
                ApiHandler.RespondJson(context,
                    "{\"success\":false,\"error\":\"internalError\"}");
            }
        }

        public static async Task HandleFileUpload(HttpListenerContext ctx)
        {
            try
            {
                // --- auth ---
                var principal = UserAuth.VerifyJwtFromContext(ctx);
                if (principal == null)
                {
                    ctx.Response.StatusCode = 401;
                    ApiHandler.RespondJson(ctx,
                        "{\"success\":false,\"message\":\"Unauthorized\"}");
                    return;
                }

                // --- content type check ---
                if (!ctx.Request.ContentType.StartsWith("multipart/form-data"))
                    throw new InvalidOperationException("Invalid content type.");

                // --- parse multipart ---
                var form = MultipartFormDataParser.Parse(ctx.Request.InputStream);

                string serverId = form.GetParameterValue("id");
                string path = form.GetParameterValue("path");
                bool overwrite = false;

                if (form.HasParameter("overwrite"))
                    bool.TryParse(
                        form.GetParameterValue("overwrite"),
                        out overwrite);

                var file = form.Files.FirstOrDefault();
                if (file == null)
                    throw new InvalidOperationException("No file provided.");

                // --- determine final path ---
                string targetPath = path;
                if (!path.EndsWith("/"))
                    targetPath = path;
                else
                    targetPath = path + file.FileName;

                // --- upload ---
                await ServerFiles.UploadAsync(
                    serverId,
                    targetPath,
                    file.Data,
                    overwrite);

                ApiHandler.RespondJson(ctx, "{\"success\":true}");
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 400;
                ApiHandler.RespondJson(ctx,
                    "{\"success\":false,\"message\":\"" +
                    Escape(ex.Message) +
                    "\"}");
            }
        }

    }
}
