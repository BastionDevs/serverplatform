using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace servermgr
{
    internal class BackendAuth
    {

        public static string getAuthToken(string user, string pass)
        {
            string serverAddr = user.Split('@')[1];
            user = user.Split('@')[1];

            if (!serverAddr.Contains(":"))
            {
                serverAddr += ":5678";
            }

            string resp = APIUtil.PostJson($"http://{serverAddr}/auth/login", "{\"username\": \""+user+"\", \"password\": \""+pass+"\"}");

            if (resp.StartsWith("Error:") || resp.StartsWith("Exception:"))
            {
                return "ERROR-AuthFailure-"+resp;
            }
            else
            {
                try
                {
                    var json = JObject.Parse(resp);
                    bool success = json.Value<bool>("success");

                    if (success)
                    {
                        return json.Value<string>("token");
                    }
                    else
                    {
                        return "ERROR-AuthFailure-WrongCreds";
                    }
                }
                catch (Exception ex)
                {
                    return "ERROR-AuthFailure-" + ex.Message;
                }
            }
        }

    }
}
