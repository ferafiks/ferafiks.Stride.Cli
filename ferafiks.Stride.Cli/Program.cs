using Microsoft.Build.Locator;
using ferafiks.Stride.Cli;
using ferafiks.Stride.Cli.Core;
using System.CommandLine;

MSBuildLocator.RegisterDefaults();

var solutionManager = new SolutionManager();

var testSolution = new Command("testload", "Attempts to load a Stride solution and displays all log messages.");
testSolution.SetAction(result =>
{
    solutionManager.TryFindAndLoadSolution(result.GetValue(CommonArguments.Solution), out _, true, Stride.Core.Diagnostics.LogMessageType.Verbose);
});

var root = new RootCommand("ferafiks's Stride CLI tool.")
{
    Subcommands =
    {
        testSolution,
        AssetsCommand.Create(solutionManager),
        PackageCommand.Create(solutionManager.PackageFileManager),
    }
};

return await root.Parse(args).InvokeAsync();
