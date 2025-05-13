using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;



namespace serverplatform
{
    internal class UserAuth
    {
        class User
        {
            public string Username { get; set; }
            public string PasswordHash { get; set; }
        }

        private static readonly string jwtSecret = LoadJwtSecret();

        private static string LoadJwtSecret()
        {
            try
            {
                string jsonContent = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));
                JObject config = JObject.Parse(jsonContent);

                // Access the Jwt Secret from the loaded JSON
                var secret = config["Jwt"]?["Secret"]?.ToString();

                if (string.IsNullOrEmpty(secret))
                {
                    throw new Exception("JWT Secret is missing in appsettings.json");
                }

                return secret;
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Error loading JWT Secret: {ex.Message}");
                throw;  // Rethrow the exception to maintain the stack trace
            }
        }


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
                        string token = GenerateJwtToken(username);
                        return JObject.FromObject(new { success = true, token = token });
                    }

                    ConsoleLogging.LogWarning($"User {username} failed to authenticate: Incorrect password", "AUTH");
                    return JObject.FromObject(new { success = false, error = "wrongPassword" });
                }
            }

            ConsoleLogging.LogWarning($"User {username} not found", "AUTH");
            return JObject.FromObject(new { success = false, error = "userNotFound" });
        }

        private static string GenerateJwtToken(string username)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwtSecret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, username)
                }),
                Expires = DateTime.UtcNow.AddHours(1), // Adjust as needed
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public static ClaimsPrincipal ValidateJwtToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwtSecret);

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero // Optional: reduce default 5-min tolerance
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, validationParams, out SecurityToken validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }

    }
}
