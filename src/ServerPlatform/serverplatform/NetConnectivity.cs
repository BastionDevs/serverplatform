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
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36"); // Some servers require this
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
