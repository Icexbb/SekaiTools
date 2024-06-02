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
        None,
    }

    public string Host { get; set; } = host;
    public int Port { get; set; } = port;
    public string Username { get; set; } = username;
    public string Password { get; set; } = password;
    public Type ProxyType { get; set; } = proxyType;

    public string GetProxyString()
    {
        if (ProxyType == Type.None)
            return "";
        if (ProxyType == Type.System)
            return "System";
        return $"{ProxyType.ToString().ToLower()}://{Username}:{Password}@{Host}:{Port}";
    }

    public void SetProxy(string host, int port, Proxy.Type proxyType = Proxy.Type.None, string username = "",
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

    public static Proxy None => new Proxy("", 0, Type.None);
}