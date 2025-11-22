using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            }
        }
    }
}
