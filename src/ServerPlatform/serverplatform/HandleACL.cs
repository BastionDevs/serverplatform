using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace serverplatform
{
    internal class HandleACL
    {
        public static bool UrlAclExists(string prefix, string user = null)
        {
            // Normalize the input the way netsh normalizes it
            string normalizedPrefix = NormalizePrefix(prefix);

            ProcessStartInfo psi = new ProcessStartInfo("netsh", "http show urlacl");
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            Process p = Process.Start(psi);
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            // Matches lines like: "URL : http://+:8080/"
            Regex urlLine = new Regex(
                @"^\s*(?:URL|Reserved\s+URL)\s*:\s*(\S+)\s*$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            MatchCollection matches = urlLine.Matches(output);

            foreach (Match m in matches)
            {
                string found = NormalizePrefix(m.Groups[1].Value);

                if (string.Equals(found, normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(user))
                        return true;

                    // Find the block of text for this URL
                    int startIndex = m.Index;
                    int endIndex = (matches.Cast<Match>()
                                           .Where(x => x.Index > startIndex)
                                           .Select(x => x.Index)
                                           .DefaultIfEmpty(output.Length)
                                           .Min());

                    string urlBlock = output.Substring(startIndex, endIndex - startIndex);

                    // Look for "User : <username>"
                    Regex userLine = new Regex(@"^\s*User\s*:\s*(.+)$",
                        RegexOptions.Multiline | RegexOptions.IgnoreCase);

                    foreach (Match um in userLine.Matches(urlBlock))
                    {
                        string foundUser = um.Groups[1].Value.Trim();
                        if (string.Equals(foundUser, user, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }

                    return false; // URL exists but user not found
                }
            }

            return false; // URL not found
        }

        public static void AddUrlAcl(string prefix, string user = "Everyone")
        {
            string normalizedPrefix = NormalizePrefix(prefix);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "netsh";
            psi.Arguments = "http add urlacl url=" + normalizedPrefix + " user=" + user;
            psi.UseShellExecute = true;   // REQUIRED for runas
            psi.Verb = "runas";           // Triggers UAC
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            Process p = null;

            try
            {
                p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "netsh failed with exit code " + p.ExitCode);
                }
            }
            finally
            {
                if (p != null)
                    p.Dispose();
            }
        }

        private static string NormalizePrefix(string prefix)
        {
            prefix = prefix.Trim();

            // Ensure trailing slash
            if (!prefix.EndsWith("/"))
                prefix += "/";

            // Normalize hostname: netsh replaces host with '+'
            // So normalize any hostname except '*' to '+'
            Uri uri;
            if (Uri.TryCreate(prefix, UriKind.Absolute, out uri))
            {
                string scheme = uri.Scheme;
                int port = uri.Port;
                string path = uri.AbsolutePath;

                // netsh uses wildcard host for all hosts
                string normalized = scheme + "://+:" + port + path;

                // Ensure trailing slash always exists
                if (!normalized.EndsWith("/"))
                    normalized += "/";

                return normalized;
            }

            return prefix;
        }
    }
}
