using System.Net;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SekaiToolsCore.Story.Fetch.Data;
using Action = SekaiToolsCore.Story.Fetch.Data.Action;

namespace SekaiToolsCore.Story.Fetch;

public class Fetcher
{
    public class SourceList
    {
        public enum SourceType
        {
            SekaiBest,
            Pjsekai,
            // UniPjsk
        }

        private SourceType _source;

        public SourceList(SourceType sourceType = 0) => SetSource(sourceType);

        public SourceType Source
        {
            get => _source;
            set
            {
                _source = value;
                string root;
                switch (_source)
                {
                    case SourceType.SekaiBest:
                        root = "https://sekai-world.github.io/sekai-master-db-diff/";
                        Events = root + "events.json";
                        Cards = root + "cards.json";
                        Character2ds = root + "character2ds.json";
                        UnitStories = root + "unitStories.json";
                        EventStories = root + "eventStories.json";
                        CardEpisodes = root + "cardEpisodes.json";
                        ActionSets = root + "actionSets.json";
                        SpecialStories = root + "specialStories.json";
                        break;
                    case SourceType.Pjsekai:
                        root = "https://api.pjsek.ai/database/master/";
                        Events = root + "events?$limit=2000";
                        Cards = root + "cards?$limit=2000";
                        Character2ds = root + "character2ds?$limit=2000";
                        UnitStories = root + "unitStories?$limit=2000";
                        EventStories = root + "eventStories?$limit=2000";
                        CardEpisodes = root + "cardEpisodes?$limit=2000";
                        ActionSets = root + "actionSets?$limit=2000";
                        SpecialStories = root + "specialStories?$limit=2000";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void SetSource(SourceType sourceType) => Source = sourceType;

        public string Events { get; private set; } = "";
        public string Cards { get; private set; } = "";
        public string Character2ds { get; private set; } = "";
        public string UnitStories { get; private set; } = "";
        public string EventStories { get; private set; } = "";
        public string CardEpisodes { get; private set; } = "";
        public string ActionSets { get; private set; } = "";
        public string SpecialStories { get; private set; } = "";
    }

    private SourceList Source { get; } = new();
    public void SetSource(SourceList.SourceType sourceType) => Source.SetSource(sourceType);
    private Proxy UserProxy { get; set; } = Proxy.None;
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

    private List<Action> GetAction()
    {
        var json = HttpRequest(Source.ActionSets);
        return json == null ? [] : json.Select(Action.FromJson).ToList();
    }

    private List<SpecialStory> GetSpecialStories()
    {
        var json = HttpRequest(Source.SpecialStories);
        return json == null ? [] : json.Select(SpecialStory.FromJson).ToList();
    }

    public Data.Data GetData()
    {
        var taskAction = new Task<List<Action>>(GetAction);
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