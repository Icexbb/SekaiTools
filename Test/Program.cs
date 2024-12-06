// // See https://aka.ms/new-console-template for more information
//

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;


var vapourExecutable = Path.GetRelativePath(".", "./vs/VSPipe.exe");
var vapourScript = Path.GetRelativePath(".", "./vs/lim5994.vpy");
var ffmpegExecutable = Path.GetRelativePath(".", "./vs/ffmpeg.exe");

const string source = @"F:\ProjectSekai\录屏存档\VBS Archives\VBS Archives_#001.mp4";
const string subtitle = @"F:\ProjectSekai\录屏存档\VBS Archives\VBS Archives_#001.ass";
const string output = @"F:\ProjectSekai\录屏存档\VBS Archives\VBS Archives_#001_h264.mp4";

var vapourCommand =
    $"""
     {vapourExecutable} "{vapourScript}" - -c y4m -a "source={source}" -a "subtitle={subtitle}"
     """;
var ffmpegCommand =
    @$"{ffmpegExecutable} -f yuv4mpegpipe -i - -i ""{source}"" " +
    "-map 0:v -map 1:1 " +
    "-c:v libx264 -psy-rd 0.4:0.15 -crf 21 " +
    "-c:a copy " +
    $"\"{output}\" -y " +
    "-nostats -loglevel quiet -progress -";

var command = vapourCommand + " | " + ffmpegCommand;

Console.WriteLine(command);
var process = new Process();
process.StartInfo.FileName = "cmd.exe";
process.StartInfo.Arguments = "/C " + command;
process.StartInfo.UseShellExecute = false;
process.StartInfo.CreateNoWindow = false;
process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

process.StartInfo.RedirectStandardInput = false; //不重定向输入  
process.StartInfo.RedirectStandardOutput = true; //重定向输出 

Console.WriteLine(bool.Parse("" + ""));