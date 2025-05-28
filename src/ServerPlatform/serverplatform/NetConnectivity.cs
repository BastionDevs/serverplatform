using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace serverplatform
{
    internal class NetConnectivity
    {
        public static bool CanConnectHttp(string url)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0"); // Some servers require this
                    using (client.OpenRead(url))
                    {
                        return true; // Connection succeeded
                    }
                }
            }
            catch
            {
                return false; // Failed to connect
            }
        }
    }
}
