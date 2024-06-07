// // See https://aka.ms/new-console-template for more information
//
// using SekaiToolsCore;
// using Emgu.CV;
// using Emgu.CV.CvEnum;

using SekaiDataFetch;

// var fetcher = new Fetcher();
// fetcher.SetSource(SourceList.SourceType.SiteAi);
// fetcher.SetProxy(new Proxy("127.0.0.1", 20001, Proxy.Type.Http));
// var data = fetcher.GetDataSync();
// Console.WriteLine(data);
var ApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

Console.WriteLine(ApplicationData);