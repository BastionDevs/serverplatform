﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace servermgr
{
    internal class BackendAuthAsync
    {

        public static async Task<string> PostJsonAsync(string url, string jsonBody)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, content);

                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return $"Error: {responseBody}";
                    }

                    return responseBody;
                }
            }
            catch (Exception ex)
            {
                return $"Exception: {ex.Message}";
            }
        }

        public static async Task<string> PostAuthenticatedAsync(string url, string bearerToken, string body = "")
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // Add bearer token if provided
                    if (!string.IsNullOrEmpty(bearerToken))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                    }

                    // Prepare content (form-urlencoded by default)
                    var content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded");

                    var response = await client.PostAsync(url, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return $"Error ({(int)response.StatusCode}): {responseBody}";
                    }

                    return responseBody;
                }
            }
            catch (Exception ex)
            {
                return $"Exception: {ex.Message}";
            }
        }


        public static async Task<string> getAuthTokenAsync(string user, string pass)
        {
            string serverAddr = user.Split('@')[1];
            user = user.Split('@')[0];

            if (!serverAddr.Contains(":"))
            {
                serverAddr += ":5678";
            }

            string url = $"http://{serverAddr}/auth/login";
            string jsonBody = $"{{\"username\": \"{user}\", \"password\": \"{pass}\"}}";

            string resp = await PostJsonAsync(url, jsonBody);

            if (resp.StartsWith("Error:") || resp.StartsWith("Exception:"))
            {
                return "ERROR-AuthFailure-" + resp;
            }

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
