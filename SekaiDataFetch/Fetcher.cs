using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SekaiDataFetch.Data;


namespace SekaiDataFetch;

public class Fetcher
{
    private SourceList Source { get; } = new();
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

    private class PjSekaiResponse
    {
        public int Total { get; set; }
        public int Limit { get; set; }
        public JObject[] Data { get; set; }
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

    private JObject[]? HttpRequest(string url)
    {
        try
        {
            var handler = GetHttpHandler();
            using var client = new HttpClient(handler);
            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();
            var responseContent = response.Content.ReadAsStringAsync().Result;
            var obj = JsonConvert.DeserializeObject(responseContent);
            switch (obj)
            {
                case JObject:
                {
                    var data = JsonDeserialize<PjSekaiResponse>(responseContent);
                    if (data == null) throw new JsonSerializationException();
                    return data.Total > data.Limit
                        ? HttpRequest(url.Insert(url.IndexOf('?'), $"&limit={data.Total}"))
                        : data.Data;
                }
                case JArray jArray:
                    return jArray.ToObject<JObject[]>()!;
                default:
                    throw new NotSupportedException();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    private List<GameEvent> GetGameEvents()
    {
        var json = HttpRequest(Source.Events);
        return json == null ? [] : json.Select(GameEvent.FromJson).ToList();
    }

    private List<Card> GetCards()
    {
        var json = HttpRequest(Source.Cards);
        return json == null ? [] : json.Select(Card.FromJson).ToList();
    }

    private List<Character2d> GetCharacter2ds()
    {
        var json = HttpRequest(Source.Character2ds);
        return json == null ? [] : json.Select(Character2d.FromJson).ToList();
    }

    private List<UnitStory> GetUnitStories()
    {
        var json = HttpRequest(Source.UnitStories);
        return json == null ? [] : json.Select(UnitStory.FromJson).ToList();
    }

    private List<EventStory> GetEventStories()
    {
        var json = HttpRequest(Source.EventStories);
        return json == null ? [] : json.Select(EventStory.FromJson).ToList();
    }

    private List<CardEpisode> GetCardEpisodes()
    {
        var json = HttpRequest(Source.CardEpisodes);
        return json == null ? [] : json.Select(CardEpisode.FromJson).ToList();
    }

    private List<ActionSet> GetAction()
    {
        var json = HttpRequest(Source.ActionSets);
        return json == null ? [] : json.Select(ActionSet.FromJson).ToList();
    }

    private List<SpecialStory> GetSpecialStories()
    {
        var json = HttpRequest(Source.SpecialStories);
        return json == null ? [] : json.Select(SpecialStory.FromJson).ToList();
    }

    public Data.Data GetData()
    {
        var taskAction = new Task<List<ActionSet>>(GetAction);
        var taskCard = new Task<List<Card>>(GetCards);
        var taskCardEpisode = new Task<List<CardEpisode>>(GetCardEpisodes);
        var taskCharacter2d = new Task<List<Character2d>>(GetCharacter2ds);
        var taskGameEvent = new Task<List<GameEvent>>(GetGameEvents);
        var taskEventStory = new Task<List<EventStory>>(GetEventStories);
        var taskSpecialStory = new Task<List<SpecialStory>>(GetSpecialStories);
        var taskUnitStory = new Task<List<UnitStory>>(GetUnitStories);

        Task.WaitAll(taskAction, taskCard, taskCardEpisode, taskCharacter2d, taskGameEvent,
            taskEventStory, taskSpecialStory, taskUnitStory);

        var result = new Data.Data(
            taskAction.Result,
            taskCard.Result,
            taskCardEpisode.Result,
            taskCharacter2d.Result,
            taskGameEvent.Result,
            taskEventStory.Result,
            taskSpecialStory.Result,
            taskUnitStory.Result
        );
        if (result.NotComplete) throw new Exception("Failed to fetch data");
        return result;
    }
}