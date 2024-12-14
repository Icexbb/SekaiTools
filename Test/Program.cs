using System.Text.Json;
using System.Text.Json.Nodes;
using SekaiToolsCore.Story.Game;


namespace Test
{
    public static class Program
    {
        public static void Main()
        {
            const string source = @"F:\ProjectSekai\test\1963_areatalk_ev_street_16_003.asset";
            var data = new GameData(source);
            Console.WriteLine(data.TalkData.Length);
            Console.WriteLine(data.SpecialEffectData.Length);
            Console.WriteLine(data.Snippets.Length);
        }
    }
}