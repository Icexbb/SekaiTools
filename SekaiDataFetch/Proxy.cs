namespace SekaiDataFetch;

public class Proxy(
    string host,
    int port,
    Proxy.Type proxyType = Proxy.Type.None,
    string username = "",
    string password = "")
{
    public enum Type
    {
        Socks5,
        Http,
        System,
        None
    }

    public string Host { get; private set; } = host;
    public int Port { get; private set; } = port;
    private string Username { get; set; } = username;
    private string Password { get; set; } = password;
    public Type ProxyType { get; private set; } = proxyType;

    public static Proxy None => new("", 0);

    public string GetProxyString()
    {
        return ProxyType switch
        {
            Type.None => "",
            Type.System => "System",
            _ => $"{ProxyType.ToString().ToLower()}://{Username}:{Password}@{Host}:{Port}"
        };
    }

    public void SetProxy(string host, int port, Type proxyType = Type.None, string username = "",
        string password = "")
    {
        Host = host;
        Port = port;
        ProxyType = proxyType;
        Username = username;
        Password = password;
    }

    public void SetProxy(Proxy proxy)
    {
        Host = proxy.Host;
        Port = proxy.Port;
        ProxyType = proxy.ProxyType;
        Username = proxy.Username;
        Password = proxy.Password;
    }
}