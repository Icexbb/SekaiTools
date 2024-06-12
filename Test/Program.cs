// // See https://aka.ms/new-console-template for more information
//
// using SekaiToolsCore;
// using Emgu.CV;
// using Emgu.CV.CvEnum;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SekaiDataFetch;
using SekaiDataFetch.Data;
using SekaiDataFetch.List;

var fetcher = new Fetcher();
fetcher.SetSource(SourceList.SourceType.SiteBest);
fetcher.SetProxy(new Proxy("127.0.0.1", 20001, Proxy.Type.Http));
Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
// var data = (await fetcher.FetchSource(fetcher.Source.SpecialStories))!;
// Console.WriteLine(data.Length);
//
// var dataString = JsonConvert.SerializeObject(data);
//
// var unitStory = data.Select(SpecialStory.FromJson).ToList();
// Console.WriteLine(unitStory.Count);
//
// var jObj = JsonConvert.DeserializeObject<JObject[]>(dataString);
// Console.WriteLine(jObj.Length);
// unitStory = jObj.Select(SpecialStory.FromJson).ToList();
// Console.WriteLine(unitStory.Count);