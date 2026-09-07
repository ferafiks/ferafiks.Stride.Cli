using System.CommandLine;

namespace ferafiks.Stride.Cli;

public static class CommonArguments
{
    public static Argument<string?> Solution { get; } = new Argument<string?>("solution") { Description = "Path to the solution file or a directory containing one.", Arity = ArgumentArity.ZeroOrOne };
    public static Option<bool> SolutionIgnoreErrors { get; } = new Option<bool>("--ignore-errors") { Description = "Makes the command proceed as normal if there were errors while loading the solution file." };
    public static Option<bool> OnlyResults { get; } = new Option<bool>("--only-result") { Description = "Makes the command only log the result." };
}
