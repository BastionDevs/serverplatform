using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace serverplatform
{
    internal class UserAuth
    {
        private const string UsersFilePath = "users.json";
        private const string AppSettingsPath = "appsettings.json";
        private const string SessionsFilePath = "auth_sessions.json";
        private const string JwtIssuer = "serverplatform";
        private const string JwtAudience = "serverplatform-panel";
        private const string LegacyJwtSecret = "&&Z8dAl0!1$jxBIJMv1cUy7iaAsa#Vat";
        private const int PasswordIterations = 100000;
        private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
        private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
        private static readonly object SessionsLock = new object();
        private static readonly object UsersLock = new object();
        private static string _jwtSecret;

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
                var config = JObject.Parse(File.ReadAllText(AppSettingsPath));
                var secret = config["Jwt"]?["Secret"]?.ToString();
                if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
                    throw new Exception("JWT Secret must contain at least 32 bytes");

                if (secret == LegacyJwtSecret)
                {
                    secret = GenerateRandomToken(48);
                    config["Jwt"]["Secret"] = secret;
                    File.WriteAllText(AppSettingsPath, config.ToString(Formatting.Indented));
                    ConsoleLogging.LogWarning(
                        "The publicly known legacy JWT secret was replaced; existing access tokens are invalid.",
                        "SETUP");
                }
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
            if (File.Exists(UsersFilePath) && File.Exists(AppSettingsPath))
            {
                ConsoleLogging.LogWarning("First run already completed. Setup aborted.", "SETUP");
                return;
            }

            if (!File.Exists(UsersFilePath))
            {
                var defaultUser = new List<User>
                {
                    new User
                    {
                        Username = "admin", DisplayName = "Administrator",
                        Website = "https://github.com/BastionDevs/serverplatform",
                        PasswordHash = HashPassword("admin")
                    }
                };
                File.WriteAllText(UsersFilePath, JsonConvert.SerializeObject(defaultUser, Formatting.Indented));
            }

            if (!File.Exists(AppSettingsPath))
            {
                var secret = GenerateRandomToken(48);
                File.WriteAllText(AppSettingsPath,
                    JObject.FromObject(new { Jwt = new { Secret = secret } }).ToString(Formatting.Indented));
            }

            ConsoleLogging.LogSuccess("Default user and appsettings.json created.", "SETUP");
        }

        private static string LegacySha256Hash(string password)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static JObject AuthenticateUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                return Error("missingFields");
            if (!File.Exists(UsersFilePath))
                return Error("serverError");

            try
            {
                User user;
                lock (UsersLock)
                {
                    var users = JsonConvert.DeserializeObject<List<User>>(File.ReadAllText(UsersFilePath));
                    user = users?.Find(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

                    // Keep the externally visible result identical for unknown users and bad passwords.
                    if (user == null || !VerifyPassword(password, user.PasswordHash))
                    {
                        ConsoleLogging.LogWarning("Authentication failed for supplied credentials.", "AUTH");
                        return Error("invalidCredentials");
                    }

                    if (IsLegacyPasswordHash(user.PasswordHash))
                    {
                        user.PasswordHash = HashPassword(password);
                        SaveUsers(users);
                    }
                }

                var tokens = IssueTokenPair(user.Username);
                ConsoleLogging.LogSuccess($"User {user.Username} authenticated.", "AUTH");
                return tokens;
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Authentication failed: {ex.Message}", "AUTH");
                return Error("serverError");
            }
        }

        public static JObject RefreshTokens(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return Error("refreshTokenRequired");

            lock (SessionsLock)
            {
                try
                {
                    var store = LoadSessions();
                    var now = DateTime.UtcNow;
                    RemoveExpiredSessions(store, now);
                    var hash = HashToken(refreshToken);
                    var session = store.RefreshTokens.FirstOrDefault(s => FixedTimeEquals(s.TokenHash, hash));

                    if (session == null || session.ExpiresUtc <= now)
                    {
                        SaveSessions(store);
                        return Error("invalidRefreshToken");
                    }

                    if (session.Revoked)
                    {
                        // A rotated token was replayed: revoke the whole token family.
                        foreach (var related in store.RefreshTokens.Where(s => s.FamilyId == session.FamilyId))
                            related.Revoked = true;
                        SaveSessions(store);
                        ConsoleLogging.LogWarning("Refresh token reuse detected; token family revoked.", "AUTH");
                        return Error("invalidRefreshToken");
                    }

                    if (!UserExists(session.Username))
                    {
                        session.Revoked = true;
                        SaveSessions(store);
                        return Error("invalidRefreshToken");
                    }

                    session.Revoked = true;
                    var replacement = CreateRefreshSession(session.Username, session.FamilyId, now);
                    store.RefreshTokens.Add(replacement.Session);
                    SaveSessions(store);
                    return TokenResponse(session.Username, replacement.PlainTextToken);
                }
                catch (Exception ex)
                {
                    ConsoleLogging.LogError($"Token refresh failed: {ex.Message}", "AUTH");
                    return Error("serverError");
                }
            }
        }

        public static JObject RegisterUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return Error("missingFields");

            try
            {
                lock (UsersLock)
                {
                    var users = File.Exists(UsersFilePath)
                        ? JsonConvert.DeserializeObject<List<User>>(File.ReadAllText(UsersFilePath)) ?? new List<User>()
                        : new List<User>();
                    if (users.Exists(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                        return Error("userExists");

                    users.Add(new User { Username = username, PasswordHash = HashPassword(password) });
                    SaveUsers(users);
                }
                return JObject.FromObject(new { success = true, message = "User registered successfully" });
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogError($"Failed to register user: {ex.Message}", "AUTH");
                return Error("serverError");
            }
        }

        private static JObject IssueTokenPair(string username)
        {
            lock (SessionsLock)
            {
                var now = DateTime.UtcNow;
                var store = LoadSessions();
                RemoveExpiredSessions(store, now);
                var refresh = CreateRefreshSession(username, Guid.NewGuid().ToString("N"), now);
                store.RefreshTokens.Add(refresh.Session);
                SaveSessions(store);
                return TokenResponse(username, refresh.PlainTextToken);
            }
        }

        private static JObject TokenResponse(string username, string refreshToken)
        {
            var accessToken = GenerateJwtToken(username);
            return JObject.FromObject(new
            {
                success = true,
                token = accessToken, // Backwards-compatible alias.
                accessToken,
                refreshToken,
                tokenType = "Bearer",
                expiresIn = (int)AccessTokenLifetime.TotalSeconds,
                refreshExpiresIn = (int)RefreshTokenLifetime.TotalSeconds
            });
        }

        private static string GenerateJwtToken(string username)
        {
            var now = DateTime.UtcNow;
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
                }),
                Issuer = JwtIssuer,
                Audience = JwtAudience,
                IssuedAt = now,
                NotBefore = now,
                Expires = now.Add(AccessTokenLifetime),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                    SecurityAlgorithms.HmacSha256Signature)
            };
            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(descriptor));
        }

        public static ClaimsPrincipal ValidateJwtToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var parameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                    ValidateIssuer = true,
                    ValidIssuer = JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = JwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
                };
                var principal = handler.ValidateToken(token, parameters, out var validatedToken);
                var jwt = validatedToken as JwtSecurityToken;
                if (jwt == null || !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256,
                        StringComparison.Ordinal))
                    return null;

                var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                lock (SessionsLock)
                {
                    var store = LoadSessions();
                    RemoveExpiredSessions(store, DateTime.UtcNow);
                    if (!string.IsNullOrEmpty(jti) && store.RevokedAccessTokens.Any(t => t.Jti == jti))
                        return null;
                }
                return principal;
            }
            catch (Exception ex)
            {
                ConsoleLogging.LogWarning($"Invalid JWT: {ex.Message}", "JWT");
                return null;
            }
        }

        public static ClaimsPrincipal VerifyJwtFromContext(HttpListenerContext context)
        {
            var authHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrWhiteSpace(authHeader) ||
                !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;
            var token = authHeader.Substring("Bearer ".Length).Trim();
            return string.IsNullOrEmpty(token) ? null : ValidateJwtToken(token);
        }

        public static string GetUsernameFromContext(HttpListenerContext context)
        {
            return GetUsernameFromPrincipal(VerifyJwtFromContext(context));
        }

        public static string GetUsernameFromPrincipal(ClaimsPrincipal principal)
        {
            if (principal == null)
                return null;
            var username = principal.Identity?.Name ?? principal.FindFirst(ClaimTypes.Name)?.Value ??
                           principal.FindFirst("username")?.Value ?? principal.FindFirst("sub")?.Value;
            return string.IsNullOrWhiteSpace(username) ? null : username;
        }

        public static JObject LogoutUser(string accessToken, string refreshToken)
        {
            var principal = ValidateJwtToken(accessToken);
            if (principal == null)
                return Error("invalidAccessToken");

            lock (SessionsLock)
            {
                var now = DateTime.UtcNow;
                var store = LoadSessions();
                RemoveExpiredSessions(store, now);
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
                var jti = jwt.Id;
                if (!string.IsNullOrEmpty(jti) && store.RevokedAccessTokens.All(t => t.Jti != jti))
                    store.RevokedAccessTokens.Add(new RevokedAccessToken { Jti = jti, ExpiresUtc = jwt.ValidTo });

                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    var hash = HashToken(refreshToken);
                    var session = store.RefreshTokens.FirstOrDefault(s => FixedTimeEquals(s.TokenHash, hash));
                    if (session != null)
                        foreach (var related in store.RefreshTokens.Where(s => s.FamilyId == session.FamilyId))
                            related.Revoked = true;
                }

                SaveSessions(store);
            }
            return JObject.FromObject(new { success = true, message = "User logged out successfully" });
        }

        private static RefreshTokenResult CreateRefreshSession(string username, string familyId, DateTime now)
        {
            var token = GenerateRandomToken(64);
            return new RefreshTokenResult
            {
                PlainTextToken = token,
                Session = new RefreshTokenSession
                {
                    TokenHash = HashToken(token), Username = username, FamilyId = familyId,
                    CreatedUtc = now, ExpiresUtc = now.Add(RefreshTokenLifetime)
                }
            };
        }

        private static string GenerateRandomToken(int byteCount)
        {
            var bytes = new byte[byteCount];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string HashToken(string token)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(token)));
        }

        private static string HashPassword(string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            using (var derive = new Rfc2898DeriveBytes(password, salt, PasswordIterations, HashAlgorithmName.SHA256))
            {
                return $"pbkdf2-sha256${PasswordIterations}${Convert.ToBase64String(salt)}$" +
                       Convert.ToBase64String(derive.GetBytes(32));
            }
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            if (IsLegacyPasswordHash(storedHash))
                return FixedTimeEquals(storedHash, LegacySha256Hash(password));

            var parts = storedHash?.Split('$');
            if (parts == null || parts.Length != 4 || parts[0] != "pbkdf2-sha256" ||
                !int.TryParse(parts[1], out var iterations) || iterations < 10000)
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                using (var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                    return FixedTimeEquals(expected, derive.GetBytes(expected.Length));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool IsLegacyPasswordHash(string hash)
        {
            return hash != null && hash.Length == 64 && hash.All(Uri.IsHexDigit);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0;
            for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0;
            for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private static bool UserExists(string username)
        {
            lock (UsersLock)
            {
                if (!File.Exists(UsersFilePath)) return false;
                var users = JsonConvert.DeserializeObject<List<User>>(File.ReadAllText(UsersFilePath));
                return users != null && users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static void SaveUsers(List<User> users)
        {
            var temporaryPath = UsersFilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(users, Formatting.Indented));
            if (File.Exists(UsersFilePath)) File.Replace(temporaryPath, UsersFilePath, null);
            else File.Move(temporaryPath, UsersFilePath);
        }

        private static SessionStore LoadSessions()
        {
            if (!File.Exists(SessionsFilePath)) return new SessionStore();
            return JsonConvert.DeserializeObject<SessionStore>(File.ReadAllText(SessionsFilePath)) ?? new SessionStore();
        }

        private static void SaveSessions(SessionStore store)
        {
            var temporaryPath = SessionsFilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(store, Formatting.Indented));
            if (File.Exists(SessionsFilePath))
                File.Replace(temporaryPath, SessionsFilePath, null);
            else
                File.Move(temporaryPath, SessionsFilePath);
        }

        private static void RemoveExpiredSessions(SessionStore store, DateTime now)
        {
            store.RefreshTokens.RemoveAll(t => t.ExpiresUtc <= now);
            store.RevokedAccessTokens.RemoveAll(t => t.ExpiresUtc <= now);
        }

        private static JObject Error(string error)
        {
            return JObject.FromObject(new { success = false, error });
        }

        private class User
        {
            public string Username { get; set; }
            public string DisplayName { get; set; }
            public string Website { get; set; }
            public string PasswordHash { get; set; }
        }

        private class SessionStore
        {
            public List<RefreshTokenSession> RefreshTokens { get; set; } = new List<RefreshTokenSession>();
            public List<RevokedAccessToken> RevokedAccessTokens { get; set; } = new List<RevokedAccessToken>();
        }

        private class RefreshTokenSession
        {
            public string TokenHash { get; set; }
            public string Username { get; set; }
            public string FamilyId { get; set; }
            public DateTime CreatedUtc { get; set; }
            public DateTime ExpiresUtc { get; set; }
            public bool Revoked { get; set; }
        }

        private class RevokedAccessToken
        {
            public string Jti { get; set; }
            public DateTime ExpiresUtc { get; set; }
        }

        private class RefreshTokenResult
        {
            public string PlainTextToken { get; set; }
            public RefreshTokenSession Session { get; set; }
        }
    }
}
