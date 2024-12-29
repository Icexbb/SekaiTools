using SekaiDataFetch;
using SekaiDataFetch.List;


namespace Test
{
    public static class Program
    {
        public static void Main()
        {
            FetchSource().Wait();
        }

        private static async Task FetchSource()
        {
            var e = new ListEventStory(proxy: new Proxy("127.0.0.1", 7897, Proxy.Type.Socks5));
            await e.Refresh();
        }
    }
}