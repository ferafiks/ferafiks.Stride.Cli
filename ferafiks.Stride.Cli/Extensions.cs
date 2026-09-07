using Stride.Core.Assets;
using Stride.Core.Diagnostics;

namespace ferafiks.Stride.Cli;

public static partial class Extensions
{
    public static string MakePathRelative(this string s) =>
        s.MakePathRelative(Environment.CurrentDirectory);
    
    public static string MakePathRelative(this string s, string relativeTo) =>
        Path.GetRelativePath(s, relativeTo);
    
    public static TextWriter GetConsoleTextWriter(this LogMessageType messageType)
    {
        return messageType switch
        {
            LogMessageType.Fatal => Console.Error,
            LogMessageType.Error => Console.Error,
            _ => Console.Out,
        };
    }

    public static IEnumerable<Type> AllAssetTypes(this IAssetImporter importer) =>
        importer.RootAssetTypes.Concat(importer.AdditionalAssetTypes);
}
