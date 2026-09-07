using Stride.Core.Diagnostics;

namespace ferafiks.Stride.Cli;

public class CliLogger : Logger
{
    public static bool BlockStandardWrite { get; set; }

    public CliLogger() : base()
    {
        ActivateLog(LogMessageType.Info, LogMessageType.Fatal);
    }

    protected override void LogRaw(ILogMessage logMessage) =>
        HandleLog(logMessage);

    public static void HandleLog(ILogMessage logMessage)
    {
        var writer = logMessage.Type switch
        {
            LogMessageType.Fatal => Console.Error,
            LogMessageType.Error => Console.Error,
            _ => Console.Out,
        };

        if (writer == Console.Out && BlockStandardWrite)
            return;

        writer.WriteLine(logMessage.Text);
    }
}
