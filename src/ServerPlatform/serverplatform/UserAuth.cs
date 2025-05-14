using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace serverplatform
{
    internal class UserAuth
    {
        // Lazy-loaded JWT secret
        private static string _jwtSecret;

        // In-memory blacklist for invalidated tokens
        private static readonly HashSet<string> _blacklistedTokens = new HashSet<string>();

        private static string JwtSecret
        {
            get
            {
                if (_jwtSecret == null) _jwtSecret = LoadJwtSecret();
                return _jwtSecret;
            }
        }

        private static string LoadJwtSecret()
        {
            try
            {
                var jsonContent = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));
                var config = JObject.Parse(jsonContent);

                var secret = config["Jwt"]?["Secret"]?.ToString();

                if (string.IsNullOrEmpty(secret)) throw new Exception("JWT Secret is missing in appsettings.json");

                return secret;
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Error loading JWT Secret: {ex.Message}");
                throw;
            }
        }

        public static void CreateDefaultUsers()
        {
            if (File.Exists("users.json") || File.Exists("appsettings.json"))
            {
                ConsoleLogging.LogWarning("First run has already been completed. Aborting setup.", "SETUP");
                return;
            }

            var sw = new StreamWriter("users.json", false);
            sw.WriteLine(
                "[\r\n    {\r\n        \"Username\": \"admin\",\r\n        \"PasswordHash\": \"240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9\"\r\n    }\r\n]");
            sw.Close();

            var swappsettings = new StreamWriter("appsettings.json", false);
            swappsettings.WriteLine(
                "{\r\n  \"Jwt\": {\r\n    \"Secret\": \"&&Z8dAl0!1$jxBIJMv1cUy7iaAsa#Vat\"\r\n  }\r\n}");
            swappsettings.Close();

            ConsoleLogging.LogSuccess("First run completed: Default user and appsettings.json created.");
        }

        public static string SHA256Hash(string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
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

            var json = File.ReadAllText("users.json");

            if (string.IsNullOrWhiteSpace(json))
            {
                ConsoleLogging.LogError("users.json is empty.");
                return JObject.FromObject(new { success = false, error = "serverError" });
            }

            var users = JsonConvert.DeserializeObject<List<User>>(json);
            if (users == null)
            {
                ConsoleLogging.LogWarning("Deserialization returned null. Check JSON format.", "AUTH");
                return JObject.FromObject(new { success = false, error = "serverError" });
            }

            foreach (var user in users)
                if (user.Username == username)
                {
                    if (user.PasswordHash == SHA256Hash(password))
                    {
                        var token = GenerateJwtToken(username);
                        return JObject.FromObject(new { success = true, token });
                    }

                    ConsoleLogging.LogWarning($"User {username} failed to authenticate: Incorrect password", "AUTH");
                    return JObject.FromObject(new { success = false, error = "wrongPassword" });
                }

            ConsoleLogging.LogWarning($"User {username} not found", "AUTH");
            return JObject.FromObject(new { success = false, error = "userNotFound" });
        }

        private static string GenerateJwtToken(string username)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(JwtSecret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, username)
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public static ClaimsPrincipal ValidateJwtToken(string token)
        {
            if (_blacklistedTokens.Contains(token))
                // Token is blacklisted, invalidating the request
                return null;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(JwtSecret);

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, validationParams, out var validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }

        public static JObject LogoutUser(string token)
        {
            // Add the token to the blacklist to invalidate it
            if (string.IsNullOrWhiteSpace(token))
                return JObject.FromObject(new { success = false, error = "Token is required" });

            if (_blacklistedTokens.Contains(token))
                return JObject.FromObject(new { success = true, message = "User already logged out" });

            _blacklistedTokens.Add(token); // Invalidate the token
            return JObject.FromObject(new { success = true, message = "User logged out successfully" });
        }

        private class User
        {
            public string Username { get; set; }
            public string PasswordHash { get; set; }
        }
    }
}