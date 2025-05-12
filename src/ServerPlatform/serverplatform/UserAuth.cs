using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace serverplatform
{
    internal class UserAuth
    {
        class User
        {
            public string Username { get; set; }
            public string PasswordHash { get; set; }
        }

        static Dictionary<string, string> accessTokens = new Dictionary<string, string>();
        public static void CreateDefaultUsers()
        {
            var sw = new StreamWriter("users.json", false);
            sw.WriteLine("[");
            sw.WriteLine("    {");
            sw.WriteLine("        \"Username\": \"admin\",");
            sw.WriteLine("        \"PasswordHash\": \"240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9\"");
            sw.WriteLine("    }");
            sw.WriteLine("]");
            sw.Close();
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
            if (!File.Exists("users.json"))
            {
                ConsoleLogging.LogError("users.json not found.");
                return "serverError";
            }

            string json = File.ReadAllText("users.json");

            if (string.IsNullOrWhiteSpace(json))
            {
                ConsoleLogging.LogError("users.json is empty.");
                return "serverError";
            }

            List<User> users = JsonConvert.DeserializeObject<List<User>>(json);

            if (users == null)
            {
                ConsoleLogging.LogWarning("Deserialization returned null. Check JSON format.", "AUTH");
                return "serverError";
            }

            foreach (var user in users)
            {
                if (user.Username == username)
                {
                    if (user.PasswordHash == SHA256Hash(password))
                    {
                        if (accessTokens.ContainsKey(username))
                        {
                            ConsoleLogging.LogMessage($"User {username} already has token.", "AUTH");
                            string alrdygeneratedtoken = "";
                            accessTokens.TryGetValue(username, out alrdygeneratedtoken);
                            return alrdygeneratedtoken;
                        }

                        string accessToken;
                        do
                        {
                            accessToken = RandomString(15);
                        } while (accessTokens.ContainsValue(accessToken));

                        accessTokens.Add(username, accessToken);
                        return accessToken;
                    }
                    else
                    {
                        ConsoleLogging.LogWarning($"User {username} failed to authenticate: Incorrect password", "AUTH");
                        return "wrongPassword";
                    }
                }
            }

            ConsoleLogging.LogWarning($"User {username} not found", "AUTH");
            return "userNotFound";
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
