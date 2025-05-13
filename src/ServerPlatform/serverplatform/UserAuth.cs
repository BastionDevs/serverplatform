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

        public static JObject AuthenticateUser(string username, string password)
        {
            if (!File.Exists("users.json"))
            {
                ConsoleLogging.LogError("users.json not found.");
                return JObject.FromObject(new { success = false, error = "serverError" });
            }

            string json = File.ReadAllText("users.json");

            if (string.IsNullOrWhiteSpace(json))
            {
                ConsoleLogging.LogError("users.json is empty.");
                return JObject.FromObject(new { success = false, error = "serverError" });
            }

            List<User> users = JsonConvert.DeserializeObject<List<User>>(json);
            if (users == null)
            {
                ConsoleLogging.LogWarning("Deserialization returned null. Check JSON format.", "AUTH");
                return JObject.FromObject(new { success = false, error = "serverError" });
            }

            foreach (var user in users)
            {
                if (user.Username == username)
                {
                    if (user.PasswordHash == SHA256Hash(password))
                    {
                        string token;
                        if (!accessTokens.TryGetValue(username, out token))
                        {
                            do
                            {
                                token = RandomString(15);
                            } while (accessTokens.ContainsValue(token));

                            accessTokens.Add(username, token);
                        }

                        return JObject.FromObject(new { success = true, token = token });
                    }

                    ConsoleLogging.LogWarning($"User {username} failed to authenticate: Incorrect password", "AUTH");
                    return JObject.FromObject(new { success = false, error = "wrongPassword" });
                }
            }

            ConsoleLogging.LogWarning($"User {username} not found", "AUTH");
            return JObject.FromObject(new { success = false, error = "userNotFound" });
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
