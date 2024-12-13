// // See https://aka.ms/new-console-template for more information
//

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SekaiToolsCore.Story.Game;


const string source = @"F:\ProjectSekai\test\1963_areatalk_ev_street_16_003.asset";
var jsonString = File.ReadAllText(source);
var data1 = JsonSerializer.Deserialize<JsonObject>(jsonString);

Console.WriteLine(data1["TalkData"].GetType());