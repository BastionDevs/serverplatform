using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace serverplatform
{
    internal class ServerIndex
    {
        private Dictionary<string, List<Server>> serverIndex;
        private string IndexFile = "";

        public ServerIndex(string indexJson = "servers.json")
        {
            IndexFile = indexJson;
            serverIndex = new Dictionary<string, List<Server>>(StringComparer.OrdinalIgnoreCase);
            LoadServersFromFile(IndexFile);
        }

        private void LoadServersFromFile(string filename)
        {
            if (!File.Exists(filename))
                return;

            string json = File.ReadAllText(filename);

            var data = JsonConvert.DeserializeObject<Dictionary<string, List<Server>>>(json);
            if (data != null)
                serverIndex = data;
        }

        public void SaveServersToFile()
        {
            string json = JsonConvert.SerializeObject(serverIndex, Formatting.Indented);
            File.WriteAllText(IndexFile, json);
        }

        /// <summary>
        /// Adds a server to the owning user's list.
        /// Creates the user entry if it doesn't exist.
        /// </summary>
        public void AddServer(Server server)
        {
            if (string.IsNullOrWhiteSpace(server.Owner))
                throw new ArgumentException("Server must have an Owner");

            if (!serverIndex.TryGetValue(server.Owner, out var servers))
            {
                servers = new List<Server>();
                serverIndex[server.Owner] = servers;
            }

            servers.Add(server);
            SaveServersToFile();
        }

        public IReadOnlyList<Server> GetServersForUser(string username)
        {
            if (serverIndex.TryGetValue(username, out var servers))
                return servers;

            return Array.Empty<Server>();
        }

        public bool RemoveServer(string owner, string serverId)
        {
            if (!serverIndex.TryGetValue(owner, out var servers))
                return false;

            int removed = servers.RemoveAll(s => s.Id == serverId);

            if (servers.Count == 0)
                serverIndex.Remove(owner); // optional cleanup

            if (removed > 0)
                SaveServersToFile();

            return removed > 0;
        }
    }
}

public class Server
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Owner { get; set; } // This should be the username, not the user ID
    //public string Node { get; set; }
    public string Software { get; set; }
    public DateTime CreatedAt { get; set; }
}
