using NuGet.Packaging;
using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using Stride.Core.Diagnostics;
using Stride.Core.Solutions;
using System.Collections;
using System.Reflection;

namespace ferafiks.Stride.Cli.Core;

/// <summary>Provides helper methods for managing a solution.</summary>
public sealed class SolutionManager
{
    public PackageFileManager PackageFileManager { get; } = new();

    public PackageSession? CurrentSession { get; set; }

    /// <summary>Looks for a path for the solution file.</summary>
    /// <param name="path">The provided path. If null, uses <see cref="Environment.CurrentDirectory"/>.</param>
    /// <param name="result">Full path to the solution file if the file was found, otherwise null.</param>
    /// <returns>Returns true if it managed to find the solution file.</returns>
    public bool TryFindSolution(string? path, out string? result)
    {
        path ??= Environment.CurrentDirectory;
        path = Path.GetFullPath(path);

        // Return path if it points directly to the solution
        if (File.Exists(path))
        {
            if (!Solution.SolutionExtensions.Any(x => path.EndsWith(x)))
            {
                Console.Error.WriteLine($"Provided solution uses an unsupported file format. The tool supports the following extensions: {string.Join(", ", Solution.SolutionExtensions)}.");
                result = "";
                return false;
            }

            result = path;
            return true;
        }
        
        // Otherwise, this has to be a directory
        if (Directory.Exists(path))
        {
            var files = Directory.GetFiles(path)
                .Where(x => Solution.SolutionExtensions.Any(y => x.EndsWith(y)));
            
            if (files.Any())
            {
                if (files.Count() != 1)
                {
                    Console.Error.WriteLine("Provided folder contains multiple solution files!");
                    result = "";
                    return false;
                }

                result = files.First();
                return true;
            }
        }

        Console.Error.WriteLine("Provided solution location doesn't exist!");
        result = "";
        return false;
    }

    /// <summary>Tries to load a solution file.</summary>
    /// <param name="path">Path to the solution file.</param>
    /// <param name="session">The loaded session if successfull, otherwise null.</param>
    /// <param name="ignoreErrors">When true, the load will succeed even if there were errors during loading.</param>
    /// <param name="minimumMessageLevel">The minimum message level to appear in the console.</param>
    /// <returns>Returns true if the solution was loaded successfully.</returns>
    public bool TryLoadSolution(string path, out PackageSession? result, bool ignoreErrors = false, LogMessageType minimumMessageLevel = LogMessageType.Error)
    {
        if (!CliLogger.BlockStandardWrite)
            Console.WriteLine("Attempting to load solution file...");

        var sessionResult = CreateResult(minimumMessageLevel);
        PackageSession.Load(path, sessionResult);
        CurrentSession = sessionResult.Session;

        if (sessionResult.HasErrors)
        {
            Console.Error.WriteLine("Some errors occured while loading the solution.");

            if (ignoreErrors)
            {
                result = sessionResult.Session;
                return true;    
            }

            result = null;
            return false;
        }

        if (!CliLogger.BlockStandardWrite)
            Console.WriteLine("Solution loaded successfully.");
        
        result = sessionResult.Session;
        return true;
    }

    public bool TryFindAndLoadSolution(string? path, out PackageSession? result, bool ignoreErrors = false, LogMessageType minimumMessageLevel = LogMessageType.Error)
    {
        if (TryFindSolution(path, out var solutionLocation) &&
            TryLoadSolution(solutionLocation!, out result, ignoreErrors, minimumMessageLevel))
            return true;
        
        result = null;
        return false;
    }

    private static PackageSessionResult CreateResult(LogMessageType minimumMessageLevel)
    {
        PackageSessionResult sessionResult = new();
        sessionResult.MessageLogged += (obj, args) => CliLogger.HandleLog(args.Message);
        sessionResult.ActivateLog(minimumMessageLevel, LogMessageType.Fatal);
        return sessionResult;
    }

    public AssetDependencyMap CreateAssetDependencyMap(PackageSession session, Package package) =>
        AssetDependencyMap.CreateFromRootAssets(session.DependencyManager, GetRootAssets(session, package));

    public IEnumerable<AssetId> GetRootAssets(PackageSession session, Package package)
    {
        var rootMarked = package.RootAssets.Select(x => session.FindAsset(x.Id))
            .Where(x => x != null)
            .Select(x => x!.Id);
        
        var rootForced = session.Packages
            .SelectMany(x => x.Assets)
            .Where(x => x.Asset is not IProjectAsset)
            .Where(x => x.Asset.GetType().GetCustomAttribute<AssetDescriptionAttribute>()?.AlwaysMarkAsRoot ?? false)
            .Select(x => x.Id);
        
        return rootMarked.Concat(rootForced);
    }

    public bool TryFindPackageFromLocation(PackageSession session, string? packageLocation, out Package? package)
    {
        if (packageLocation == null)
        {
            if (session.CurrentProject?.Package == null)
            {
                Console.Error.WriteLine("Unable to find the default project package. Try specifying a path to it using the --project option.");
                package = null;
                return false;
            }

            package = session.CurrentProject.Package;
            return true;
        }

        package = session.Packages.FirstOrDefault(x => x.FullPath == packageLocation);
        if (package == null)
        {
            Console.Error.WriteLine("Provided package isn't a part of the solution!");
            return false;
        }

        return true;
    }

    public void AddRootAssets(Package package, IEnumerable<AssetReference> references)
    {
        var toAdd = references.Except(package.RootAssets).ToList();
        package.RootAssets.AddRange(toAdd);

        if (PackageFileManager.TrySavePackage(package) && !CliLogger.BlockStandardWrite)
            Console.WriteLine(toAdd.Any() ?
                $"{toAdd.Count()} assets have been added to root: {string.Join(", ", toAdd.Select(x => x.Location))}" :
                "No new assets have been added to root.");
    }

    public void RemoveRootAssets(Package package, IEnumerable<AssetReference> references)
    {
        var toRemove = references.Union(package!.RootAssets).ToList();
        foreach (var item in toRemove)
            package.RootAssets.Remove(item);
        
        if (PackageFileManager.TrySavePackage(package) && !CliLogger.BlockStandardWrite)
            Console.WriteLine(toRemove.Any() ?
                $"{toRemove.Count()} assets have been removed from root: {string.Join(", ", toRemove.Select(x => x.Location))}" :
                "No existing root assets have been removed.");
    }

    public bool AreAssetLocations(IEnumerable<string> paths) =>
        paths.All(x => !File.Exists(x));

    public bool TryGetAssetFromPath(PackageSession session, string path, out AssetItem? asset)
    {
        var allAssets = session.Projects.SelectMany(x => x.Package.Assets)
            .Where(x => x.Asset is not IProjectAsset);

        if (File.Exists(path) && allAssets.FirstOrDefault(x => x.FullPath == Path.GetFullPath(path)) is { } diskResult)
        {
            asset = diskResult;
            return true;
        }

        if (allAssets.FirstOrDefault(x => x.Location == path) is { } locationResult)
        {
            asset = locationResult;
            return true;
        }
        
        Console.Error.WriteLine($"Path {path} did not match any asset on disk or in the content system.");
        asset = null;
        return false;
    }

    public IEnumerable<AssetItem> GetMultipleAssetsFromPaths(PackageSession session, IEnumerable<string> paths) =>
        paths.Select(x => TryGetAssetFromPath(session, x, out var asset) ? asset : null)
            .Where(x => x != null)
            .Select(x => x!);
}
