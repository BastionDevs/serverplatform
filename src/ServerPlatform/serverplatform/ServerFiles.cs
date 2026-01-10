using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
}
