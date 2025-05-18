//Many thanks to Nick Rimmer
//With edits and suggestions from the Stack Overflow community
//View the Original code and comments at https://stackoverflow.com/a/7696370

/* load
   Properties config = new Properties(fileConfig);

   get value whith default value
   com_port.Text = config.get("com_port", "1");

   set value
   config.set("com_port", com_port.Text);

   save
   config.Save()

*/

using System.Collections.Generic;
using System.IO;
using System.Linq;

public class Properties
{
    private string filename;
    private Dictionary<string, string> list;

    public Properties(string file)
    {
        reload(file);
    }

    public string get(string field, string defValue) => get(field) == null ? defValue : get(field);

    public string get(string field) => list.ContainsKey(field) ? list[field] : null;

    public void set(string field, object value)
    {
        if (!list.ContainsKey(field))
            list.Add(field, value.ToString());
        else
            list[field] = value.ToString();
    }

    public void Save()
    {
        Save(filename);
    }

    public void Save(string filename)
    {
        this.filename = filename;

        if (!File.Exists(filename))
            File.Create(filename).Close();

        var file = new StreamWriter(filename);

        foreach (var prop in list.Keys.ToArray())
            if (!string.IsNullOrWhiteSpace(list[prop]))
                file.WriteLine(prop + "=" + list[prop]);

        file.Close();
    }

    public void reload()
    {
        reload(filename);
    }

    public void reload(string filename)
    {
        this.filename = filename;
        list = new Dictionary<string, string>();

        if (File.Exists(filename))
            loadFromFile(filename);
        else
            File.Create(filename).Close();
    }

    private void loadFromFile(string file)
    {
        foreach (var line in File.ReadAllLines(file))
            if (!string.IsNullOrEmpty(line) &&
                !line.StartsWith(";") &&
                !line.StartsWith("#") &&
                !line.StartsWith("'") &&
                line.Contains('='))
            {
                var index = line.IndexOf('=');
                var key = line.Substring(0, index).Trim();
                var value = line.Substring(index + 1).Trim();

                if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                    (value.StartsWith("'") && value.EndsWith("'")))
                    value = value.Substring(1, value.Length - 2);

                try
                {
                    //ignore dublicates
                    list.Add(key, value);
                }
                catch {}
            }
    }
}