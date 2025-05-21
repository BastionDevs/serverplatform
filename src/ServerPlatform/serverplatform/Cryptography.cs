using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace serverplatform
{
    internal class Cryptography
    {
        public static string Sha256Hash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string Sha256FileHash(string filePath)
        {
            using (var sha256 = SHA256.Create()) // Use SHA256 hashing algorithm
            using (var fileStream = File.OpenRead(filePath))
            {
                var hashBytes = sha256.ComputeHash(fileStream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower(); // Convert to a lowercase hex string
            }
        }
    }
}