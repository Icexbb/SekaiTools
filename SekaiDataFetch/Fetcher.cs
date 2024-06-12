using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SekaiDataFetch.Data;


namespace SekaiDataFetch;

public class Fetcher
{
    public SourceList Source { get; } = new();
    private Proxy UserProxy { get; set; } = Proxy.None;
    public void SetSource(SourceList.SourceType sourceType) => Source.SetSource(sourceType);
    public void SetProxy(Proxy proxy) => UserProxy = proxy;

    private HttpMessageHandler GetHttpHandler()
    {
        return UserProxy.ProxyType switch
        {
            Proxy.Type.None => new HttpClientHandler(),
            Proxy.Type.System => new HttpClientHandler(),
            Proxy.Type.Http => new HttpClientHandler
            {
                Proxy = new WebProxy(UserProxy.Host, UserProxy.Port), UseProxy = true
            },
            Proxy.Type.Socks5 => new SocketsHttpHandler
            {
                Proxy = new WebProxy(UserProxy.Host, UserProxy.Port), UseProxy = true
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }


    private record PjSekaiResponse
    {
        public int Total { get; set; }
        public int Limit { get; set; }
        public JObject[] Data { get; set; } = [];
    }

    private static T? JsonDeserialize<T>(string json) where T : class
    {
        try
        {
            var result = JsonConvert.DeserializeObject<T>(json);
            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }


    public async Task<JObject[]?> FetchSource(string url)
    {
        var responseContent = await TryGet();
        switch (Source.Source)
        {
            case SourceList.SourceType.SiteBest: // obj is jArray.
                var jObjects = JsonConvert.DeserializeObject<JObject[]>(responseContent);
                if (jObjects == null) throw new JsonSerializationException();
                return jObjects;
            case SourceList.SourceType.SiteAi:
                var data = JsonDeserialize<PjSekaiResponse>(responseContent);
                if (data == null) throw new JsonSerializationException();
                return data.Total > data.Limit
                    ? await FetchSource(url.Insert(url.IndexOf('?') + 1, $"$limit={data.Total}&"))
                    : data.Data;
            default:
                throw new ArgumentOutOfRangeException();
        }


        async Task<string> TryGet(int time = 5)
        {
            Console.WriteLine($"GET {url}");
            try
            {
                return await Get();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"GET {url} Error: " + (e.InnerException?.Message ?? e.Message));
                if (time > 0) return await TryGet(time - 1);
                throw;
            }
        }

        async Task<string> Get()
        {
            using var client = new HttpClient(GetHttpHandler());
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }


    public async Task<List<GameEvent>> GetGameEvents()
    {
        var json = await FetchSource(Source.Events);
        return json == null ? [] : json.Select(GameEvent.FromJson).ToList();
    }

    private async Task<List<Card>> GetCards()
    {
        var json = await FetchSource(Source.Cards);
        return json == null ? [] : json.Select(Card.FromJson).ToList();
    }

    private async Task<List<Character2d>> GetCharacter2ds()
    {
        var json = await FetchSource(Source.Character2ds);
        return json == null ? [] : json.Select(Character2d.FromJson).ToList();
    }

    public async Task<List<UnitStory>> GetUnitStories()
    {
        var json = await FetchSource(Source.UnitStories);
        return json == null ? [] : json.Select(UnitStory.FromJson).ToList();
    }

    public async Task<List<EventStory>> GetEventStories()
    {
        var json = await FetchSource(Source.EventStories);
        return json == null ? [] : json.Select(EventStory.FromJson).ToList();
    }

    private async Task<List<CardEpisode>> GetCardEpisodes()
    {
        var json = await FetchSource(Source.CardEpisodes);
        return json == null ? [] : json.Select(CardEpisode.FromJson).ToList();
    }

    private async Task<List<ActionSet>> GetAction()
    {
        var json = await FetchSource(Source.ActionSets);
        return json == null ? [] : json.Select(ActionSet.FromJson).ToList();
    }

    private async Task<List<SpecialStory>> GetSpecialStories()
    {
        var json = await FetchSource(Source.SpecialStories);
        return json == null ? [] : json.Select(SpecialStory.FromJson).ToList();
    }
}