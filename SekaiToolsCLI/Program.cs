using CommandLine;
using Core;

namespace SekaiToolsCLI;

internal class SekaiToolsCli
{
    private class Options
    {
        [Option('v', "video", Required = true, HelpText = "Input file to be processed.")]
        public string VideoFile { get; set; }

        [Option('j', "json", Required = true, HelpText = "Json file to be processed.")]
        public string JsonFile { get; set; }

        [Option('t', "translate", Required = false, HelpText = "Translation file to be processed.")]
        public string TranslationFile { get; set; }


        // [Option('o', "output", Required = false, HelpText = "Output file to be processed.")]
        // public string OutputFile { get; set; }
    }

    private static void Run(Options options)
    {
        var process = new VideoProcess(options.VideoFile, options.JsonFile, options.TranslationFile);
        process.Process();
    }

    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args).WithParsed(Run);
    }
}