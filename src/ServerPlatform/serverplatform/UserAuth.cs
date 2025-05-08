using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace serverplatform
{
    internal class UserAuth
    {
        public static void CreateDefaultUsers()
        {
            var sw = new StreamWriter("users.json", true);
            sw.WriteLine("[");
            sw.WriteLine("    {");
            sw.WriteLine("        \"Username\": \"admin\",");
            sw.WriteLine("        \"PasswordHash\": \"240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9\"");
            sw.WriteLine("    }");
            sw.WriteLine("]");
        }

        public static string SHA256Hash(string password, bool nodash)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                byte[] hash = sha.ComputeHash(bytes);

                if (nodash)
                {
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                } else
                {
                    return BitConverter.ToString(hash).ToLowerInvariant();
                }
            }
        }
    }
}
