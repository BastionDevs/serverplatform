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
        private const string UsersFilePath = "users.json";
        private const string AppSettingsPath = "appsettings.json";
        private static string _jwtSecret;
        private static readonly HashSet<string> BlocklistedTokens = new HashSet<string>();

        private static string JwtSecret
        {
            get
            {
                if (_jwtSecret == null)
                    _jwtSecret = LoadJwtSecret();
                return _jwtSecret;
            }
        }

        private static string LoadJwtSecret()
        {
            try
            {
                var jsonContent = File.ReadAllText(AppSettingsPath);
                var config = JObject.Parse(jsonContent);

                var secret = config["Jwt"]?["Secret"]?.ToString();
                if (string.IsNullOrEmpty(secret)) throw new Exception("JWT Secret is missing in appsettings.json");

                return secret;
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Error loading JWT Secret: {ex.Message}", "SETUP");
                throw;
            }
        }

        public static void CreateDefaultUsers()
        {
            if (File.Exists(UsersFilePath) || File.Exists(AppSettingsPath))
            {
                ConsoleLogging.LogWarning("First run already completed. Setup aborted.", "SETUP");
                return;
            }

            var defaultUser = new List<User>
            {
                new User
                {
                    Username = "admin", DisplayName = "Administrator", Website = "https://github.com/BastionDevs/serverplatform", PasswordHash = Sha256Hash("admin")
                }
            };

            File.WriteAllText(UsersFilePath, JsonConvert.SerializeObject(defaultUser, Formatting.Indented));
            File.WriteAllText(AppSettingsPath, "{ \"Jwt\": { \"Secret\": \"&&Z8dAl0!1$jxBIJMv1cUy7iaAsa#Vat\" } }");

            ConsoleLogging.LogSuccess("Default user and appsettings.json created.", "SETUP");
        }

        private static string Sha256Hash(string password)
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
            if (!File.Exists(UsersFilePath))
            {
                ConsoleLogging.LogError("users.json not found.", "AUTH");
                return JObject.FromObject(new
                {
                    success = false, error = "serverError"
                });
            }

            try
            {
                var json = File.ReadAllText(UsersFilePath);
                var users = JsonConvert.DeserializeObject<List<User>>(json);

                var user = users?.Find(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
                if (user == null)
                {
                    ConsoleLogging.LogWarning($"User not found: {username}", "AUTH");
                    return JObject.FromObject(new
                    {
                        success = false, error = "userNotFound"
                    });
                }

                if (user.PasswordHash != Sha256Hash(password))
                {
                    ConsoleLogging.LogWarning($"Incorrect password for user '{username}'", "AUTH");
                    return JObject.FromObject(new
                    {
                        success = false, error = "wrongPassword"
                    });
                }

                var token = GenerateJwtToken(username);
                ConsoleLogging.LogSuccess($"User {username} authenticated.", "AUTH");
                return JObject.FromObject(new
                {
                    success = true, token
                });
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Authentication failed: {ex.Message}", "AUTH");
                return JObject.FromObject(new
                {
                    success = false, error = "serverError"
                });
            }
        }

        public static JObject RegisterUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ConsoleLogging.LogWarning("Username or password is missing.", "AUTH");
                return JObject.FromObject(new
                {
                    success = false, error = "missingFields"
                });
            }

            try
            {
                var users = File.Exists(UsersFilePath)
                    ? JsonConvert.DeserializeObject<List<User>>(File.ReadAllText(UsersFilePath)) ?? new List<User>()
                    : new List<User>();

                if (users.Exists(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                {
                    ConsoleLogging.LogWarning($"Username already exists: {username}", "AUTH");
                    return JObject.FromObject(new
                    {
                        success = false, error = "userExists"
                    });
                }

                users.Add(new User
                {
                    Username = username, PasswordHash = Sha256Hash(password)
                });
                File.WriteAllText(UsersFilePath, JsonConvert.SerializeObject(users, Formatting.Indented));

                ConsoleLogging.LogSuccess($"User registered: {username}", "AUTH");
                return JObject.FromObject(new
                {
                    success = true, message = "User registered successfully"
                });
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Failed to register user: {ex.Message}", "AUTH");
                return JObject.FromObject(new
                {
                    success = false, error = "serverError"
                });
            }
        }

        private static string GenerateJwtToken(string username)
        {
            var key = Encoding.ASCII.GetBytes(JwtSecret);
            var tokenHandler = new JwtSecurityTokenHandler();

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
            var tokenString = tokenHandler.WriteToken(token);

            ConsoleLogging.LogMessage($"JWT issued for user: {username} [Token length: {tokenString.Length}]", "JWT");
            return tokenString;
        }

        public static ClaimsPrincipal ValidateJwtToken(string token)
        {
            if (BlocklistedTokens.Contains(token))
            {
                ConsoleLogging.LogWarning("Rejected token: Blocklisted", "JWT");
                return null;
            }

            try
            {
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

                var principal = tokenHandler.ValidateToken(token, validationParams, out var validatedToken);

                if (validatedToken is JwtSecurityToken jwtToken)
                    ConsoleLogging.LogMessage(
                        $"JWT validated: User = {principal.Identity?.Name}, Expires = {jwtToken.ValidTo:u}", "JWT");

                return principal;
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Invalid JWT: {ex.Message}", "JWT");
                return null;
            }
        }

        public static JObject LogoutUser(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ConsoleLogging.LogWarning("Logout failed: Token is missing.", "AUTH");
                return JObject.FromObject(new
                {
                    success = false, error = "Token is required"
                });
            }

            var added = BlocklistedTokens.Add(token);
            var logContext = "AUTH";

            if (added)
            {
                ConsoleLogging.LogSuccess("Token blocklisted on logout.", logContext);
                return JObject.FromObject(new
                {
                    success = true, message = "User logged out successfully"
                });
            }

            ConsoleLogging.LogMessage("Logout token already blocklisted.", logContext);
            return JObject.FromObject(new
            {
                success = true, message = "User already logged out"
            });
        }

        private class User
        {
            public string Username { get; set; }
            public string DisplayName { get; set; }
            public string Website { get; set; }
            public string PasswordHash { get; set; }
        }
    }
}