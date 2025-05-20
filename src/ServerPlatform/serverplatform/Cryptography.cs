using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace serverplatform
{
    internal class Cryptography
    {
        public static string SHA256Hash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string SHA256FileHash(string filePath)
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
