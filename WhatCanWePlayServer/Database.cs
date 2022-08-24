namespace WhatCanWePlayServer;

//todo: prolly pridat interface a nejak to zakomponovat s dependenci injection v Program.cs
public class Database
{
    private string filename;

    public Database(string filename)
    {
        if (filename is null)
        {
            throw new ArgumentNullException(nameof(filename)); //nebo udelat inmemory db.
        }

        this.filename = "./" + filename;

        if (!File.Exists(this.filename)) File.Create(this.filename).Close();
    }

    public string Get(string key)
    {
        //todo: this object should cache the contents of the db.txt and only refresh the cache once it's written to the file
        //(that'd b prolly indicated by some flag raised in the Save() method)
        //this should largely help with disk usage on the server

        using (StreamReader sr = new StreamReader(filename))
        {
            string s;
            while ((s = sr.ReadLine()) != null)
            {
                // separate entries in csv by / (forward slash, since it's an illegal folder name in both windows and linux)
                string[] data = s.Split('/');

                if (data[0] == key)
                {
                    return string.Join("/", data[1..]); //all but the first element of the array
                }
            }
        }

        return ""; //no data matched the provided key
    }

    public bool Exists(string key)
    {
        return Get(key) != "";
    }

    public void Save(string key, string value)
    {
        if (!Exists(key))
        {
            File.AppendAllText(filename, key + "/" + value + Environment.NewLine);
        }
        else
        {
            Console.WriteLine("klic existuje, updatim");
            //klic uz existuje, musim updatnout
            string[] lines = File.ReadAllLines(filename);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(key))
                {
                    lines[i] = key + "/" + value + Environment.NewLine;
                    break;
                }
            }
            File.WriteAllLines(filename, lines);
        }
    }
}
