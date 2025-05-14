using System;
using System.IO;
using System.Security.Cryptography;

namespace serverplatform
{
    internal class ServerCreation
    {
        public static string CalculateSHA256Hash(string filePath)
        {
            using (var sha256 = SHA256.Create()) // Use SHA256 hashing algorithm
            using (var fileStream = File.OpenRead(filePath))
            {
                var hashBytes = sha256.ComputeHash(fileStream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower(); // Convert to a lowercase hex string
            }
        }

        public static void CreateServer(string name, string description)
        {
            var serversFolder = Config.GetConfig("ServersDir", "main");

            if (Directory.Exists($@"{serversFolder}\\{name}")) throw new Exception("Folder already exists.");
        }
    }
}