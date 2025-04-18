// See https://aka.ms/new-console-template for more information

using System.Text;

using CommandLine;
using CommandLine.Text;

using McmodIconGenSharp;

using Microsoft.VisualBasic;


#region Parse Args

var headInfo = new StringBuilder()
              .AppendLine(HeadingInfo.Default)
              .AppendLine()
              .AppendLine(CopyrightInfo.Default)
              .AppendLine()
              .AppendLine("Tool for Mcmod style icon generation")
              .AppendLine("see https://github.com/KirisameY/McmodIconGenSharp for more info")
              .ToString();

var parser = new Parser(with => with.HelpWriter = null);
var parserResult = parser.ParseArguments<RunArgs>(args);
var runArgs = parserResult.WithNotParsed(_ =>
{
    var helpText = HelpText.AutoBuild(parserResult, h =>
    {
        h.Heading = "";
        h.Copyright = headInfo;

        return HelpText.DefaultParsingErrorsHandler(parserResult, h);
    }, e => e);
    Console.WriteLine(helpText);
    Environment.Exit(-1);
}).Value;

Console.WriteLine(headInfo);

#endregion

Console.WriteLine(runArgs);

if (runArgs.Test)
{

    return 0;
}

var inputPath = runArgs.InputPath;
if (File.Exists(inputPath))
{
    var file = new FileInfo(inputPath);
    //todo
}
else if (Directory.Exists(inputPath))
{
    var dir = new DirectoryInfo(inputPath);
    //todo
}
else
{
    Console.WriteLine($"Given file or directory '{inputPath}' does not exists.\n"
                    + $"(or maybe you passed something idk that's neither a file nor a dir)");
    return 0;
}


return 0;