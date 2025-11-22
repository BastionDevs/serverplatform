using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace httpsysutil
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Title = "Bastion Server Platform | HTTP System Utility";
                Console.WriteLine("Bastion Server Platform");
                Console.WriteLine("HTTP System Utility");
                Console.WriteLine();
                Console.WriteLine("Missing options. Try 'httpsysutil --help' for more information");
                Environment.Exit(1);
            } if (args.Length >= 1)
            {
                if (args[0] == "check")
                {
                    if (args.Length == 3)
                    {
                        if (args[1] == "acl")
                        {
                            Console.Title = "Bastion Server Platform | HTTP System Utility";
                            Console.WriteLine(UrlAclExists(args[2]));
                            Environment.Exit(0);
                        } else
                        {
                            Console.Title = "Bastion Server Platform | HTTP System Utility";
                            Console.WriteLine("Bastion Server Platform");
                            Console.WriteLine("HTTP System Utility");
                            Console.WriteLine();
                            Console.WriteLine("Invalid option. Try 'httpsysutil --help' for more information");
                            Environment.Exit(1);
                        }
                    } else
                    {
                        Console.Title = "Bastion Server Platform | HTTP System Utility";
                        Console.WriteLine("Bastion Server Platform");
                        Console.WriteLine("HTTP System Utility");
                        Console.WriteLine();
                        Console.WriteLine("Missing options. Try 'httpsysutil --help' for more information");
                        Environment.Exit(1);
                    }
                }
            }
        }

        public static bool UrlAclExists(string prefix)
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
                    return true;
            }

            return false;
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
