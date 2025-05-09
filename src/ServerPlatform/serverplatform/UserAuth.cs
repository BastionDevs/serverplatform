using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace serverplatform
{
    internal class UserAuth
    {
        static Dictionary<string, string> accessTokens;
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
        public static string SHA256Hash(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string AuthenticateUser(string username, string password)
        {
            string json = File.ReadAllText("users.json");
            JArray users = JArray.Parse(json);

            if (users.FirstOrDefault(u => (string)u["username"] == username)?["passwordHash"]?.ToString() == SHA256Hash(password))
            {
                if (accessTokens.ContainsKey(username))
                {
                    return accessTokens[username];
                }

                string accessToken = RandomString(15);
                if (accessTokens.ContainsValue(accessToken))
                {
                    accessToken = RandomString(15);
                    if (accessTokens.ContainsValue(accessToken))
                    {
                        accessToken = RandomString(15);
                    }
                }

                accessTokens[username] = accessToken;

                return accessToken;
            }
            else
            {
                throw new Exception("invalidUserOrPwd");
            }
        }

        //Credit https://github.com/tylerablake/randomStringGenerator
        //To generate a random alphanumeric string of a specified length
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray()).ToLower();
        }
    }
}
