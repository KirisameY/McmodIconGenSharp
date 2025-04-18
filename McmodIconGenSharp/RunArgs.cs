using CommandLine;

using JetBrains.Annotations;

namespace McmodIconGenSharp;

[UsedImplicitly]
internal record RunArgs(string InputPath, bool Test)
{
    [Value(0, Required = true, HelpText = "Path of input file or directory.")]
    public string InputPath { get; } = InputPath;

    [Option('t', "test", Default = false, Hidden = true)]
    public bool Test { get; } = Test;
}