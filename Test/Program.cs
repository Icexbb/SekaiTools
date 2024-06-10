// // See https://aka.ms/new-console-template for more information
//
// using SekaiToolsCore;
// using Emgu.CV;
// using Emgu.CV.CvEnum;

using Newtonsoft.Json;
using SekaiDataFetch;
using SekaiDataFetch.Data;
using SekaiDataFetch.List;

var fetcher = new Fetcher();
fetcher.SetSource(SourceList.SourceType.SiteBest);
fetcher.SetProxy(new Proxy("127.0.0.1", 20001, Proxy.Type.Http));
var data = await fetcher.GetData();
Console.WriteLine(data);
var unitStory = new ListUnitStory(data.UnitStories);


var json = JsonConvert.SerializeObject(unitStory.Data);
Console.WriteLine(json);
var obj = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(json);

foreach (var (key, value) in obj)
{
    foreach (var (key1, value1) in value)
    {
        foreach (var (key2, value2) in value1)
        {
            Console.WriteLine($"{key}-{key1}-{key2} : {value2}");
        }
    }
}