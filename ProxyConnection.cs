public class ProxyConnection
{
    public string Hostname { get; set; }
    public uint Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }

    public ProxyConnection(string hostname, uint port, string username = null, string password = null)
    {
        Hostname = hostname;
        Port = port;
        Username = username;
        Password = password;
    }

    public bool HasCredentials()
    {
        return Username != null && Password != null && Username != "" && Password != "";
    }

    public override string ToString()
    {
        string proxy = $"{Hostname}:{Port}";

        if (HasCredentials())
        {
            proxy += $"{Username}:{Password}";
        }

        return proxy;
    }
}