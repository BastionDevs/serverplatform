using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace serverplatform
{
    internal class UserAuth
    {
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
    }
}
